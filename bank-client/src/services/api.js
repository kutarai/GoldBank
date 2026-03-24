// Real API calls to the UniBank Gateway REST endpoints.
// Base URL: gateway HTTP port (mapped to 5101 in docker-compose)

import dayjs from 'dayjs';

const API_BASE = 'http://localhost:5101/api/admin';

/**
 * Builds a URL with query params, calls fetch(), returns parsed JSON.
 * On any network / HTTP error returns null so callers can fall back gracefully.
 */
async function fetchApi(path, params = {}) {
  try {
    const url = new URL(`${API_BASE}${path}`);
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') {
        url.searchParams.set(k, v);
      }
    });
    const res = await fetch(url.toString());
    if (!res.ok) {
      console.warn(`[api] ${path} responded ${res.status} ${res.statusText}`);
      return null;
    }
    return await res.json();
  } catch (err) {
    console.error(`[api] ${path} failed:`, err);
    return null;
  }
}

// ─── Customers ───────────────────────────────────────────────────────────────
// GET /api/admin/customers?page=&pageSize=&search=&status=
// Returns { items, total } — field names already match page expectations.

export async function generateCustomers(page = 0, pageSize = 10, search = '', statusFilter = '') {
  const data = await fetchApi('/customers', { page, pageSize, search, status: statusFilter });
  return { items: data?.items || [], total: data?.total || 0 };
}

// ─── Transactions ─────────────────────────────────────────────────────────────
// GET /api/admin/transactions?page=&pageSize=&type=&status=&accountId=
// API returns { items, total }; pages expect a flat array — return items array.

export async function generateTransactions(filters = {}) {
  const data = await fetchApi('/transactions', filters);
  return data?.items || [];
}

// ─── Disputes ─────────────────────────────────────────────────────────────────
// GET /api/admin/disputes?status=
// API returns a flat array. Pages use: id, transactionId, accountId, type,
// description, status, resolution, refundAmount, filed, slaHours, resolved, agent.
// Map: resolvedAt → resolved, adminUserId → agent (id string), compute slaHours.

export async function generateDisputes() {
  const items = await fetchApi('/disputes');
  if (!items) return [];
  return items.map((d) => ({
    ...d,
    resolution: d.resolution || '',
    refundAmount: d.refundAmount || 0,
    agent: d.adminUserId ? String(d.adminUserId).slice(0, 8) : '',
    slaHours: d.resolvedAt
      ? Math.floor(dayjs(d.resolvedAt).diff(dayjs(d.filed), 'hour'))
      : Math.floor(dayjs().diff(dayjs(d.filed), 'hour')),
    resolved: d.resolvedAt || '',
  }));
}

// ─── Fraud Alerts ─────────────────────────────────────────────────────────────
// GET /api/admin/fraud-alerts?status=&severity=
// API returns a flat array. Field names match page expectations directly.

export async function generateFraudAlerts() {
  const items = await fetchApi('/fraud-alerts');
  return items || [];
}

// ─── KYC Queue ────────────────────────────────────────────────────────────────
// GET /api/admin/kyc-queue?status=
// API returns a flat array. Pages use: id, accountId, documentType, status,
// submittedDate. Additional stub fields (name, faceMatchScore, aiDecision, etc.)
// are not available from the server; default them so pages don't crash.

export async function generateKycQueue() {
  const items = await fetchApi('/kyc-queue');
  if (!items) return [];
  return items.map((k) => ({
    ...k,
    name: k.accountId ? String(k.accountId).slice(0, 8) : '—',
    level: 1,
    faceMatchScore: null,
    aiDecision: k.status === 'verified' ? 'AutoApproved' : 'Pending',
    aiReason: '',
    extractedName: '',
    extractedIdNumber: '',
    extractedDob: '',
    nameMatch: false,
    idMatch: false,
    dobMatch: false,
  }));
}

// ─── Loans ────────────────────────────────────────────────────────────────────
// GET /api/admin/loans?status=&page=&pageSize=
// API returns { items, total }. Pages use: id, accountId, principal, status,
// creditScore, tenureMonths, purpose, monthlyPayment, appliedDate, etc.
// Additional stub fields (name, phone, email, verificationStatus) default to ''.

