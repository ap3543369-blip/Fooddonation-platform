// ---- theme (light/dark) ----
(function () {
  const t = localStorage.getItem('theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  document.documentElement.dataset.theme = t;
})();
function toggleTheme() {
  const n = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
  document.documentElement.dataset.theme = n; localStorage.setItem('theme', n);
}

// ---- helpers ----
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const toDate = (s) => new Date(/[zZ]|[+-]\d\d:\d\d$/.test(s) ? s : s + 'Z');
const fmt = (s) => toDate(s).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
function toast(msg, type) {
  let box = document.getElementById('toasts');
  if (!box) { box = document.createElement('div'); box.id = 'toasts'; document.body.appendChild(box); }
  const t = document.createElement('div'); t.className = 'toast' + (type === 'err' ? ' err' : ''); t.textContent = msg;
  box.appendChild(t); setTimeout(() => t.remove(), 5000);
}
function bindEyes() {
  document.querySelectorAll('.pw').forEach(w => {
    if (w.querySelector('.eye')) return;
    const b = document.createElement('button'); b.type = 'button'; b.className = 'eye'; b.textContent = '👁️';
    b.onclick = () => { const i = w.querySelector('input'); i.type = i.type === 'password' ? 'text' : 'password'; };
    w.appendChild(b);
  });
}

// ---- API ----
const API = {
  get token() { return localStorage.getItem('token'); },
  get user() { try { return JSON.parse(localStorage.getItem('user')); } catch { return null; } },
  save(token, user) { localStorage.setItem('token', token); localStorage.setItem('user', JSON.stringify(user)); },
  logout() { localStorage.removeItem('token'); localStorage.removeItem('user'); location.href = 'index.html'; },
  async call(method, url, body) {
    const isForm = body instanceof FormData;
    const headers = {};
    if (this.token) headers.Authorization = 'Bearer ' + this.token;
    if (!isForm) headers['Content-Type'] = 'application/json';
    const res = await fetch(url, { method, headers, body: body ? (isForm ? body : JSON.stringify(body)) : undefined });
    if (res.status === 401 && this.token) { this.logout(); return; }
    let data = null;
    try { data = await res.json(); } catch { }
    if (!res.ok) {
      let m = data && data.message;
      if (!m && data && data.errors) m = Object.values(data.errors).flat()[0];
      if (!m && data && data.title) m = data.title;
      throw new Error(m || 'Kuch galat ho gaya (' + res.status + ')');
    }
    return data;
  },
  get: (u) => API.call('GET', u),
  post: (u, b) => API.call('POST', u, b || {}),
  put: (u, b) => API.call('PUT', u, b || {}),
};
