import os
import sqlite3
import threading
import time
import hashlib
import secrets
from typing import Optional, Tuple, Dict, Any, List

_DB_PATH = os.path.join(os.path.dirname(__file__), '..', 'data', 'app.db')
_DB_PATH = os.path.abspath(_DB_PATH)
_DB_LOCK = threading.RLock()


def get_db() -> sqlite3.Connection:
    os.makedirs(os.path.dirname(_DB_PATH), exist_ok=True)
    conn = sqlite3.connect(_DB_PATH, check_same_thread=False)
    conn.row_factory = sqlite3.Row
    return conn


def _exec(conn: sqlite3.Connection, sql: str, params: Tuple = ()) -> None:
    with _DB_LOCK:
        conn.execute(sql, params)
        conn.commit()


def _query(conn: sqlite3.Connection, sql: str, params: Tuple = ()) -> List[sqlite3.Row]:
    with _DB_LOCK:
        cur = conn.execute(sql, params)
        rows = cur.fetchall()
        return rows


def init_db() -> None:
    conn = get_db()
    # Users and sessions
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT NOT NULL UNIQUE,
        password_hash BLOB NOT NULL,
        password_salt BLOB NOT NULL,
        role TEXT NOT NULL DEFAULT 'user',
        email TEXT,
        two_factor_secret TEXT,
        created_at INTEGER NOT NULL
    )
    """)

    _exec(conn, """
    CREATE TABLE IF NOT EXISTS sessions (
        id TEXT PRIMARY KEY,
        user_id INTEGER NOT NULL,
        csrf_token TEXT NOT NULL,
        created_at INTEGER NOT NULL,
        last_activity INTEGER NOT NULL,
        expires_at INTEGER NOT NULL,
        FOREIGN KEY(user_id) REFERENCES users(id)
    )
    """)

    # Customers
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS customers (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        name TEXT NOT NULL,
        customer_code TEXT NOT NULL UNIQUE,
        contact_details TEXT,
        system_sid TEXT,
        environment TEXT CHECK(environment IN ('Production','Development','QA','Sandbox')),
        created_at INTEGER NOT NULL,
        updated_at INTEGER NOT NULL
    )
    """)

    # Monitoring items (base catalog)
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS monitoring_items (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        category TEXT NOT NULL,
        metric TEXT NOT NULL,
        threshold TEXT,
        tool TEXT,
        steps TEXT
    )
    """)
    _exec(conn, "CREATE INDEX IF NOT EXISTS idx_monitoring_items_cat ON monitoring_items(category)")

    # Per-customer assignments of items
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS customer_assignments (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        customer_id INTEGER NOT NULL,
        item_id INTEGER NOT NULL,
        UNIQUE(customer_id, item_id),
        FOREIGN KEY(customer_id) REFERENCES customers(id) ON DELETE CASCADE,
        FOREIGN KEY(item_id) REFERENCES monitoring_items(id) ON DELETE CASCADE
    )
    """)

    # Daily inputs entered by users
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS daily_inputs (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        customer_id INTEGER NOT NULL,
        item_id INTEGER NOT NULL,
        date TEXT NOT NULL, -- YYYY-MM-DD
        value TEXT,
        status TEXT CHECK(status IN ('OK','Warning','Critical')) NOT NULL,
        comments TEXT,
        created_by INTEGER,
        created_at INTEGER NOT NULL,
        UNIQUE(customer_id, item_id, date),
        FOREIGN KEY(customer_id) REFERENCES customers(id) ON DELETE CASCADE,
        FOREIGN KEY(item_id) REFERENCES monitoring_items(id) ON DELETE CASCADE,
        FOREIGN KEY(created_by) REFERENCES users(id)
    )
    """)
    _exec(conn, "CREATE INDEX IF NOT EXISTS idx_daily_inputs_customer_date ON daily_inputs(customer_id, date)")

    # Generated reports
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS reports (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        customer_id INTEGER NOT NULL,
        period_type TEXT NOT NULL, -- Daily/Monthly/Quarterly/Yearly
        start_date TEXT NOT NULL,
        end_date TEXT NOT NULL,
        file_path TEXT, -- saved HTML/PDF path
        generated_by INTEGER,
        created_at INTEGER NOT NULL,
        FOREIGN KEY(customer_id) REFERENCES customers(id) ON DELETE CASCADE,
        FOREIGN KEY(generated_by) REFERENCES users(id)
    )
    """)

    # Audit logs
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS audit_logs (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER,
        action TEXT NOT NULL,
        details TEXT,
        created_at INTEGER NOT NULL,
        FOREIGN KEY(user_id) REFERENCES users(id)
    )
    """)

    # Settings/flags
    _exec(conn, """
    CREATE TABLE IF NOT EXISTS settings (
        key TEXT PRIMARY KEY,
        value TEXT
    )
    """)


def _get_setting(conn: sqlite3.Connection, key: str) -> Optional[str]:
    rows = _query(conn, "SELECT value FROM settings WHERE key=?", (key,))
    return rows[0][0] if rows else None


def _set_setting(conn: sqlite3.Connection, key: str, value: str) -> None:
    _exec(conn, "INSERT INTO settings(key, value) VALUES(?, ?) ON CONFLICT(key) DO UPDATE SET value=excluded.value", (key, value))


def _now() -> int:
    return int(time.time())


def _hash_password(password: str, salt: Optional[bytes] = None) -> Tuple[bytes, bytes]:
    if salt is None:
        salt = secrets.token_bytes(16)
    pw_hash = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt, 150_000)
    return pw_hash, salt


def create_admin_user_if_none(username: str = 'admin', password: str = 'admin123', email: str = 'admin@example.com') -> None:
    conn = get_db()
    users = _query(conn, "SELECT id FROM users LIMIT 1")
    if users:
        return
    pw_hash, salt = _hash_password(password)
    _exec(conn, "INSERT INTO users(username, password_hash, password_salt, role, email, created_at) VALUES(?,?,?,?,?,?)",
          (username, pw_hash, salt, 'admin', email, _now()))


def get_user(user_id: int) -> Optional[sqlite3.Row]:
    conn = get_db()
    rows = _query(conn, "SELECT id, username, role, email FROM users WHERE id=?", (user_id,))
    return rows[0] if rows else None


def verify_user_password(user_id: int, password: str) -> bool:
    conn = get_db()
    rows = _query(conn, "SELECT password_hash, password_salt FROM users WHERE id=?", (user_id,))
    if not rows:
        return False
    expected = rows[0]['password_hash']
    salt = rows[0]['password_salt']
    check_hash, _ = _hash_password(password, salt)
    return secrets.compare_digest(check_hash, expected)


def set_user_password(user_id: int, new_password: str) -> None:
    conn = get_db()
    pw_hash, salt = _hash_password(new_password)
    _exec(conn, "UPDATE users SET password_hash=?, password_salt=? WHERE id=?", (pw_hash, salt, user_id))


def create_user(username: str, password: str, role: str = 'user', email: Optional[str] = None) -> int:
    conn = get_db()
    pw_hash, salt = _hash_password(password)
    _exec(conn, "INSERT INTO users(username, password_hash, password_salt, role, email, created_at) VALUES(?,?,?,?,?,?)",
          (username, pw_hash, salt, role, email, _now()))
    row = _query(conn, "SELECT id FROM users WHERE username=?", (username,))
    return int(row[0]['id'])


def audit_log(user_id: Optional[int], action: str, details: Optional[str]) -> None:
    conn = get_db()
    _exec(conn, "INSERT INTO audit_logs(user_id, action, details, created_at) VALUES(?,?,?,?)",
          (user_id, action, details, _now()))


def verify_password(username: str, password: str) -> Optional[int]:
    conn = get_db()
    rows = _query(conn, "SELECT id, password_hash, password_salt FROM users WHERE username=?", (username,))
    if not rows:
        return None
    row = rows[0]
    expected = row['password_hash']
    salt = row['password_salt']
    check_hash, _ = _hash_password(password, salt)
    if secrets.compare_digest(check_hash, expected):
        return row['id']
    return None


def new_session(user_id: int, ttl_seconds: int = 1800) -> Dict[str, Any]:
    conn = get_db()
    sess_id = secrets.token_urlsafe(32)
    csrf = secrets.token_urlsafe(24)
    now = _now()
    exp = now + ttl_seconds
    _exec(conn, "INSERT INTO sessions(id, user_id, csrf_token, created_at, last_activity, expires_at) VALUES(?,?,?,?,?,?)",
          (sess_id, user_id, csrf, now, now, exp))
    return {"id": sess_id, "csrf": csrf, "expires_at": exp}


def get_session(session_id: str) -> Optional[sqlite3.Row]:
    conn = get_db()
    rows = _query(conn, "SELECT * FROM sessions WHERE id=?", (session_id,))
    if not rows:
        return None
    sess = rows[0]
    now = _now()
    if now >= sess['expires_at']:
        _exec(conn, "DELETE FROM sessions WHERE id=?", (session_id,))
        return None
    # sliding expiration: update last_activity and extend expiry
    new_expiry = now + 1800
    _exec(conn, "UPDATE sessions SET last_activity=?, expires_at=? WHERE id=?", (now, new_expiry, session_id))
    refreshed = _query(conn, "SELECT * FROM sessions WHERE id=?", (session_id,))
    return refreshed[0] if refreshed else None


def end_session(session_id: str) -> None:
    conn = get_db()
    _exec(conn, "DELETE FROM sessions WHERE id=?", (session_id,))


def seed_monitoring_items_from_text(raw_text_path: str) -> None:
    conn = get_db()
    exists = _get_setting(conn, 'monitoring_items_seeded')
    if exists == '1':
        return
    if not os.path.exists(raw_text_path):
        return
    with open(raw_text_path, 'r', encoding='utf-8') as f:
        lines = [ln.strip() for ln in f.readlines() if ln.strip()]
    inserted = 0
    for ln in lines:
        if ln.startswith('SAP System Daily Monitoring Report'):
            continue
        if ': ' not in ln:
            continue
        category, rest = ln.split(': ', 1)
        parts = rest.split(' - ')
        if len(parts) < 3:
            continue
        # heuristic: combine first 2 parts as metric if available
        if len(parts) >= 5:
            sub = parts[0].strip()
            metric_name = parts[1].strip()
            threshold = parts[2].strip()
            tool = parts[3].strip()
            steps = ' - '.join(p.strip() for p in parts[4:])
            metric = f"{sub}: {metric_name}"
        elif len(parts) == 4:
            sub = parts[0].strip()
            metric_name = parts[1].strip()
            threshold = parts[2].strip()
            tool_or_steps = parts[3].strip()
            metric = f"{sub}: {metric_name}"
            tool = ''
            steps = tool_or_steps
        else:  # len(parts) == 3
            sub = parts[0].strip()
            threshold = parts[1].strip()
            steps = parts[2].strip()
            metric = sub
            tool = ''
        _exec(conn, "INSERT INTO monitoring_items(category, metric, threshold, tool, steps) VALUES(?,?,?,?,?)",
              (category, metric, threshold, tool, steps))
        inserted += 1
    _set_setting(conn, 'monitoring_items_seeded', '1')
    print(f"Seeded monitoring_items: {inserted}")


def ensure_schema_and_bootstrap() -> None:
    init_db()
    create_admin_user_if_none()
    raw_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'data', 'monitoring_items_raw.txt'))
    seed_monitoring_items_from_text(raw_path)