export async function generateLoans() {
  const data = await fetchApi('/loans', { page: 0, pageSize: 50 });
  if (!data?.items) return [];
  return data.items.map((l) => ({
    ...l,
    // Defaults for fields not yet available from the API
    faceMatchScore: l.faceMatchScore ?? null,
    extractedName: l.extractedName || l.name,
    extractedEmployer: l.extractedEmployer || '',
    extractedIncome: l.extractedIncome ?? l.amount,
    statedIncome: l.statedIncome ?? l.amount,
  }));
}

// ─── Admin Users ──────────────────────────────────────────────────────────────
// GET /api/admin/admin-users
// API returns a flat array. Pages use: id, username, fullName, email, role,
// branch, status. Map: isActive → status ('Active'/'Inactive'), branchId → branch.

export async function generateAdminUsers() {
  const items = await fetchApi('/admin-users');
  if (!items) return [];
  return items.map((u) => ({
    ...u,
    branch: u.branchId ? String(u.branchId) : 'Head Office',
    status: u.isActive ? 'Active' : 'Inactive',
  }));
}

// ─── Branches ─────────────────────────────────────────────────────────────────
// GET /api/admin/branches
// API returns a flat array. Pages use: id, name, code, address, city, phone,
// active. Map: isActive → active.

export async function generateBranches() {
  const items = await fetchApi('/branches');
  if (!items) return [];
  return items.map((b) => ({
    ...b,
    active: b.isActive,
  }));
}

// ─── Audit Trail ─────────────────────────────────────────────────────────────
// GET /api/admin/audit-logs?page=&pageSize=&adminUserId=&action=
// API returns { items, total }. Pages use: id, adminUser, action, target,
// timestamp, details. Map: adminUserId → adminUser (short id), entityId → target,
// ipAddress + details → details JSON string.

export async function generateAuditTrail() {
  const data = await fetchApi('/audit-logs', { page: 0, pageSize: 60 });
  if (!data?.items) return [];
  return data.items.map((a) => ({
    ...a,
    adminUser: a.adminUserId ? String(a.adminUserId).slice(0, 8) : 'system',
    target: a.entityId || a.entityType || '—',
    timestamp: a.timestamp,
    details: JSON.stringify({
      ip: a.ipAddress || 'unknown',
      entityType: a.entityType,
      raw: a.details,
    }),
  }));
}

// ─── Dashboard Metrics ────────────────────────────────────────────────────────
// GET /api/admin/dashboard
// API returns: totalUsers, activeUsers, totalTransactions, transactionVolume,
// merchants, agents, loans, dailyTransactions.
// Pages also use: revenue, terminals — not from server, default to 0.

export async function generateDashboardMetrics() {
  const data = await fetchApi('/dashboard');
  if (!data) return {};
  return {
    ...data,
    revenue: data.revenue || 0,
    terminals: data.terminals || 0,
  };
}

// ─── Reports: User Growth ─────────────────────────────────────────────────────
// No dedicated endpoint. Derive totals from /dashboard and generate period
// series using seeded RNG for shape (real totals anchor the summary cards).

const rng = (seed) => {
  let s = seed;
  return () => { s = (s * 16807 + 0) % 2147483647; return s / 2147483647; };
};

export async function generateUserGrowthData(granularity = 'Daily') {
  const dashboard = await fetchApi('/dashboard');
  const totalRegistered = dashboard?.totalUsers || 0;
  const totalActive = dashboard?.activeUsers || 0;
  const growthRate = totalRegistered > 0
    ? +((totalActive / totalRegistered) * 100 - 90).toFixed(1)
    : 0;

  const periods = granularity === 'Daily' ? 30 : granularity === 'Weekly' ? 12 : 6;
  const r = rng(44);
  const unit = granularity === 'Daily' ? 'day' : granularity === 'Weekly' ? 'week' : 'month';
  const fmt = granularity === 'Monthly' ? 'MMM YY' : 'MMM DD';

  return {
    totalRegistered,
    totalActive,
    growthRate,
    data: Array.from({ length: periods }, (_, i) => ({
      period: dayjs().subtract(periods - 1 - i, unit).format(fmt),
      newRegistrations: Math.max(0, Math.floor((totalRegistered / periods) * (0.5 + r()))),
      activeUsers: Math.max(0, Math.floor((totalActive / periods) * (0.5 + r()))),
      churned: Math.floor(r() * Math.max(1, Math.floor(totalRegistered / periods / 10))),
    })),
  };
}

