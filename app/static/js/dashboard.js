(function(){
  const csrf = (document.cookie.match(/(?:^|; )csrf=([^;]+)/)||[])[1] || '';
  const customerSelect = document.getElementById('customerSelect');
  const catalogEl = document.getElementById('catalog');
  const assignedEl = document.getElementById('assigned');
  const inputsEl = document.getElementById('inputs');

  document.getElementById('logoutBtn').onclick = async () => {
    await fetch('/api/logout', { method: 'POST', headers: {'X-CSRF': csrf} });
    window.location.href = '/login';
  };

  // Customers
  async function loadCustomers(){
    const r = await fetch('/api/customers');
    if(!r.ok){ if(r.status===401){ location.href='/login'; } return; }
    const j = await r.json();
    customerSelect.innerHTML = '';
    j.data.forEach(c => {
      const opt = document.createElement('option'); opt.value = c.id; opt.textContent = `${c.name} (${c.customer_code})`; customerSelect.appendChild(opt);
    });
    if(j.data.length) { loadAssignments(); }
  }

  document.getElementById('newCustomerBtn').onclick = () => {
    document.getElementById('newCustomerForm').style.display='block';
  };
  document.getElementById('editCustomerBtn').onclick = async () => {
    const cid = customerSelect.value; if(!cid) return;
    // naive: ask current values via list fetch
    const r = await fetch('/api/customers'); const j = await r.json();
    const c = j.data.find(x => String(x.id)===String(cid)); if(!c) return;
    document.getElementById('c_name').value = c.name||'';
    document.getElementById('c_code').value = c.customer_code||'';
    document.getElementById('c_contact').value = c.contact_details||'';
    document.getElementById('c_sid').value = c.system_sid||'';
    document.getElementById('c_env').value = c.environment||'Production';
    document.getElementById('newCustomerForm').style.display='block';
    document.getElementById('saveCustomerBtn').dataset.mode = 'edit';
  };
  document.getElementById('deleteCustomerBtn').onclick = async () => {
    const cid = customerSelect.value; if(!cid) return;
    if(!confirm('Delete this customer?')) return;
    await fetch(`/api/customers/${cid}`, { method:'DELETE', headers:{'X-CSRF':csrf} });
    loadCustomers();
  };
  document.getElementById('saveCustomerBtn').onclick = async () => {
    const payload = {
      name: document.getElementById('c_name').value,
      customer_code: document.getElementById('c_code').value,
      contact_details: document.getElementById('c_contact').value,
      system_sid: document.getElementById('c_sid').value,
      environment: document.getElementById('c_env').value,
    };
    const mode = document.getElementById('saveCustomerBtn').dataset.mode || 'create';
    if(mode==='edit'){
      const cid = customerSelect.value; if(!cid) return;
      await fetch(`/api/customers/${cid}`, { method:'PUT', headers: {'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify(payload) });
      delete document.getElementById('saveCustomerBtn').dataset.mode;
    } else {
      await fetch('/api/customers', { method:'POST', headers: {'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify(payload) });
    }
    document.getElementById('newCustomerForm').style.display='none';
    loadCustomers();
  };

  customerSelect.onchange = () => loadAssignments();

  // Catalog
  async function loadCatalog(){
    const term = (document.getElementById('searchInput').value||'').toLowerCase();
    const r = await fetch('/api/items');
    if(!r.ok){ return; }
    const j = await r.json();
    let data = j.data;
    if(term){
      data = data.filter(x => `${x.category} ${x.metric} ${x.threshold||''} ${x.tool||''}`.toLowerCase().includes(term));
    }
    renderItems(catalogEl, data, false);
  }

  async function loadAssignments(){
    const cid = customerSelect.value; if(!cid) return;
    const r = await fetch(`/api/customers/${cid}/assignments`);
    if(!r.ok){ return; }
    const j = await r.json();
    renderItems(assignedEl, j.data, true);
  }

  function renderItems(container, items, assigned){
    container.innerHTML = '';
    items.forEach(it => {
      const el = document.createElement('div'); el.className = 'card'; el.draggable = true; el.dataset.itemId = it.id;
      el.innerHTML = `
        <div style="display:flex;justify-content:space-between;align-items:center;gap:8px">
          <div style="font-weight:600">${it.metric}</div>
          ${assigned ? `<button data-remove="1" title="Remove">✕</button>` : ''}
        </div>
        <div style="font-size:12px;color:#333;margin-top:6px">${it.category}</div>
        ${it.threshold ? `<div class="badge warn" title="threshold">${it.threshold}</div>` : ''}
        ${it.tool ? `<div style="font-size:12px;color:#555">Tool: ${it.tool}</div>` : ''}
      `;
      if(assigned){
        el.querySelector('button[data-remove]')?.addEventListener('click', async (ev)=>{
          ev.stopPropagation();
          const cid = customerSelect.value;
          await fetch(`/api/customers/${cid}/assignments`, { method:'DELETE', headers:{'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify({ item_id: it.id }) });
          loadAssignments();
        });
      }
      el.addEventListener('dragstart', ev => {
        ev.dataTransfer.setData('text/plain', JSON.stringify(it));
      });
      container.appendChild(el);
    });
  }

  // Drop to assign
  assignedEl.addEventListener('dragover', ev => ev.preventDefault());
  assignedEl.addEventListener('drop', async (ev) => {
    ev.preventDefault();
    const cid = customerSelect.value; if(!cid) return;
    try { const it = JSON.parse(ev.dataTransfer.getData('text/plain')); 
      await fetch(`/api/customers/${cid}/assignments`, { method:'POST', headers:{'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify({ item_id: it.id }) });
      loadAssignments();
    } catch(e){}
  });

  // Daily inputs UI: build from assigned list
  document.getElementById('loadInputsBtn').onclick = async () => {
    const cid = customerSelect.value; if(!cid) return;
    const date = document.getElementById('dateInput').value || new Date().toISOString().slice(0,10);
    const r = await fetch(`/api/customers/${cid}/assignments`); const j = await r.json();
    const existing = await (await fetch(`/api/daily_inputs?customer_id=${cid}&date=${date}`)).json();
    const inputMap = new Map(existing.data.map(x => [x.item_id, x]));
    inputsEl.innerHTML = '';
    j.data.forEach(it => {
      const row = document.createElement('div'); row.className='card';
      const prev = inputMap.get(it.id);
      row.innerHTML = `
        <div style="font-weight:600">${it.metric}</div>
        <div style="font-size:12px;color:#555">${it.threshold || ''}</div>
        <div class="grid" style="margin-top:6px">
          <label>Value <input data-x="value" value="${prev?.value || ''}"></label>
          <label>Status
            <select data-x="status">
              <option ${prev?.status==='OK'?'selected':''}>OK</option>
              <option ${prev?.status==='Warning'?'selected':''}>Warning</option>
              <option ${prev?.status==='Critical'?'selected':''}>Critical</option>
            </select>
          </label>
        </div>
        <label>Comments
          <textarea data-x="comments">${prev?.comments || ''}</textarea>
        </label>
        <div style="display:flex;justify-content:flex-end"><button data-save="1">Save</button></div>
      `;
      row.querySelector('button[data-save]').onclick = async () => {
        const payload = {
          customer_id: parseInt(cid,10),
          item_id: it.id,
          date,
          value: row.querySelector('[data-x="value"]').value,
          status: row.querySelector('[data-x="status"]').value,
          comments: row.querySelector('[data-x="comments"]').value,
        };
        await fetch('/api/daily_inputs', { method:'POST', headers:{'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify(payload) });
        row.style.outline = '2px solid #2ca02c'; setTimeout(()=> row.style.outline='none', 600);
      };
      inputsEl.appendChild(row);
    });
  };

  // Report generation
  document.getElementById('genReportBtn').onclick = async () => {
    const cid = customerSelect.value; if(!cid) return;
    const payload = {
      customer_id: parseInt(cid,10),
      period_type: document.getElementById('periodType').value,
      start_date: document.getElementById('startDate').value,
      end_date: document.getElementById('endDate').value,
    };
    const r = await fetch('/api/reports', { method:'POST', headers:{'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify(payload) });
    const j = await r.json();
    if(j.ok){
      document.getElementById('reportLink').innerHTML = `<a href="${j.url}" target="_blank">Open Report</a>`;
    }
  };

  // Inline trend chart (SVG) for selected period
  async function loadTrend(){
    const cid = customerSelect.value; if(!cid) return;
    const start = document.getElementById('startDate').value;
    const end = document.getElementById('endDate').value;
    const r = await fetch(`/api/analytics/status_trend?customer_id=${cid}&start_date=${start}&end_date=${end}`);
    if(!r.ok) return;
    const j = await r.json();
    const data = j.data||[];
    const maxY = Math.max(1, ...data.map(d => (d.ok_cnt+d.warn_cnt+d.crit_cnt)));
    const W=440,H=140,P=28; // chart size and padding
    const step = data.length>1 ? (W-P*2)/(data.length-1) : 0;
    function pathFor(key,color){
      let p='';
      data.forEach((d,i)=>{
        const x = P + i*step; const y = H-P - (d[key]/maxY)*(H-P*2);
        p += (i? ' L':'M')+x+','+y;
      });
      return `<path d="${p}" fill="none" stroke="${color}" stroke-width="2"/>`;
    }
    const dates = data.map(d=>d.date);
    const svg = `
      <svg width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg" style="background:#fff;border:1px solid #eee;border-radius:6px">
        <g stroke="#ddd">
          <line x1="${P}" y1="${H-P}" x2="${W-P}" y2="${H-P}"/>
          <line x1="${P}" y1="${P}" x2="${P}" y2="${H-P}"/>
        </g>
        ${pathFor('ok_cnt','#2ca02c')}
        ${pathFor('warn_cnt','#ff7f0e')}
        ${pathFor('crit_cnt','#d62728')}
      </svg>
      <div style="font-size:12px;color:#555;margin-top:4px">OK (green), Warning (orange), Critical (red)</div>
    `;
    document.getElementById('reportLink').innerHTML = svg;
  }
  document.getElementById('startDate').addEventListener('change', loadTrend);
  document.getElementById('endDate').addEventListener('change', loadTrend);

  // Notifications
  async function loadAlerts(){
    const cid = customerSelect.value; if(!cid) return;
    const r = await fetch(`/api/notifications?customer_id=${cid}`);
    if(!r.ok) return;
    const j = await r.json();
    const wrap = document.getElementById('alerts');
    wrap.innerHTML = '';
    (j.messages||[]).forEach(m => {
      const el = document.createElement('div'); el.className = 'card';
      const color = m.severity==='Critical' ? 'crit' : (m.severity==='Warning' ? 'warn' : '');
      el.innerHTML = `<div><span class="badge ${color}">${m.severity||'Info'}</span> ${m.message}</div>`;
      wrap.appendChild(el);
    });
  }
  document.getElementById('checkAlertsBtn').onclick = loadAlerts;
  document.getElementById('sendAlertsBtn').onclick = async () => {
    const cid = customerSelect.value; if(!cid) return;
    await fetch('/api/notifications/send', { method:'POST', headers:{'Content-Type':'application/json','X-CSRF':csrf}, body: JSON.stringify({ customer_id: parseInt(cid,10) }) });
    loadAlerts();
  };

  // Defaults
  const today = new Date().toISOString().slice(0,10);
  document.getElementById('dateInput').value = today;
  document.getElementById('startDate').value = today;
  document.getElementById('endDate').value = today;

  // Overdue notice simulation: if today and time > 10:00 local and no inputs
  async function checkOverdue(){
    const cid = customerSelect.value; if(!cid) return;
    const now = new Date();
    const date = document.getElementById('dateInput').value || now.toISOString().slice(0,10);
    if(date !== now.toISOString().slice(0,10)) return;
    if(now.getHours() < 10) return;
    const existing = await (await fetch(`/api/daily_inputs?customer_id=${cid}&date=${date}`)).json();
    const notice = document.getElementById('overdueNotice');
    if((existing.data||[]).length === 0){
      notice.textContent = 'Overdue: No daily inputs recorded by 10:00';
      notice.style.display='block';
    } else {
      notice.style.display='none';
    }
  }
  setInterval(checkOverdue, 60000);

  loadCustomers();
  loadCatalog();
  document.getElementById('searchInput').addEventListener('input', loadCatalog);
})();
