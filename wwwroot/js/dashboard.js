if (!API.token || !API.user) API.logout();
let me = API.user;
const $ = id => document.getElementById(id);
const app = $('app');

const CATS = ['Cooked Food', 'Packaged Food', 'Fruits & Vegetables', 'Bakery', 'Beverages', 'Other'];
const CAT_ICON = { 'Cooked Food': '🍛', 'Packaged Food': '📦', 'Fruits & Vegetables': '🥦', 'Bakery': '🥖', 'Beverages': '🥤', 'Other': '🍽️' };
const STEPS = ['Pending', 'Accepted', 'PickedUp', 'Delivered'];
const STATUSES = [['Pending', 'Pending'], ['Accepted', 'Accepted'], ['PickedUp', 'Picked Up'], ['Delivered', 'Delivered'], ['Cancelled', 'Cancelled']];

const NAV = {
  Donor: [['overview', '🏠', 'Overview'], ['new', '➕', 'Nayi Donation'], ['mine', '📦', 'Meri Donations'], ['profile', '👤', 'Profile']],
  Ngo: [['overview', '🏠', 'Overview'], ['avail', '🆕', 'Available'], ['mine', '✅', 'Accepted'], ['profile', '👤', 'Profile']],
  Admin: [['overview', '🏠', 'Overview'], ['ngos', '🏢', 'NGO Approvals'], ['all', '📊', 'All Donations'], ['profile', '👤', 'Profile']]
}[me.role];

const loaders = {}, lists = {}, F = {};
let currentTab = null;

// ---------- small helpers ----------
const initials = n => (n || '?').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
const roleLabel = r => r === 'Ngo' ? 'NGO' : r;
const badge = s => `<span class="badge b-${s}">${s === 'PickedUp' ? 'Picked Up' : s}</span>`;
const loading = () => app.innerHTML = '<div class="loading"><div class="spin"></div></div>';
function paintUser() {
  me = API.user;
  $('who').innerHTML = `<span class="avatar">${esc(initials(me.name))}</span><span class="uname">${esc(me.name)}<small>${roleLabel(me.role)}</small></span>`;
}
function tracker(status) {
  if (status === 'Cancelled') return '';
  const idx = STEPS.indexOf(status);
  return '<div class="track">' + STEPS.map((s, i) => `<div class="step ${i <= idx ? 'done' : ''}">${s === 'PickedUp' ? 'Picked Up' : s}</div>`).join('') + '</div>';
}
function timeLeft(iso) {
  const ms = toDate(iso) - Date.now();
  if (ms <= 0) return '<span class="warn">Expired</span>';
  const h = Math.floor(ms / 36e5), m = Math.floor(ms % 36e5 / 6e4);
  return `<span class="okc">${h >= 24 ? Math.floor(h / 24) + 'd ' + (h % 24) + 'h' : h + 'h ' + m + 'm'} left</span>`;
}
window.zoom = url => {
  const m = document.createElement('div'); m.className = 'modal'; m.innerHTML = `<img src="${esc(url)}" alt="">`;
  m.onclick = () => m.remove(); document.body.appendChild(m);
};
const qs = f => { const p = new URLSearchParams(); for (const [k, v] of Object.entries(f)) if (v !== '' && v != null) p.set(k, v); return p.toString(); };

// ---------- navigation ----------
function buildSide() {
  $('side').innerHTML = NAV.map(([k, ic, l]) => `<button class="nav-item" data-k="${k}"><span>${ic}</span>${l}</button>`).join('');
  $('side').querySelectorAll('.nav-item').forEach(b => b.onclick = () => show(b.dataset.k));
}
async function show(k, silent) {
  currentTab = k;
  $('side').querySelectorAll('.nav-item').forEach(b => b.classList.toggle('active', b.dataset.k === k));
  if (!silent) loading();
  try { await loaders[k](); } catch (e) { app.innerHTML = `<p class="msg err">${esc(e.message)}</p>`; }
}
window.go = k => show(k);
// realtime refresh: sirf list/overview tabs (forms ka typed data na mitey)
function refreshCurrent() {
  if (lists[currentTab]) lists[currentTab]();
  else if (currentTab === 'overview') show('overview', true);
}

