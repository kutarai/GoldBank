export const Roles = {
  Admin: 'Admin',
  KycOfficer: 'KycOfficer',
  FraudAnalyst: 'FraudAnalyst',
  CustomerService: 'CustomerService',
  LoanOfficer: 'LoanOfficer',
  ComplianceOfficer: 'ComplianceOfficer',
  BranchManager: 'BranchManager',
  Teller: 'Teller',
};

export const Access = {
  KycAccess: [Roles.Admin, Roles.KycOfficer, Roles.BranchManager],
  FraudAccess: [Roles.Admin, Roles.FraudAnalyst, Roles.BranchManager],
  CustomerAccess: [Roles.Admin, Roles.CustomerService, Roles.BranchManager],
  DisputeAccess: [Roles.Admin, Roles.CustomerService, Roles.BranchManager],
  LoanAccess: [Roles.Admin, Roles.LoanOfficer, Roles.BranchManager],
  ReportAccess: [Roles.Admin, Roles.ComplianceOfficer, Roles.BranchManager],
  UserManagement: [Roles.Admin, Roles.BranchManager],
  ConfigAccess: [Roles.Admin],
};

export const SEED_ACCOUNTS = [
  { username: 'admin', password: 'Admin@1234', role: Roles.Admin, name: 'System Administrator' },
  { username: 'kyc', password: 'Kyc@1234', role: Roles.KycOfficer, name: 'KYC Officer' },
  { username: 'fraud', password: 'Fraud@1234', role: Roles.FraudAnalyst, name: 'Fraud Analyst' },
  { username: 'support', password: 'Support@1234', role: Roles.CustomerService, name: 'Customer Support' },
  { username: 'loans', password: 'Loans@1234', role: Roles.LoanOfficer, name: 'Loan Officer' },
  { username: 'compliance', password: 'Compliance@1234', role: Roles.ComplianceOfficer, name: 'Compliance Officer' },
  { username: 'branch', password: 'Branch@1234', role: Roles.BranchManager, name: 'Branch Manager' },
];
