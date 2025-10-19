import os
import json
import time
from wsgiref.simple_server import make_server
from urllib.parse import parse_qs
from http import HTTPStatus
from typing import Dict, Any, Optional

from .db import ensure_schema_and_bootstrap, verify_password, new_session, get_session, end_session, get_db, _exec, _query, get_user, audit_log, create_user, set_user_password

STATIC_DIR = os.path.join(os.path.dirname(__file__), 'static')
TEMPLATES_DIR = os.path.join(os.path.dirname(__file__), 'templates')


def read_body(environ) -> bytes:
    try:
        length = int(environ.get('CONTENT_LENGTH') or 0)
    except (ValueError, TypeError):
        length = 0
    return environ['wsgi.input'].read(length) if length > 0 else b''


def json_response(start_response, payload: Dict[str, Any], status: int = 200):
    body = json.dumps(payload).encode('utf-8')
    headers = [('Content-Type', 'application/json; charset=utf-8'), ('Content-Length', str(len(body)))]
    start_response(f"{status} {HTTPStatus(status).phrase}", headers)
    return [body]


def file_response(start_response, file_path: str, content_type: str = 'text/html; charset=utf-8', status: int = 200):
    try:
        with open(file_path, 'rb') as f:
            data = f.read()
    except FileNotFoundError:
        start_response('404 Not Found', [('Content-Type', 'text/plain')])
        return [b'Not Found']
    headers = [('Content-Type', content_type), ('Content-Length', str(len(data)))]
    start_response(f"{status} {HTTPStatus(status).phrase}", headers)
    return [data]


def parse_cookies(environ) -> Dict[str, str]:
    cookie = environ.get('HTTP_COOKIE') or ''
    out: Dict[str, str] = {}
    for part in cookie.split(';'):
        if '=' in part:
            k, v = part.strip().split('=', 1)
            out[k] = v
    return out


def set_cookie_headers(name: str, value: str, max_age: int = 1800, http_only: bool = True) -> list:
    attrs = [f"{name}={value}", f"Max-Age={max_age}", 'Path=/', 'SameSite=Lax']
    if http_only:
        attrs.append('HttpOnly')
    return [('Set-Cookie', '; '.join(attrs))]


def require_auth(environ) -> Optional[Dict[str, Any]]:
    cookies = parse_cookies(environ)
    sid = cookies.get('sid')
    if not sid:
        return None
    return get_session(sid)


def csrf_valid(environ, sess) -> bool:
    if not sess:
        return False
    header_token = environ.get('HTTP_X_CSRF') or ''
    cookies = parse_cookies(environ)
    cookie_token = cookies.get('csrf') or ''
    expected = sess['csrf_token']
    return header_token == expected and cookie_token == expected