// ---------- reusable filter bar + list + pager ----------
function filterBar(key, o) {
  const f = F[key] ||= { q: '', category: '', status: '', sort: 'newest', page: 1, pageSize: o.pageSize || 6 };
  return `<div class="card filters">
    <input class="fi-q" placeholder="🔍 ${o.placeholder}" value="${esc(f.q)}">
    ${o.cats ? `<select class="fi-cat"><option value="">Saari categories</option>${CATS.map(c => `<option ${f.category === c ? 'selected' : ''}>${c}</option>`).join('')}</select>` : ''}
    ${o.statuses ? `<select class="fi-status"><option value="">Saare status</option>${o.statuses.map(([v, l]) => `<option value="${v}" ${f.status === v ? 'selected' : ''}>${l}</option>`).join('')}</select>` : ''}
    ${o.sort ? `<select class="fi-sort"><option value="newest" ${f.sort === 'newest' ? 'selected' : ''}>Naye pehle</option><option value="expiry" ${f.sort === 'expiry' ? 'selected' : ''}>Jaldi expire hone wale pehle</option></select>` : ''}
  </div>`;
}
function bindFilters(key) {
  const f = F[key], reload = () => { f.page = 1; lists[key](); };
  const q = app.querySelector('.fi-q'); let t;
  q.oninput = () => { clearTimeout(t); t = setTimeout(() => { f.q = q.value; reload(); }, 350); };
  [['.fi-cat', 'category'], ['.fi-status', 'status'], ['.fi-sort', 'sort']].forEach(([sel, prop]) => {
    const el = app.querySelector(sel); if (el) el.onchange = () => { f[prop] = el.value; reload(); };
  });
}
window.gotoPage = (key, p) => { F[key].page = p; lists[key](); window.scrollTo({ top: 0, behavior: 'smooth' }); };
function renderPager(r, key) {
  $('pager').innerHTML = r.pages > 1 ? `<div class="pager">
    <button class="btn sm sec" ${r.page <= 1 ? 'disabled' : ''} onclick="gotoPage('${key}',${r.page - 1})">‹ Pehle</button>
    <span>Page ${r.page} / ${r.pages} · ${r.total} results</span>
    <button class="btn sm sec" ${r.page >= r.pages ? 'disabled' : ''} onclick="gotoPage('${key}',${r.page + 1})">Aage ›</button></div>`
    : (r.total ? `<div class="pager">${r.total} result${r.total > 1 ? 's' : ''}</div>` : '');
}
function listTab({ key, title, opts, url, render, empty, emptyIcon }) {
  loaders[key] = async () => {
    app.innerHTML = `<div class="page-head"><h2>${title}</h2></div>` + filterBar(key, opts) + '<div id="list"></div><div id="pager"></div>';
    lists[key] = async () => {
      let r;
      try { r = await API.get(url + '?' + qs(F[key])); }
      catch (e) { $('list').innerHTML = `<p class="msg err">${esc(e.message)}</p>`; return; }
      $('list').innerHTML = r.items.length ? render(r.items) : `<div class="empty"><span class="big">${emptyIcon || '📭'}</span>${empty}</div>`;
      renderPager(r, key);
    };
    bindFilters(key);
    await lists[key]();
  };
}

// ---------- donation card ----------
function donCard(d, { actions = '', who = '', showTime = false } = {}) {
  const icon = CAT_ICON[d.category] || '🍽️';
  const img = d.imageUrl ? `<img class="thumb" src="${esc(d.imageUrl)}" alt="" onclick="zoom('${esc(d.imageUrl)}')">` : `<div class="thumb ph">${icon}</div>`;
  return `<div class="card don">${img}
    <div>
      <div class="don-head"><h3>${esc(d.foodItem)}</h3>${badge(d.status)}</div>
      <div class="chips"><span class="chip">${icon} ${esc(d.category)}</span><span class="chip">⚖️ ${esc(d.quantity)}</span>${showTime ? `<span class="chip">⏰ ${timeLeft(d.expiryTime)}</span>` : ''}</div>
      <p class="meta">📍 ${esc(d.pickupAddress)}</p>
      <p class="meta">🕒 Expiry: ${fmt(d.expiryTime)}</p>
      ${who}${d.description ? `<p class="meta">📝 ${esc(d.description)}</p>` : ''}
      ${tracker(d.status)}
    </div>
    <div class="don-actions">${actions}</div></div>`;
}

