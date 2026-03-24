// Stub data generators matching the Blazor admin portal's placeholder data.
// Replace with real gRPC-web or REST calls when backend is available.

import dayjs from 'dayjs';

const rng = (seed) => {
  let s = seed;
  return () => { s = (s * 16807 + 0) % 2147483647; return s / 2147483647; };
};

export function generateCustomers(page = 0, pageSize = 10, search = '', statusFilter = '') {
  const names = [
    'Tendai Moyo', 'Chiedza Mutasa', 'Farai Chikwanha', 'Rudo Nyamupfukudza',
    'Blessing Chikowore', 'Tatenda Mashava', 'Nyasha Dube', 'Rumbidzai Hwami',
    'Simba Jongwe', 'Kudzi Mhike', 'Tawanda Nzira', 'Grace Mapfumo',
    'Tinashe Gumbo', 'Patience Mwale', 'Lloyd Phiri', 'Edith Banda',
    'Moses Sithole', 'Janet Sibanda', 'Charles Ngwenya', 'Susan Tembo',
  ];
  const statuses = ['Active', 'Active', 'Active', 'Suspended', 'Frozen', 'Closed'];
  const r = rng(42);
  let items = names.map((name, i) => ({
    id: `ACC-${String(1000 + i).padStart(6, '0')}`,
    name,
    phone: `+2637${String(70000000 + Math.floor(r() * 9999999)).slice(0, 8)}`,
    status: statuses[i % statuses.length],
    kycLevel: Math.min(3, Math.floor(r() * 4)),
    balanceZwg: +(r() * 50000).toFixed(2),
    balanceUsd: +(r() * 2000).toFixed(2),
    created: dayjs().subtract(Math.floor(r() * 180), 'day').format('YYYY-MM-DD'),
    lastLogin: dayjs().subtract(Math.floor(r() * 14), 'day').format('YYYY-MM-DD HH:mm'),
    email: `${name.split(' ')[0].toLowerCase()}@email.co.zw`,
    nationalId: `63-${String(Math.floor(r() * 9999999)).padStart(7, '0')}T${String(Math.floor(r() * 99)).padStart(2, '0')}`,
  }));
  if (search) items = items.filter((c) => c.name.toLowerCase().includes(search.toLowerCase()) || c.phone.includes(search));
  if (statusFilter) items = items.filter((c) => c.status === statusFilter);
  return { items: items.slice(page * pageSize, (page + 1) * pageSize), total: items.length };
}

export function generateTransactions(filters = {}) {
  const types = ['Purchase', 'Transfer', 'CashIn', 'CashOut', 'BillPay', 'P2P'];
  const txStatuses = ['Completed', 'Pending', 'Failed', 'Reversed'];
  const r = rng(99);
  const items = Array.from({ length: 50 }, (_, i) => ({
    id: `TXN-${String(100000 + i).padStart(8, '0')}`,
    accountId: `ACC-${String(1000 + Math.floor(r() * 20)).padStart(6, '0')}`,
    phone: `+2637${String(70000000 + Math.floor(r() * 9999999)).slice(0, 8)}`,
    type: types[Math.floor(r() * types.length)],
    amount: +(r() * 5000).toFixed(2),
    fee: +(r() * 25).toFixed(2),
    status: txStatuses[Math.floor(r() * txStatuses.length)],
    reference: `REF${String(Math.floor(r() * 999999)).padStart(6, '0')}`,
    counterparty: `ACC-${String(2000 + Math.floor(r() * 20)).padStart(6, '0')}`,
    date: dayjs().subtract(Math.floor(r() * 30), 'day').format('YYYY-MM-DD HH:mm'),
    currency: r() > 0.3 ? 'ZWG' : 'USD',
  }));
  return items;
}