def app(environ, start_response):
    method = environ['REQUEST_METHOD']
    path = environ.get('PATH_INFO') or '/'

    # Static assets
    if path.startswith('/static/'):
        rel = path[len('/static/'):]
        file_path = os.path.join(STATIC_DIR, rel)
        if file_path.endswith('.css'):
            return file_response(start_response, file_path, 'text/css; charset=utf-8')
        if file_path.endswith('.js'):
            return file_response(start_response, file_path, 'application/javascript; charset=utf-8')
        return file_response(start_response, file_path, 'application/octet-stream')

    # Pages
    if path == '/' or path == '/login':
        return file_response(start_response, os.path.join(TEMPLATES_DIR, 'login.html'))
    if path == '/dashboard':
        sess = require_auth(environ)
        if not sess:
            start_response('302 Found', [('Location', '/login')])
            return [b'']
        return file_response(start_response, os.path.join(TEMPLATES_DIR, 'dashboard.html'))

    if path == '/health':
        start_response('200 OK', [('Content-Type','text/plain')])
        return [b'OK']

    # API endpoints
    if path == '/api/login' and method == 'POST':
        data = json.loads(read_body(environ) or b'{}')
        username = (data.get('username') or '').strip()
        password = (data.get('password') or '')
        user_id = verify_password(username, password)
        if not user_id:
            return json_response(start_response, {"ok": False, "error": "Invalid credentials"}, 401)
        sess = new_session(user_id)
        audit_log(user_id, 'login', f"user {username} logged in")
        headers = [('Content-Type', 'application/json; charset=utf-8'), ('Content-Length', '2')]
        headers += set_cookie_headers('sid', sess['id'], max_age=1800, http_only=True)
        headers += set_cookie_headers('csrf', sess['csrf'], max_age=1800, http_only=False)
        start_response('200 OK', headers)
        return [b'{}']

    if path == '/api/logout' and method == 'POST':
        sess = require_auth(environ)
        if not sess or not csrf_valid(environ, sess):
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        cookies = parse_cookies(environ)
        sid = cookies.get('sid')
        if sid:
            end_session(sid)
        start_response('204 No Content', [('Set-Cookie', 'sid=; Max-Age=0; Path=/; SameSite=Lax; HttpOnly')])
        return [b'']

    # Customers CRUD
    if path == '/api/customers':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        if method == 'GET':
            rows = _query(get_db(), 'SELECT * FROM customers ORDER BY name')
            return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})
        if method == 'POST':
            if not csrf_valid(environ, sess):
                return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
            data = json.loads(read_body(environ) or b'{}')
            now = int(time.time())
            _exec(get_db(), 'INSERT INTO customers(name, customer_code, contact_details, system_sid, environment, created_at, updated_at) VALUES(?,?,?,?,?,?,?)',
                  (data.get('name'), data.get('customer_code'), data.get('contact_details'), data.get('system_sid'), data.get('environment'), now, now))
            return json_response(start_response, {"ok": True})

    if path.startswith('/api/customers/'):
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        _, _, cid_str, *rest = path.split('/')
        if not cid_str.isdigit():
            return json_response(start_response, {"ok": False, "error": "Invalid customer id"}, 400)
        cid = int(cid_str)
        if len(rest) == 0:
            if method == 'PUT':
                if not csrf_valid(environ, sess):
                    return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
                data = json.loads(read_body(environ) or b'{}')
                _exec(get_db(), 'UPDATE customers SET name=?, contact_details=?, system_sid=?, environment=?, updated_at=? WHERE id=?',
                      (data.get('name'), data.get('contact_details'), data.get('system_sid'), data.get('environment'), int(time.time()), cid))
                audit_log(sess['user_id'], 'customer.update', json.dumps({'customer_id': cid}))
                return json_response(start_response, {"ok": True})
            if method == 'DELETE':
                if not csrf_valid(environ, sess):
                    return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
                _exec(get_db(), 'DELETE FROM customers WHERE id=?', (cid,))
                audit_log(sess['user_id'], 'customer.delete', json.dumps({'customer_id': cid}))
                return json_response(start_response, {"ok": True})
        elif rest[0] == 'assignments':
            if method == 'GET':
                rows = _query(get_db(), 'SELECT mi.*, ca.id AS assignment_id FROM customer_assignments ca JOIN monitoring_items mi ON ca.item_id=mi.id WHERE ca.customer_id=? ORDER BY mi.category, mi.metric', (cid,))
                return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})
            if method == 'POST':
                if not csrf_valid(environ, sess):
                    return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
                data = json.loads(read_body(environ) or b'{}')
                item_id = int(data.get('item_id'))
                try:
                    _exec(get_db(), 'INSERT INTO customer_assignments(customer_id, item_id) VALUES(?,?)', (cid, item_id))
                except Exception:
                    pass
                audit_log(sess['user_id'], 'assignment.add', json.dumps({'customer_id': cid, 'item_id': item_id}))
                return json_response(start_response, {"ok": True})
            if method == 'DELETE':
                if not csrf_valid(environ, sess):
                    return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
                data = json.loads(read_body(environ) or b'{}')
                item_id = int(data.get('item_id'))
                _exec(get_db(), 'DELETE FROM customer_assignments WHERE customer_id=? AND item_id=?', (cid, item_id))
                audit_log(sess['user_id'], 'assignment.remove', json.dumps({'customer_id': cid, 'item_id': item_id}))
                return json_response(start_response, {"ok": True})

    # Monitoring items catalog
    if path == '/api/items' and method == 'GET':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        q = parse_qs(environ.get('QUERY_STRING') or '')
        cat = q.get('category', [None])[0]
        if cat:
            rows = _query(get_db(), 'SELECT * FROM monitoring_items WHERE category=? ORDER BY metric', (cat,))
        else:
            rows = _query(get_db(), 'SELECT * FROM monitoring_items ORDER BY category, metric LIMIT 1000')
        return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})

    # Daily inputs
    if path == '/api/daily_inputs':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        if method == 'POST':
            if not csrf_valid(environ, sess):
                return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
            data = json.loads(read_body(environ) or b'{}')
            _exec(get_db(), 'INSERT OR REPLACE INTO daily_inputs(customer_id, item_id, date, value, status, comments, created_by, created_at) VALUES(?,?,?,?,?,?,?,?)',
                  (int(data['customer_id']), int(data['item_id']), data['date'], data.get('value'), data['status'], data.get('comments'), int(data.get('created_by') or 0), int(time.time())))
            audit_log(sess['user_id'], 'daily_input.save', json.dumps({'customer_id': int(data['customer_id']), 'item_id': int(data['item_id']), 'date': data['date']}))
            return json_response(start_response, {"ok": True})
        if method == 'GET':
            q = parse_qs(environ.get('QUERY_STRING') or '')
            customer_id = int(q.get('customer_id', ['0'])[0])
            date = q.get('date', [None])[0]
            sql = 'SELECT * FROM daily_inputs WHERE customer_id=?'
            params = [customer_id]
            if date:
                sql += ' AND date=?'
                params.append(date)
            rows = _query(get_db(), sql, tuple(params))
            return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})

    # Reports (simple HTML export; printing to PDF via browser supported by print CSS)
    if path == '/api/reports' and method == 'POST':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        if not csrf_valid(environ, sess):
            return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
        data = json.loads(read_body(environ) or b'{}')
        customer_id = int(data['customer_id'])
        period_type = data.get('period_type') or 'Daily'
        start_date = data.get('start_date')
        end_date = data.get('end_date')
        # Aggregate counts
        rows = _query(get_db(), 'SELECT status, COUNT(*) cnt FROM daily_inputs WHERE customer_id=? AND date BETWEEN ? AND ? GROUP BY status', (customer_id, start_date, end_date))
        counts = {r['status']: r['cnt'] for r in rows}
        ok = counts.get('OK', 0)
        warn = counts.get('Warning', 0)
        crit = counts.get('Critical', 0)
        total = ok + warn + crit
        # Build a tiny SVG pie-like bar
        chart_svg = f"""
        <svg width='320' height='24' xmlns='http://www.w3.org/2000/svg'>
          <rect x='0' y='0' width='{(320*ok/max(total,1)):.1f}' height='24' fill='#2ca02c'/>
          <rect x='{(320*ok/max(total,1)):.1f}' y='0' width='{(320*warn/max(total,1)):.1f}' height='24' fill='#ff7f0e'/>
          <rect x='{(320*(ok+warn)/max(total,1)):.1f}' y='0' width='{(320*crit/max(total,1)):.1f}' height='24' fill='#d62728'/>
        </svg>
        """
        # Render HTML
        html = f"""
        <html><head><meta charset='utf-8'><title>Report</title>
        <style>
        body {{ font-family: Arial, sans-serif; margin: 24px; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; font-size: 12px; }}
        th {{ background: #f6f8fa; text-align: left; }}
        .badge {{ display: inline-block; padding: 2px 6px; border-radius: 4px; color: #fff; font-size: 11px; }}
        .ok {{ background: #2ca02c; }} .warn {{ background: #ff7f0e; }} .crit {{ background: #d62728; }}
        </style></head>
        <body>
        <h2>SAP System Monitoring Report</h2>
        <div>Period: {start_date} to {end_date} ({period_type})</div>
        <div>Generated: {time.strftime('%Y-%m-%d %H:%M:%S')}</div>
        <h3>Status Distribution</h3>
        {chart_svg}
        <h3>Summary</h3>
        <table>
        <tr><th>Status</th><th>Count</th></tr>
        <tr><td>OK</td><td>{ok}</td></tr>
        <tr><td>Warning</td><td>{warn}</td></tr>
        <tr><td>Critical</td><td>{crit}</td></tr>
        <tr><th>Total</th><th>{total}</th></tr>
        </table>
        </body></html>
        """
        reports_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'data', 'reports'))
        os.makedirs(reports_dir, exist_ok=True)
        filename = f"report_{customer_id}_{int(time.time())}.html"
        file_path = os.path.join(reports_dir, filename)
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(html)
        _exec(get_db(), 'INSERT INTO reports(customer_id, period_type, start_date, end_date, file_path, generated_by, created_at) VALUES(?,?,?,?,?,?,?)',
              (customer_id, period_type, start_date, end_date, file_path, 0, int(time.time())))
        audit_log(sess['user_id'], 'report.create', json.dumps({'customer_id': customer_id, 'period_type': period_type, 'start': start_date, 'end': end_date, 'file': filename}))
        return json_response(start_response, {"ok": True, "file_path": file_path, "filename": filename, "url": f"/reports/{filename}"})

    # Analytics: trend of statuses over period grouped by date
    if path == '/api/analytics/status_trend' and method == 'GET':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        q = parse_qs(environ.get('QUERY_STRING') or '')
        customer_id = int(q.get('customer_id', ['0'])[0])
        start_date = q.get('start_date', [''])[0]
        end_date = q.get('end_date', [''])[0]
        rows = _query(get_db(), (
            'SELECT date, '
            'SUM(CASE WHEN status="OK" THEN 1 ELSE 0 END) AS ok_cnt, '
            'SUM(CASE WHEN status="Warning" THEN 1 ELSE 0 END) AS warn_cnt, '
            'SUM(CASE WHEN status="Critical" THEN 1 ELSE 0 END) AS crit_cnt '
            'FROM daily_inputs WHERE customer_id=? AND date BETWEEN ? AND ? '
            'GROUP BY date ORDER BY date'
        ), (customer_id, start_date, end_date))
        return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})

    # Notifications (simulated): list overdue inputs and recent criticals
    if path == '/api/notifications' and method == 'GET':
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        q = parse_qs(environ.get('QUERY_STRING') or '')
        cid = int(q.get('customer_id', ['0'])[0])
        today = time.strftime('%Y-%m-%d')
        now_h = int(time.strftime('%H'))
        cnt_today = _query(get_db(), 'SELECT COUNT(*) AS c FROM daily_inputs WHERE customer_id=? AND date=?', (cid, today))[0]['c']
        overdue = (cnt_today == 0 and now_h >= 10)
        recent_crit = _query(get_db(), 'SELECT COUNT(*) AS c FROM daily_inputs WHERE customer_id=? AND date>=date(?, "-3 day") AND status="Critical"', (cid, today))[0]['c'] > 0
        messages = []
        if overdue:
            messages.append({ 'type': 'overdue', 'severity': 'Warning', 'message': 'No daily inputs recorded by 10:00 local', 'date': today })
        if recent_crit:
            messages.append({ 'type': 'critical', 'severity': 'Critical', 'message': 'Recent Critical statuses detected in last 3 days', 'date': today })
        return json_response(start_response, {"ok": True, "messages": messages})

    if path == '/api/notifications/send' and method == 'POST':
        sess = require_auth(environ)
        if not sess or not csrf_valid(environ, sess):
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        data = json.loads(read_body(environ) or b'{}')
        cid = int(data.get('customer_id') or 0)
        # Recompute messages for safety
        today = time.strftime('%Y-%m-%d')
        now_h = int(time.strftime('%H'))
        cnt_today = _query(get_db(), 'SELECT COUNT(*) AS c FROM daily_inputs WHERE customer_id=? AND date=?', (cid, today))[0]['c']
        overdue = (cnt_today == 0 and now_h >= 10)
        recent_crit = _query(get_db(), 'SELECT COUNT(*) AS c FROM daily_inputs WHERE customer_id=? AND date>=date(?, "-3 day") AND status="Critical"', (cid, today))[0]['c'] > 0
        messages = []
        if overdue:
            messages.append('Overdue: No daily inputs recorded by 10:00')
        if recent_crit:
            messages.append('Alert: Recent Critical statuses detected in last 3 days')
        alerts_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'data', 'alerts.log'))
        os.makedirs(os.path.dirname(alerts_path), exist_ok=True)
        with open(alerts_path, 'a', encoding='utf-8') as f:
            for m in messages:
                f.write(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] customer={cid} user={sess['user_id']} {m}\n")
        audit_log(sess['user_id'], 'notification.send', json.dumps({'customer_id': cid, 'count': len(messages)}))
        return json_response(start_response, {"ok": True, "sent": len(messages)})

    # Admin: role-based simple endpoints
    if path == '/api/users' and method in ('GET','POST'):
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        user = get_user(sess['user_id'])
        if not user or user['role'] != 'admin':
            return json_response(start_response, {"ok": False, "error": "Forbidden"}, 403)
        if method == 'GET':
            rows = _query(get_db(), 'SELECT id, username, role, email FROM users ORDER BY username')
            return json_response(start_response, {"ok": True, "data": [dict(r) for r in rows]})
        if method == 'POST':
            if not csrf_valid(environ, sess):
                return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
            data = json.loads(read_body(environ) or b'{}')
            uid = create_user(data['username'], data['password'], data.get('role','user'), data.get('email'))
            audit_log(sess['user_id'], 'user.create', json.dumps({'user_id': uid}))
            return json_response(start_response, {"ok": True, "id": uid})

    if path.startswith('/api/users/') and method in ('PUT','DELETE'):
        sess = require_auth(environ)
        if not sess:
            return json_response(start_response, {"ok": False, "error": "Unauthorized"}, 401)
        user = get_user(sess['user_id'])
        if not user or user['role'] != 'admin':
            return json_response(start_response, {"ok": False, "error": "Forbidden"}, 403)
        _, _, uid_str = path.split('/')
        if not uid_str.isdigit():
            return json_response(start_response, {"ok": False, "error": "Invalid user id"}, 400)
        uid = int(uid_str)
        if method == 'PUT':
            if not csrf_valid(environ, sess):
                return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
            data = json.loads(read_body(environ) or b'{}')
            if 'password' in data and data['password']:
                set_user_password(uid, data['password'])
            if 'role' in data and data['role']:
                _exec(get_db(), 'UPDATE users SET role=? WHERE id=?', (data['role'], uid))
            audit_log(sess['user_id'], 'user.update', json.dumps({'user_id': uid}))
            return json_response(start_response, {"ok": True})
        if method == 'DELETE':
            if not csrf_valid(environ, sess):
                return json_response(start_response, {"ok": False, "error": "CSRF invalid"}, 403)
            _exec(get_db(), 'DELETE FROM users WHERE id=?', (uid,))
            audit_log(sess['user_id'], 'user.delete', json.dumps({'user_id': uid}))
            return json_response(start_response, {"ok": True})

    # Serve stored reports safely
    if path.startswith('/reports/') and method == 'GET':
        sess = require_auth(environ)
        if not sess:
            start_response('302 Found', [('Location', '/login')])
            return [b'']
        rel = path[len('/reports/'):]
        rel = os.path.basename(rel)  # prevent traversal
        reports_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'data', 'reports'))
        file_path = os.path.join(reports_dir, rel)
        return file_response(start_response, file_path, 'text/html; charset=utf-8')

    # Fallback 404
    start_response('404 Not Found', [('Content-Type', 'text/plain')])
    return [b'Not Found']


if __name__ == '__main__':
    ensure_schema_and_bootstrap()
    port = int(os.environ.get('PORT', '8000'))
    with make_server('', port, app) as httpd:
        print(f"Serving on port {port}...")
        httpd.serve_forever()