// ================= OVERVIEW =================
loaders.overview = async () => {
  const s = await API.get('/api/profile/summary');
  const hi = { Donor: 'Aaj kisi ki bhookh mitane me madad karein.', Ngo: 'Naye donations aapka intezaar kar rahe hain.', Admin: 'Platform ka poora overview yahan hai.' }[me.role];
  const cta = { Donor: ['new', '➕ Nayi Donation'], Ngo: ['avail', '🆕 Available Donations'], Admin: ['ngos', '🏢 NGO Approvals'] }[me.role];
  app.innerHTML = `<div class="banner"><div><h2>Namaste, ${esc(me.name.split(' ')[0])} 👋</h2><p>${hi}</p></div>
    <button class="btn" onclick="go('${cta[0]}')">${cta[1]}</button></div>
    <div class="stats-grid">${s.cards.map(c => `<div class="stat-card"><div class="ic">${c.icon}</div><div><b>${c.value}</b><span>${esc(c.label)}</span></div></div>`).join('')}</div>`;
};

// ================= DONOR =================
loaders.new = async () => {
  const d = new Date(Date.now() + 6 * 3600e3); d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  app.innerHTML = `<div class="page-head"><h2>➕ Nayi Food Donation</h2></div><div class="card"><form id="f">
    <div class="row"><div><label>Food Item *</label><input id="fi" required placeholder="Jaise: Dal chawal"></div>
    <div><label>Quantity *</label><input id="fq" required placeholder="Jaise: 20 plates / 5 kg"></div></div>
    <div class="row"><div><label>Category</label><select id="fc">${CATS.map(c => `<option>${c}</option>`).join('')}</select></div>
    <div><label>Khana kab tak safe hai? *</label><input type="datetime-local" id="fe" required value="${d.toISOString().slice(0, 16)}"></div></div>
    <label>Pickup Address *</label><input id="fa" required value="${esc((me.address || ''))}">
    <label>Description (optional)</label><textarea id="fd" rows="3" placeholder="Khane ke baare me kuch aur batayein"></textarea>
    <label>📸 Khane ki photo (optional, max 3 MB)</label><input type="file" id="fimg" accept="image/png,image/jpeg,image/webp">
    <img id="prev" class="preview" alt="">
    <p class="msg" id="m"></p><button class="btn" id="sb">🚀 Donation Request Bhejein</button></form></div>`;
  $('fimg').onchange = () => {
    const f = $('fimg').files[0], p = $('prev');
    if (f && f.size > 3 * 1024 * 1024) { toast('Image 3 MB se chhoti honi chahiye', 'err'); $('fimg').value = ''; p.style.display = 'none'; return; }
    if (f) { p.src = URL.createObjectURL(f); p.style.display = 'block'; } else p.style.display = 'none';
  };
  $('f').onsubmit = async e => {
    e.preventDefault(); $('sb').disabled = true;
    const fd = new FormData();
    fd.append('foodItem', $('fi').value); fd.append('quantity', $('fq').value); fd.append('category', $('fc').value);
    fd.append('pickupAddress', $('fa').value); fd.append('description', $('fd').value);
    fd.append('expiryTime', new Date($('fe').value).toISOString());
    if ($('fimg').files[0]) fd.append('image', $('fimg').files[0]);
    try { const r = await API.call('POST', '/api/donations', fd); toast('✅ ' + r.message); show('mine'); }
    catch (err) { $('m').className = 'msg err'; $('m').textContent = err.message; $('sb').disabled = false; }
  };
};
listTab({
  key: 'mine', title: '📦 Meri Donations', url: '/api/donations/mine', emptyIcon: '📦', empty: 'Koi donation nahi mili.',
  opts: { placeholder: 'Food ya address search karein', cats: true, statuses: STATUSES },
  render: items => items.map(d => donCard(d, {
    showTime: d.status === 'Pending',
    who: d.ngoName ? `<p class="meta">🤝 NGO: <b>${esc(d.ngoName)}</b>${d.ngoPhone ? ' · ' + esc(d.ngoPhone) : ''}</p>` : '',
    actions: d.status === 'Pending' ? `<button class="btn red sm" onclick="cancelD(${d.id})">Cancel</button>` : ''
  })).join('')
});
window.cancelD = async id => {
  if (!confirm('Is donation ko cancel karein?')) return;
  try { toast((await API.post(`/api/donations/${id}/cancel`)).message); refreshCurrent(); } catch (e) { toast(e.message, 'err'); }
};

