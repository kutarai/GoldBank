// API client for the bank-teller front-end.
// All endpoints are under /api/teller — JWT auth required except /auth/login.

const API_BASE = 'http://localhost:5001/api/teller';

const TOKEN_KEY = 'goldbank_teller_token';
const USER_KEY = 'goldbank_teller_user';

export function getToken() {
  return sessionStorage.getItem(TOKEN_KEY);
}
export function getUser() {
  const raw = sessionStorage.getItem(USER_KEY);
  return raw ? JSON.parse(raw) : null;
}
export function setSession(token, user) {
  sessionStorage.setItem(TOKEN_KEY, token);
  sessionStorage.setItem(USER_KEY, JSON.stringify(user));
}
export function clearSession() {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(USER_KEY);
}

async function call(path, opts = {}) {
  const headers = {
    'Content-Type': 'application/json',
    ...(opts.headers || {}),
  };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...opts, headers });
  if (res.status === 401 && !path.startsWith('/auth/')) {
    clearSession();
    if (window.location.pathname !== '/login') window.location.href = '/login';
    throw new Error('unauthorized');
  }
  const text = await res.text();
  let body = null;
  try { body = text ? JSON.parse(text) : null; } catch { body = text; }
  if (!res.ok) {
    const err = new Error((body && body.error) || `HTTP ${res.status}`);
    err.status = res.status;
    err.body = body;
    throw err;
  }
  return body;
}

// ─── Auth ──────────────────────────────────────────────────────────────
export async function login(username, password) {
  const data = await call('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
  setSession(data.accessToken, data.user);
  return data.user;
}
export function logout() { clearSession(); }

// ─── Customers ─────────────────────────────────────────────────────────
export async function searchCustomers(q) {
  if (!q || !q.trim()) return [];
  return call(`/customers/search?q=${encodeURIComponent(q)}`);
}
export async function getCustomerCard(accountId) {
  return call(`/customers/${encodeURIComponent(accountId)}/card`);
}
export async function getCustomerTransactions(accountId, from, to) {
  const params = new URLSearchParams();
  if (from) params.set('from', from);
  if (to)   params.set('to', to);
  const qs = params.toString() ? `?${params.toString()}` : '';
  return call(`/customers/${encodeURIComponent(accountId)}/transactions${qs}`);
}

// ─── Drawer ────────────────────────────────────────────────────────────
export async function getCurrentDrawer() {
  try { return await call('/drawer/current'); }
  catch (e) { if (e.status === 404) return null; throw e; }
}
export async function openDrawer({ branchId, openingFloatJson }) {
  return call('/drawer/open', {
    method: 'POST',
    body: JSON.stringify({ branchId, openingFloatJson }),
  });
}
export async function closeDrawer({ drawerId, closingBalanceJson, confirmVariance = false }) {
  return call('/drawer/close', {
    method: 'POST',
    body: JSON.stringify({ drawerId, closingBalanceJson, confirmVariance }),
  });
}

// ─── Cash transactions ─────────────────────────────────────────────────
export async function createDeposit(payload) {
  return call('/deposits', { method: 'POST', body: JSON.stringify(payload) });
}
export async function createWithdrawal(payload) {
  return call('/withdrawals', { method: 'POST', body: JSON.stringify(payload) });
}
export async function approveWithdrawal(pendingId, supervisorUsername, supervisorPin) {
  return call(`/withdrawals/${pendingId}/approve`, {
    method: 'POST',
    body: JSON.stringify({ supervisorUsername, supervisorPin }),
  });
}
export async function reverseTransaction(cashTxnId, { reason, supervisorUsername, supervisorPin }) {
  return call(`/transactions/${cashTxnId}/reverse`, {
    method: 'POST',
    body: JSON.stringify({ reason, supervisorUsername, supervisorPin }),
  });
}
export async function listTransactions(date) {
  const q = date ? `?date=${date}` : '';
  return call(`/transactions${q}`);
}
export async function getBranchDashboard(branchId) {
  return call(`/branches/${encodeURIComponent(branchId)}/dashboard`);
}

// ─── Vault (STORY-166/167/168) ──────────────────────────────────────
export async function listVaults(branchId) {
  const q = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
  return call(`/vaults${q}`);
}
export async function getVault(vaultId) {
  return call(`/vaults/${encodeURIComponent(vaultId)}`);
}
export async function postVaultMovement(vaultId, payload) {
  return call(`/vaults/${encodeURIComponent(vaultId)}/movements`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}
export async function postVaultSpotCheck(vaultId, payload) {
  return call(`/vaults/${encodeURIComponent(vaultId)}/spot-checks`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}
export async function listVaultMovements(vaultId, take = 50) {
  return call(`/vaults/${encodeURIComponent(vaultId)}/movements?take=${take}`);
}
export async function openVaultEodReport(vaultId, date) {
  const q = date ? `?date=${date}` : '';
  const res = await fetch(
    `${API_BASE}/vaults/${encodeURIComponent(vaultId)}/eod-report.pdf${q}`,
    { headers: { Authorization: `Bearer ${getToken()}` } },
  );
  if (!res.ok) throw new Error(`vault report fetch failed (${res.status})`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank');
}

// Fetches the receipt PDF as a blob (with auth headers) and returns an
// object URL that can be passed to window.open() in a new tab.
// The caller should URL.revokeObjectURL(url) when done.
export async function fetchReceiptPdfUrl(cashTxnId) {
  const res = await fetch(
    `${API_BASE}/transactions/${encodeURIComponent(cashTxnId)}/receipt.pdf`,
    { headers: { Authorization: `Bearer ${getToken()}` } },
  );
  if (!res.ok) throw new Error(`receipt fetch failed (${res.status})`);
  const blob = await res.blob();
  return URL.createObjectURL(blob);
}

export async function openReceipt(cashTxnId) {
  const url = await fetchReceiptPdfUrl(cashTxnId);
  window.open(url, '_blank', 'noopener');
}

// EOD teller report PDF for a drawer session (STORY-159)
export async function fetchEodReportUrl(drawerId) {
  const res = await fetch(
    `${API_BASE}/drawer/${encodeURIComponent(drawerId)}/eod-report.pdf`,
    { headers: { Authorization: `Bearer ${getToken()}` } },
  );
  if (!res.ok) throw new Error(`eod fetch failed (${res.status})`);
  const blob = await res.blob();
  return URL.createObjectURL(blob);
}
export async function openEodReport(drawerId) {
  const url = await fetchEodReportUrl(drawerId);
  window.open(url, '_blank', 'noopener');
}

// ─── Asset custody (STORY: teller asset deposit / withdraw) ─────────────────

// GET /api/teller/customers/{accountId}/assets
export function listCustomerAssets(accountId) {
  return call(`/customers/${encodeURIComponent(accountId)}/assets`);
}

// POST /api/teller/customers/{accountId}/assets
export function registerCustomerAsset(accountId, payload) {
  return call(`/customers/${encodeURIComponent(accountId)}/assets`, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

// POST /api/teller/assets/{assetId}/withdraw — returns 409 with `blockingLoan`
// when the asset is securing an open loan.
export function withdrawAsset(assetId, reason) {
  return call(`/assets/${encodeURIComponent(assetId)}/withdraw`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

// GET /api/teller/deposit-houses — used by the Asset Deposit dialog to populate
// the picker.
export function listDepositHouses() {
  return call('/deposit-houses');
}