export function generateDisputes() {
  const types = ['Unauthorized', 'Duplicate', 'MerchantDispute', 'ServiceNotRendered', 'WrongAmount'];
  const statuses = ['Open', 'Investigating', 'Resolved', 'Resolved'];
  const resolutions = ['', '', 'Refunded', 'Rejected'];
  const r = rng(77);
  return Array.from({ length: 15 }, (_, i) => {
    const status = statuses[i % statuses.length];
    const filed = dayjs().subtract(Math.floor(r() * 30), 'day');
    return {
      id: `DSP-${String(5000 + i).padStart(6, '0')}`,
      transactionId: `TXN-${String(100000 + Math.floor(r() * 50)).padStart(8, '0')}`,
      accountId: `ACC-${String(1000 + Math.floor(r() * 20)).padStart(6, '0')}`,
      type: types[Math.floor(r() * types.length)],
      description: 'Customer reported issue with transaction',
      status,
      resolution: resolutions[i % resolutions.length],
      refundAmount: status === 'Resolved' ? +(r() * 500).toFixed(2) : 0,
      agent: status !== 'Open' ? ['Agent Smith', 'Agent Jones', 'Agent Brown'][Math.floor(r() * 3)] : '',
      filed: filed.format('YYYY-MM-DD'),
      slaHours: Math.floor((dayjs().diff(filed, 'hour'))),
      resolved: status === 'Resolved' ? filed.add(Math.floor(r() * 48), 'hour').format('YYYY-MM-DD HH:mm') : '',
    };
  });
}

export function generateFraudAlerts() {
  const types = ['VelocityAnomaly', 'GeoAnomaly', 'AmountAnomaly', 'DeviceAnomaly', 'PatternMatch'];
  const severities = ['High', 'Medium', 'Low'];
  const statuses = ['New', 'Reviewed', 'Escalated', 'Dismissed'];
  const r = rng(55);
  return Array.from({ length: 12 }, (_, i) => ({
    id: `FRD-${String(8000 + i).padStart(6, '0')}`,
    accountId: `ACC-${String(1000 + Math.floor(r() * 20)).padStart(6, '0')}`,
    transactionId: `TXN-${String(100000 + Math.floor(r() * 50)).padStart(8, '0')}`,
    type: types[Math.floor(r() * types.length)],
    severity: severities[i % severities.length],
    description: `Suspicious ${types[Math.floor(r() * types.length)].toLowerCase()} detected`,
    status: statuses[i % statuses.length],
    created: dayjs().subtract(Math.floor(r() * 14), 'day').format('YYYY-MM-DD HH:mm'),
  }));
}

export function generateKycQueue() {
  const r = rng(33);
  const names = ['Tendai Moyo', 'Chiedza Mutasa', 'Farai Chikwanha', 'Rudo Nyamupfukudza', 'Blessing Chikowore', 'Simba Jongwe'];
  return names.map((name, i) => ({
    id: `KYC-${String(3000 + i).padStart(6, '0')}`,
    accountId: `ACC-${String(1000 + i).padStart(6, '0')}`,
    name,
    submittedDate: dayjs().subtract(Math.floor(r() * 7), 'day').format('YYYY-MM-DD HH:mm'),
    level: i < 3 ? 1 : 2,
    faceMatchScore: +(0.75 + r() * 0.24).toFixed(2),
    aiDecision: i === 3 ? 'Rejected' : 'AutoApproved',
    aiReason: i === 3 ? 'Face match below threshold' : '',
    extractedName: name,
    extractedIdNumber: `63-${String(Math.floor(r() * 9999999)).padStart(7, '0')}T${String(Math.floor(r() * 99)).padStart(2, '0')}`,
    extractedDob: `19${80 + Math.floor(r() * 15)}-${String(1 + Math.floor(r() * 12)).padStart(2, '0')}-${String(1 + Math.floor(r() * 28)).padStart(2, '0')}`,
    nameMatch: i !== 2,
    idMatch: true,
    dobMatch: i !== 4,
  }));
}