// ================= NGO =================
// NGO ke 'mine' (accepted) aur donor ke 'mine' alag-alag role me hain, isliye same key safe hai
if (me.role === 'Ngo') {
  listTab({
    key: 'avail', title: '🆕 Available Donations', url: '/api/donations/available', emptyIcon: '🎉', empty: 'Abhi koi nayi donation available nahi hai.',
    opts: { placeholder: 'Food ya address search karein', cats: true, sort: true },
    render: items => items.map(d => donCard(d, {
      showTime: true,
      who: `<p class="meta">👤 Donor: <b>${esc(d.donorName)}</b>${d.donorPhone ? ' · ' + esc(d.donorPhone) : ''}</p>`,
      actions: `<button class="btn" onclick="acceptD(${d.id})">✅ Accept</button>`
    })).join('')
  });
  listTab({
    key: 'mine', title: '✅ Meri Accepted Donations', url: '/api/donations/ngo/mine', emptyIcon: '🤝', empty: 'Aapne abhi koi donation accept nahi ki.',
    opts: { placeholder: 'Food ya address search karein', cats: true, statuses: STATUSES.filter(s => s[0] !== 'Pending' && s[0] !== 'Cancelled') },
    render: items => items.map(d => {
      const next = d.status === 'Accepted' ? ['PickedUp', '🚚 Mark Picked Up'] : d.status === 'PickedUp' ? ['Delivered', '📬 Mark Delivered'] : null;
      return donCard(d, {
        who: `<p class="meta">👤 Donor: <b>${esc(d.donorName)}</b>${d.donorPhone ? ' · ' + esc(d.donorPhone) : ''}</p>`,
        actions: next ? `<button class="btn blue sm" onclick="setS(${d.id},'${next[0]}')">${next[1]}</button>` : ''
      });
    }).join('')
  });
}
window.acceptD = async id => { try { toast('✅ ' + (await API.post(`/api/donations/${id}/accept`)).message); } catch (e) { toast(e.message, 'err'); } refreshCurrent(); };
window.setS = async (id, status) => { try { toast((await API.post(`/api/donations/${id}/status`, { status })).message); refreshCurrent(); } catch (e) { toast(e.message, 'err'); } };

// ================= ADMIN =================
if (me.role === 'Admin') {
  listTab({
    key: 'ngos', title: '🏢 NGO Approvals', url: '/api/admin/ngos', emptyIcon: '🏢', empty: 'Koi NGO nahi mila.',
    opts: { placeholder: 'NGO ka naam ya email search karein', statuses: [['pending', 'Pending'], ['approved', 'Approved']], pageSize: 8 },
    render: items => `<div class="card table-wrap"><table><thead><tr><th>NGO</th><th>Contact</th><th>Reg. No</th><th>Status</th><th></th></tr></thead><tbody>` +
      items.map(n => `<tr><td><b>${esc(n.orgName)}</b><br><small class="meta">${esc(n.name)}</small></td>
        <td>${esc(n.email)}<br><small class="meta">${esc(n.phone || '')}</small></td><td>${esc(n.registrationNo || '-')}</td>
        <td>${n.isApproved ? '<span class="badge b-Approved">Approved</span>' : '<span class="badge b-Pending">Pending</span>'}</td>
        <td>${n.isApproved ? `<button class="btn red sm" onclick="adm(${n.id},'reject')">Revoke</button>` : `<button class="btn sm" onclick="adm(${n.id},'approve')">Approve</button>`}</td></tr>`).join('') + '</tbody></table></div>'
  });
  listTab({
    key: 'all', title: '📊 Saari Donations', url: '/api/admin/donations', emptyIcon: '📊', empty: 'Koi donation nahi mili.',
    opts: { placeholder: 'Food, donor ya NGO search karein', cats: true, statuses: STATUSES, pageSize: 10 },
    render: items => `<div class="card table-wrap"><table><thead><tr><th>#</th><th>Food</th><th>Donor</th><th>NGO</th><th>Status</th><th>Date</th></tr></thead><tbody>` +
      items.map(d => `<tr><td>${d.id}</td><td>${CAT_ICON[d.category] || '🍽️'} ${esc(d.foodItem)}<br><small class="meta">${esc(d.quantity)}</small></td><td>${esc(d.donorName)}</td><td>${esc(d.ngoName || '-')}</td><td>${badge(d.status)}</td><td>${fmt(d.createdAt)}</td></tr>`).join('') + '</tbody></table></div>'
  });
}
window.adm = async (id, act) => {
  if (act === 'reject' && !confirm('Is NGO ki approval hata dein?')) return;
  try { toast((await API.post(`/api/admin/ngos/${id}/${act}`)).message); refreshCurrent(); } catch (e) { toast(e.message, 'err'); }
};