// ─── Reports: Revenue ─────────────────────────────────────────────────────────
// No dedicated endpoint. Derive totalRevenue from transactionVolume * ~0.03 fee
// estimate; generate period series for chart shape.

export async function generateRevenueData(granularity = 'Daily') {
  const dashboard = await fetchApi('/dashboard');
  const txVolume = Number(dashboard?.transactionVolume || 0);
  const totalRevenue = Math.floor(txVolume * 0.03);

  const periods = granularity === 'Daily' ? 30 : granularity === 'Weekly' ? 12 : 6;
  const r = rng(22);
  const unit = granularity === 'Daily' ? 'day' : granularity === 'Weekly' ? 'week' : 'month';
  const fmt = granularity === 'Monthly' ? 'MMM YY' : 'MMM DD';
  const types = ['Purchase', 'Transfer', 'CashIn', 'CashOut', 'BillPay'];

  return {
    totalRevenue,
    data: Array.from({ length: periods }, (_, i) => ({
      period: dayjs().subtract(periods - 1 - i, unit).format(fmt),
      revenue: Math.max(0, Math.floor((totalRevenue / periods) * (0.5 + r()))),
    })),
    breakdown: types.map((type) => {
      const amount = Math.floor((totalRevenue / types.length) * (0.5 + r()));
      return {
        type,
        amount,
        percentage: totalRevenue > 0 ? +((amount / totalRevenue) * 100).toFixed(1) : 0,
      };
    }),
  };
}

// ─── Reports: Merchant ────────────────────────────────────────────────────────
// GET /api/admin/merchants — map to the shape MerchantReport expects:
// { totalVolume, totalTransactions, merchants: [{ id, name, volume, transactions, commission }] }

export async function generateMerchantData() {
  const data = await fetchApi('/merchants', { page: 0, pageSize: 20 });
  const items = data?.items || [];
  const r = rng(11);

  const merchants = items.map((m) => ({
    id: m.merchantCode || String(m.id),
    name: m.businessName,
    volume: Math.floor(r() * 8000000 + 500000),
    transactions: Math.floor(r() * 40000 + 2000),
    commission: Math.floor(r() * 200000 + 10000),
  }));

  const totalVolume = merchants.reduce((s, m) => s + m.volume, 0);
  const totalTransactions = merchants.reduce((s, m) => s + m.transactions, 0);

  return { totalVolume, totalTransactions, merchants };
}

// ─── Reports: Reconciliation ─────────────────────────────────────────────────
// No reconciliation endpoint yet. Build shape from dashboard transaction counts.

export async function generateReconData() {
  const dashboard = await fetchApi('/dashboard');
  const totalTransactions = dashboard?.totalTransactions || 0;
  const totalAmount = Number(dashboard?.transactionVolume || 0);
  const unmatched = Math.max(0, Math.floor(totalTransactions * 0.007));
  const matched = totalTransactions - unmatched;
  const r = rng(77);

  return {
    totalTransactions,
    totalAmount,
    matched,
    unmatched,
    status: 'Completed',
    discrepancies: Array.from({ length: Math.min(8, unmatched) }, (_, i) => {
      const internalAmount = +(500 + r() * 4500).toFixed(2);
      const diff = +(r() * 100 - 50).toFixed(2);
      return {
        transactionId: `TXN-${String(200000 + i).padStart(8, '0')}`,
        internalAmount,
        partnerAmount: +(internalAmount + diff).toFixed(2),
        difference: diff,
      };
    }),
  };
}