export function generateLoans() {
  const r = rng(88);
  const names = ['Tendai Moyo', 'Nyasha Dube', 'Tatenda Mashava', 'Grace Mapfumo', 'Lloyd Phiri'];
  const purposes = ['Business', 'Education', 'Medical', 'Home Improvement', 'Agriculture'];
  const verificationStatuses = ['Verified', 'Partial', 'Failed', 'Not Available', 'Verified'];
  return names.map((name, i) => ({
    id: `LOAN-${String(6000 + i).padStart(6, '0')}`,
    accountId: `ACC-${String(1000 + i).padStart(6, '0')}`,
    name,
    phone: `+2637${String(70000000 + Math.floor(r() * 9999999)).slice(0, 8)}`,
    email: `${name.split(' ')[0].toLowerCase()}@email.co.zw`,
    kycLevel: 2 + Math.floor(r() * 2),
    creditScore: Math.floor(500 + r() * 350),
    amount: Math.floor(500 + r() * 9500),
    tenure: [3, 6, 12, 18, 24][Math.floor(r() * 5)],
    purpose: purposes[i],
    monthlyRepayment: 0,
    appliedDate: dayjs().subtract(Math.floor(r() * 7), 'day').format('YYYY-MM-DD'),
    verificationStatus: verificationStatuses[i],
    faceMatchScore: verificationStatuses[i] !== 'Not Available' ? +(0.7 + r() * 0.29).toFixed(2) : null,
    extractedName: name,
    extractedEmployer: ['TechCo', 'AgriCorp', 'MedServices', 'BuildRight', 'FarmFirst'][i],
    extractedIncome: Math.floor(800 + r() * 3200),
    statedIncome: Math.floor(800 + r() * 3200),
  }));
}

export function generateAdminUsers() {
  return [
    { id: 1, username: 'admin', fullName: 'System Administrator', email: 'admin@unibank.co.zw', role: 'Admin', branch: 'Head Office', status: 'Active' },
    { id: 2, username: 'kyc', fullName: 'KYC Officer', email: 'kyc@unibank.co.zw', role: 'KycOfficer', branch: 'Head Office', status: 'Active' },
    { id: 3, username: 'fraud', fullName: 'Fraud Analyst', email: 'fraud@unibank.co.zw', role: 'FraudAnalyst', branch: 'Head Office', status: 'Active' },
    { id: 4, username: 'support', fullName: 'Customer Support', email: 'support@unibank.co.zw', role: 'CustomerService', branch: 'Harare CBD', status: 'Active' },
    { id: 5, username: 'loans', fullName: 'Loan Officer', email: 'loans@unibank.co.zw', role: 'LoanOfficer', branch: 'Borrowdale', status: 'Active' },
    { id: 6, username: 'compliance', fullName: 'Compliance Officer', email: 'compliance@unibank.co.zw', role: 'ComplianceOfficer', branch: 'Head Office', status: 'Active' },
    { id: 7, username: 'branch', fullName: 'Branch Manager', email: 'branch@unibank.co.zw', role: 'BranchManager', branch: 'Bulawayo Main', status: 'Active' },
  ];
}

export function generateBranches() {
  return [
    { id: 1, name: 'Head Office', code: 'HQ001', address: '1 Bank Street', city: 'Harare', phone: '+263 242 000 001', active: true },
    { id: 2, name: 'Harare CBD', code: 'HAR001', address: '55 Samora Machel Ave', city: 'Harare', phone: '+263 242 000 002', active: true },
    { id: 3, name: 'Borrowdale', code: 'HAR002', address: '12 Borrowdale Rd', city: 'Harare', phone: '+263 242 000 003', active: true },
    { id: 4, name: 'Bulawayo Main', code: 'BYO001', address: '8th Ave & Fife St', city: 'Bulawayo', phone: '+263 292 000 001', active: true },
    { id: 5, name: 'Mutare Branch', code: 'MUT001', address: '3 Herbert Chitepo St', city: 'Mutare', phone: '+263 220 000 001', active: false },
  ];
}