// ================= PROFILE =================
loaders.profile = async () => {
  const p = await API.get('/api/profile');
  app.innerHTML = `<div class="page-head"><h2>👤 Profile</h2></div><div class="grid2">
    <div class="card"><h3>Meri Details</h3><form id="pf">
      <label>Email (change nahi ho sakta)</label><input value="${esc(p.email)}" disabled>
      <label>Full Name</label><input id="pn" required value="${esc(p.name)}">
      ${p.role === 'Ngo' ? `<label>NGO Name</label><input id="po" value="${esc(p.orgName || '')}">` : ''}
      <div class="row"><div><label>Phone</label><input id="pp" value="${esc(p.phone || '')}"></div><div><label>Role</label><input value="${roleLabel(p.role)}" disabled></div></div>
      <label>Address</label><input id="pa" value="${esc(p.address || '')}">
      <p class="meta" style="margin-top:12px">Member since ${fmt(p.createdAt)}</p>
      <p class="msg" id="pm"></p><button class="btn">💾 Save Karein</button></form></div>
    <div class="card"><h3>🔒 Password Badlein</h3><form id="pw">
      <label>Current Password</label><div class="pw"><input type="password" id="c1" required></div>
      <label>Naya Password (min 6)</label><div class="pw"><input type="password" id="c2" minlength="6" required></div>
      <label>Naya Password dobara</label><div class="pw"><input type="password" id="c3" minlength="6" required></div>
      <p class="msg" id="wm"></p><button class="btn">🔑 Password Badlein</button></form></div></div>`;
  bindEyes();
  $('pf').onsubmit = async e => {
    e.preventDefault();
    try {
      const r = await API.put('/api/profile', { name: $('pn').value, phone: $('pp').value, address: $('pa').value, orgName: $('po')?.value });
      API.save(API.token, r.user); paintUser(); toast('✅ ' + r.message);
      $('pm').className = 'msg ok'; $('pm').textContent = r.message;
    } catch (err) { $('pm').className = 'msg err'; $('pm').textContent = err.message; }
  };
  $('pw').onsubmit = async e => {
    e.preventDefault();
    if ($('c2').value !== $('c3').value) { $('wm').className = 'msg err'; $('wm').textContent = 'Dono naye password same hone chahiye.'; return; }
    try {
      const r = await API.post('/api/profile/change-password', { currentPassword: $('c1').value, newPassword: $('c2').value });
      toast('✅ ' + r.message); $('wm').className = 'msg ok'; $('wm').textContent = r.message; e.target.reset();
    } catch (err) { $('wm').className = 'msg err'; $('wm').textContent = err.message; }
  };
};

// ================= NOTIFICATIONS (SignalR) =================
async function loadNotifs() {
  try {
    const list = await API.get('/api/notifications');
    const unread = list.filter(n => !n.isRead).length;
    $('dot').style.display = unread ? '' : 'none'; $('dot').textContent = unread;
    $('panel').innerHTML = `<div class="ph">Notifications</div>` + (list.length
      ? list.map(n => `<div class="n ${n.isRead ? '' : 'new'}">${esc(n.message)}<small>${fmt(n.createdAt)}</small></div>`).join('')
      : '<div class="n">Abhi koi notification nahi hai.</div>');
  } catch { }
}
$('bell').onclick = async e => {
  e.stopPropagation(); $('panel').classList.toggle('open');
  if ($('panel').classList.contains('open')) { await API.post('/api/notifications/read-all'); setTimeout(loadNotifs, 2500); }
};
document.addEventListener('click', e => { if (!$('panel').contains(e.target)) $('panel').classList.remove('open'); });

const conn = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/notifications', { accessTokenFactory: () => API.token })
  .withAutomaticReconnect().build();
conn.on('notification', msg => { toast('🔔 ' + msg); loadNotifs(); refreshCurrent(); });
conn.start().catch(e => console.warn('SignalR connect failed', e));

// ================= START =================
paintUser(); buildSide(); loadNotifs(); show('overview');