export function generateAuditTrail() {
  const actions = ['Login', 'KYC Review', 'Dispute Resolution', 'Account Action', 'Config Change'];
  const users = ['admin', 'kyc', 'fraud', 'support', 'loans', 'compliance', 'branch'];
  const r = rng(66);
  return Array.from({ length: 60 }, (_, i) => ({
    id: i + 1,
    adminUser: users[Math.floor(r() * users.length)],
    action: actions[Math.floor(r() * actions.length)],
    target: `ACC-${String(1000 + Math.floor(r() * 20)).padStart(6, '0')}`,
    timestamp: dayjs().subtract(Math.floor(r() * 168), 'hour').format('YYYY-MM-DD HH:mm:ss'),
    details: JSON.stringify({ ip: `192.168.1.${Math.floor(r() * 254) + 1}`, browser: 'Chrome 130' }),
  }));
}

export function generateDashboardMetrics() {
  return {
    totalUsers: 15420,
    activeUsers: 12380,
    totalTransactions: 284500,
    transactionVolume: 45200000,
    revenue: 1250000,
    merchants: 842,
    agents: 1560,
    terminals: 3200,
    dailyTransactions: Array.from({ length: 30 }, (_, i) => ({
      date: dayjs().subtract(29 - i, 'day').format('MMM DD'),
      count: Math.floor(8000 + Math.random() * 4000),
    })),
  };
}

export function generateUserGrowthData(granularity = 'Daily') {
  const periods = granularity === 'Daily' ? 30 : granularity === 'Weekly' ? 12 : 6;
  const r = rng(44);
  return {
    totalRegistered: 15420,
    totalActive: 12380,
    growthRate: 8.5,
    data: Array.from({ length: periods }, (_, i) => ({
      period: dayjs().subtract(periods - 1 - i, granularity === 'Daily' ? 'day' : granularity === 'Weekly' ? 'week' : 'month').format(granularity === 'Monthly' ? 'MMM YY' : 'MMM DD'),
      newRegistrations: Math.floor(100 + r() * 200),
      activeUsers: Math.floor(400 + r() * 300),
      churned: Math.floor(r() * 30),
    })),
  };
}

export function generateRevenueData(granularity = 'Daily') {
  const periods = granularity === 'Daily' ? 30 : granularity === 'Weekly' ? 12 : 6;
  const r = rng(22);
  const types = ['Purchase', 'Transfer', 'CashIn', 'CashOut', 'BillPay'];
  return {
    totalRevenue: 1250000,
    data: Array.from({ length: periods }, (_, i) => ({
      period: dayjs().subtract(periods - 1 - i, granularity === 'Daily' ? 'day' : granularity === 'Weekly' ? 'week' : 'month').format(granularity === 'Monthly' ? 'MMM YY' : 'MMM DD'),
      revenue: Math.floor(30000 + r() * 20000),
    })),
    breakdown: types.map((type) => ({
      type,
      amount: Math.floor(100000 + r() * 400000),
      percentage: +(r() * 40).toFixed(1),
    })),
  };
}

export function generateMerchantData() {
  const r = rng(11);
  const merchants = ['ShopRite Harare', 'OK Zimbabwe', 'TM Pick n Pay', 'Spar Borrowdale', 'Food Lovers'];
  return {
    totalVolume: 28500000,
    totalTransactions: 142000,
    merchants: merchants.map((name, i) => ({
      id: `MER-${String(4000 + i).padStart(6, '0')}`,
      name,
      volume: Math.floor(2000000 + r() * 8000000),
      transactions: Math.floor(10000 + r() * 40000),
      commission: Math.floor(50000 + r() * 200000),
    })),
  };
}

export function generateReconData() {
  const r = rng(77);
  return {
    totalTransactions: 5420,
    totalAmount: 12500000,
    matched: 5380,
    unmatched: 40,
    status: 'Completed',
    discrepancies: Array.from({ length: 8 }, (_, i) => ({
      transactionId: `TXN-${String(200000 + i).padStart(8, '0')}`,
      internalAmount: +(1000 + r() * 4000).toFixed(2),
      partnerAmount: +(1000 + r() * 4000).toFixed(2),
      difference: +(r() * 100 - 50).toFixed(2),
    })),
  };
}
