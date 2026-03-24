using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.Core.Modules.BillPay.Domain.Entities;
using UniBank.Core.Modules.FraudDetection.Domain.Entities;
using UniBank.Core.Modules.KYC.Domain.Entities;
using UniBank.Core.Modules.Loans.Domain.Entities;
using UniBank.Core.Modules.Merchants.Domain.Entities;

namespace UniBank.Migrator;

/// <summary>
/// Seeds the UniBank database with demo data that matches the bank-client React app's
/// stub data generators exactly (same names, amounts, statuses, dates).
///
/// RNG seeds used in the JS stubs:
///   customers=42, transactions=99, disputes=77, fraudAlerts=55,
///   kycQueue=33, loans=88, auditTrail=66
///
/// Usage: dotnet run -- --seed
/// </summary>
internal static class DemoSeeder
{
    // ── Deterministic UUIDs ────────────────────────────────────────────────
    // Scheme: XXYYYYYY-0000-4000-8000-NNNNNNNNNNNN
    //   XX = table prefix (hex)  NN = row number (hex, 1-based)
    //
    //  01 = tenant      02 = admin_user   03 = account      04 = transaction
    //  05 = dispute     06 = fraud_alert  07 = kyc_document  08 = loan
    //  09 = merchant    0a = bill_provider 0b = system_config 0c = audit_log

    private static Guid TenantUuid(int row) => MakeUuid(0x01, row);
    private static Guid AdminUuid(int row) => MakeUuid(0x02, row);
    private static Guid AcctUuid(int row) => MakeUuid(0x03, row);
    private static Guid TxnUuid(int row) => MakeUuid(0x04, row);
    private static Guid DisputeUuid(int row) => MakeUuid(0x05, row);
    private static Guid FraudUuid(int row) => MakeUuid(0x06, row);
    private static Guid KycUuid(int row) => MakeUuid(0x07, row);
    private static Guid LoanUuid(int row) => MakeUuid(0x08, row);
    private static Guid MerchantUuid(int row) => MakeUuid(0x09, row);
    private static Guid BillUuid(int row) => MakeUuid(0x0a, row);
    private static Guid CfgUuid(int row) => MakeUuid(0x0b, row);
    private static Guid AuditUuid(int row) => MakeUuid(0x0c, row);

    private static Guid MakeUuid(int prefix, int row)
    {
        // Build: PPXXXXXX-0000-4000-8000-00000000RRRR
        var bytes = new byte[16];
        bytes[0] = (byte)prefix;
        // bytes[1..3] = 0  (already zero)
        // bytes[4..5] = 0000
        // bytes[6] = 0x40  (version 4)
        bytes[6] = 0x40;
        // bytes[7] = 0x00
        // bytes[8] = 0x80  (variant)
        bytes[8] = 0x80;
        // bytes[9..11] = 0x00 0x00 0x00
        // bytes[12..15] = row (big-endian)
        bytes[12] = (byte)((row >> 24) & 0xFF);
        bytes[13] = (byte)((row >> 16) & 0xFF);
        bytes[14] = (byte)((row >> 8) & 0xFF);
        bytes[15] = (byte)(row & 0xFF);
        return new Guid(bytes);
    }

    // Account row index: customer_index * 2 + currency_offset  (ZWG=1, USD=2)
    private static Guid ZwgAcct(int customerIdx) => AcctUuid(customerIdx * 2 + 1);
    private static Guid UsdAcct(int customerIdx) => AcctUuid(customerIdx * 2 + 2);

    private static readonly DateTime Now = DateTime.UtcNow;
    private static DateTime Ago(int days) => Now.AddDays(-days);
    private static DateTime AgoH(double hours) => Now.AddHours(-hours);

    // ── Public entry point ─────────────────────────────────────────────────

    public static async Task SeedAsync(UniBankDbContext db)
    {
        Console.WriteLine("[DemoSeeder] Seeding demo data...");

        await SeedAdminUsersAsync(db);
        await SeedAccountsAsync(db);
        await SeedTransactionsAsync(db);
        await SeedDisputesAsync(db);
        await SeedFraudAlertsAsync(db);
        await SeedKycDocumentsAsync(db);
        await SeedLoansAsync(db);
        await SeedMerchantsAsync(db);
        await SeedBillProvidersAsync(db);
        await SeedSystemConfigsAsync(db);
        await SeedAuditLogsAsync(db);

        Console.WriteLine("[DemoSeeder] Demo data seeded successfully.");
    }

    // ── 1. Admin Users ─────────────────────────────────────────────────────

    private static async Task SeedAdminUsersAsync(UniBankDbContext db)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        // BCrypt placeholder hashes — real auth uses these from the DB.
        // The seeded password for each user equals their username (for demo only).
        var users = new[]
        {
            New<AdminUser>(AdminUuid(1), u =>
            {
                u.Username = "admin";
                u.PasswordHash = "$2a$10$demoSeedHashAdminXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "admin@unibank.co.zw";
                u.FullName = "System Administrator";
                u.Role = AdminRole.Admin;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(365);
            }),
            New<AdminUser>(AdminUuid(2), u =>
            {
                u.Username = "kyc";
                u.PasswordHash = "$2a$10$demoSeedHashKycXXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "kyc@unibank.co.zw";
                u.FullName = "KYC Officer";
                u.Role = AdminRole.KycOfficer;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(300);
            }),
            New<AdminUser>(AdminUuid(3), u =>
            {
                u.Username = "fraud";
                u.PasswordHash = "$2a$10$demoSeedHashFraudXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "fraud@unibank.co.zw";
                u.FullName = "Fraud Analyst";
                u.Role = AdminRole.FraudAnalyst;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(280);
            }),
            New<AdminUser>(AdminUuid(4), u =>
            {
                u.Username = "support";
                u.PasswordHash = "$2a$10$demoSeedHashSupportXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "support@unibank.co.zw";
                u.FullName = "Customer Support";
                u.Role = AdminRole.CustomerService;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(250);
            }),
            New<AdminUser>(AdminUuid(5), u =>
            {
                u.Username = "loans";
                u.PasswordHash = "$2a$10$demoSeedHashLoansXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "loans@unibank.co.zw";
                u.FullName = "Loan Officer";
                u.Role = AdminRole.LoanOfficer;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(220);
            }),
            New<AdminUser>(AdminUuid(6), u =>
            {
                u.Username = "compliance";
                u.PasswordHash = "$2a$10$demoSeedHashComplyXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "compliance@unibank.co.zw";
                u.FullName = "Compliance Officer";
                u.Role = AdminRole.ComplianceOfficer;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(200);
            }),
            New<AdminUser>(AdminUuid(7), u =>
            {
                u.Username = "branch";
                u.PasswordHash = "$2a$10$demoSeedHashBranchXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.";
                u.Email = "branch@unibank.co.zw";
                u.FullName = "Branch Manager";
                u.Role = AdminRole.BranchManager;
                u.TenantId = "unibank";
                u.IsActive = true;
                u.CreatedAt = Ago(180);
            }),
        };

        db.AdminUsers.AddRange(users);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [AdminUsers] seeded {users.Length}");
    }

    // ── 2. Accounts ────────────────────────────────────────────────────────
    // 20 customers × 2 currencies = 40 accounts (seed=42)

    private static async Task SeedAccountsAsync(UniBankDbContext db)
    {
        if (await db.Accounts.CountAsync() >= 40) return;

        // Pre-computed values from JS rng(42) execution
        var customers = new[]
        {
            // (firstName, lastName, phone, status, kycLevel, balZwg, balUsd, createdDaysAgo, lastLoginDaysAgo, nationalId)
            ("Tendai",    "Moyo",           "+263770003287", "active",    2,  36771.18m,   526.61m,  67,  2, "63-9758737T50"),
            ("Chiedza",   "Mutasa",         "+263775304489", "active",    1,   5354.36m,  1630.98m, 163,  6, "63-2453886T24"),
            ("Farai",     "Chikwanha",      "+263771882741", "active",    1,   5172.87m,  1614.75m,  96, 11, "63-7370189T07"),
            ("Rudo",      "Nyamupfukudza",  "+263775390093", "suspended", 0,  43126.27m,   929.19m,  84,  6, "63-2334748T01"),
            ("Blessing",  "Chikowore",      "+263770230257", "frozen",    3,  45771.96m,  1571.67m, 102, 10, "63-6235701T34"),
            ("Tatenda",   "Mashava",        "+263773756331", "closed",    1,  17586.84m,  1281.89m,  65,  0, "63-5286581T15"),
            ("Nyasha",    "Dube",           "+263774538185", "active",    1,   9639.03m,   129.47m, 177,  7, "63-0891291T98"),
            ("Rumbidzai", "Hwami",          "+263774337300", "active",    2,   9337.01m,  1087.40m, 179,  2, "63-6842624T39"),
            ("Simba",     "Jongwe",         "+263774389686", "active",    2,   8903.47m,  1621.56m, 145,  1, "63-0296979T13"),
            ("Kudzi",     "Mhike",          "+263776374355", "suspended", 1,  12469.53m,  1013.45m,  93,  1, "63-9835989T34"),
            ("Tawanda",   "Nzira",          "+263777683265", "frozen",    1,  33322.17m,  1830.25m,  83,  6, "63-9214295T46"),
            ("Grace",     "Mapfumo",        "+263779772308", "closed",    1,  39709.24m,  1731.03m, 133,  7, "63-8267703T52"),
            ("Tinashe",   "Gumbo",          "+263773073923", "active",    1,  15566.27m,   893.15m, 102,  9, "63-0629195T48"),
            ("Patience",  "Mwale",          "+263774562389", "active",    0,  35052.03m,   776.52m,  80, 13, "63-1251519T42"),
            ("Lloyd",     "Phiri",          "+263772925215", "active",    1,  12183.51m,   731.33m, 127, 13, "63-0178186T47"),
            ("Edith",     "Banda",          "+263778215940", "suspended", 2,   6299.16m,   800.63m,  23, 11, "63-3916865T07"),
            ("Moses",     "Sithole",        "+263775976281", "frozen",    1,  12680.39m,   772.93m,  56,  8, "63-2042175T28"),
            ("Janet",     "Sibanda",        "+263779891232", "closed",    0,  33091.91m,  1028.36m, 141,  1, "63-3581377T22"),
            ("Charles",   "Ngwenya",        "+263772988140", "active",    0,  47821.52m,  1451.49m, 106,  7, "63-4892821T36"),
            ("Susan",     "Tembo",          "+263772932938", "active",    1,  20424.32m,   858.61m,  57,  4, "63-1086290T72"),
        };

        var accounts = new List<Account>();
        for (var i = 0; i < customers.Length; i++)
        {
            var (fn, ln, phone, status, kycLevel, balZwg, balUsd, createdDays, loginDays, nationalId) = customers[i];
            var email = $"{fn.ToLower()}@email.co.zw";
            var pin = "$2a$10$demoPinHashXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
            var created = Ago(createdDays);
            var lastLogin = Ago(loginDays);

            accounts.Add(New<Account>(ZwgAcct(i), a =>
            {
                a.PhoneNumber = phone;
                a.PhoneCountryCode = "+263";
                a.PinHash = pin;
                a.TenantId = "unibank";
                a.Status = status;
                a.KycLevel = kycLevel;
                a.DailyLimit = 50000m;
                a.MonthlyLimit = 200000m;
                a.Balance = balZwg;
                a.AvailableBalance = balZwg;
                a.Currency = "ZWG";
                a.FirstName = fn;
                a.LastName = ln;
                a.Email = email;
                a.NationalId = nationalId;
                a.LastLoginAt = lastLogin;
                a.CreatedAt = created;
            }));

            accounts.Add(New<Account>(UsdAcct(i), a =>
            {
                a.PhoneNumber = phone;
                a.PhoneCountryCode = "+263";
                a.PinHash = pin;
                a.TenantId = "unibank";
                a.Status = status;
                a.KycLevel = kycLevel;
                a.DailyLimit = 10000m;
                a.MonthlyLimit = 50000m;
                a.Balance = balUsd;
                a.AvailableBalance = balUsd;
                a.Currency = "USD";
                a.FirstName = fn;
                a.LastName = ln;
                a.Email = email;
                a.NationalId = nationalId;
                a.LastLoginAt = lastLogin;
                a.CreatedAt = created;
            }));
        }

        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [Accounts] seeded {accounts.Count}");
    }

    // ── 3. Transactions ────────────────────────────────────────────────────
    // 50 rows (seed=99), account_id from ZwgAcct(accountIdx) except USD txns

    private static async Task SeedTransactionsAsync(UniBankDbContext db)
    {
        if (await db.Transactions.AnyAsync()) return;

        // Pre-computed from JS rng(99):
        // (accountIdx, type, amount, fee, status, reference, counterparty, dateDays, currency)
        // currency=USD rows use UsdAcct(accountIdx) instead of ZwgAcct
        var rows = new (int ai, string type, decimal amount, decimal fee, string status, string reference, string cp, int days, string cur)[]
        {
            (0,  "BillPay",  2031.82m, 18.60m, "Failed",    "REF657416", "ACC-002004", 7,  "ZWG"),
            (19, "Transfer", 2113.19m,  7.32m, "Pending",   "REF443788", "ACC-002015", 28, "ZWG"),
            (6,  "Transfer",  566.06m, 19.26m, "Completed", "REF104524", "ACC-002014", 5,  "ZWG"),
            (2,  "P2P",      3494.93m, 21.80m, "Pending",   "REF631217", "ACC-002017", 3,  "USD"),
            (9,  "CashOut",  2911.14m, 12.51m, "Completed", "REF531836", "ACC-002011", 29, "USD"),
            (17, "CashOut",  1078.89m, 14.18m, "Pending",   "REF165220", "ACC-002017", 0,  "ZWG"),
            (11, "Purchase", 2921.31m, 16.94m, "Reversed",  "REF348305", "ACC-002019", 24, "ZWG"),
            (11, "CashIn",   2339.63m, 11.12m, "Reversed",  "REF157015", "ACC-002019", 11, "USD"),
            (19, "CashOut",   785.37m, 23.91m, "Pending",   "REF886226", "ACC-002016", 28, "ZWG"),
            (1,  "BillPay",  4070.48m, 13.01m, "Reversed",  "REF795995", "ACC-002006", 19, "USD"),
            (3,  "CashIn",   3272.21m,  5.46m, "Failed",    "REF438166", "ACC-002005", 27, "ZWG"),
            (11, "P2P",       915.05m, 21.50m, "Pending",   "REF816983", "ACC-002001", 22, "ZWG"),
            (0,  "Transfer",   75.17m, 16.52m, "Pending",   "REF851831", "ACC-002015", 27, "ZWG"),
            (16, "P2P",      2266.38m,  4.89m, "Pending",   "REF474361", "ACC-002012", 3,  "ZWG"),
            (8,  "Purchase", 3335.02m,  8.68m, "Pending",   "REF040157", "ACC-002018", 28, "ZWG"),
            (18, "CashIn",   3879.81m, 15.14m, "Pending",   "REF544503", "ACC-002009", 16, "ZWG"),
            (10, "CashOut",   608.92m, 20.23m, "Pending",   "REF223243", "ACC-002001", 24, "ZWG"),
            (19, "Purchase",  288.28m,  0.88m, "Completed", "REF052678", "ACC-002007", 15, "USD"),
            (12, "Purchase", 1178.83m, 12.82m, "Pending",   "REF279644", "ACC-002019", 22, "USD"),
            (13, "CashIn",   4692.93m, 20.71m, "Reversed",  "REF185377", "ACC-002012", 14, "ZWG"),
            (4,  "BillPay",  2435.38m,  6.89m, "Failed",    "REF205148", "ACC-002018", 7,  "ZWG"),
            (1,  "CashOut",  1225.10m,  1.43m, "Reversed",  "REF271312", "ACC-002018", 12, "ZWG"),
            (14, "P2P",      2270.97m, 15.72m, "Pending",   "REF100227", "ACC-002010", 22, "ZWG"),
            (14, "P2P",      3901.52m, 14.61m, "Reversed",  "REF337665", "ACC-002003", 7,  "ZWG"),
            (5,  "Transfer", 4291.71m,  3.77m, "Reversed",  "REF912547", "ACC-002004", 12, "USD"),
            (1,  "CashOut",    30.82m, 14.96m, "Pending",   "REF566928", "ACC-002007", 9,  "ZWG"),
            (3,  "CashIn",    460.12m, 15.91m, "Pending",   "REF171365", "ACC-002002", 0,  "ZWG"),
            (5,  "CashOut",  3261.30m, 13.12m, "Completed", "REF909715", "ACC-002012", 24, "ZWG"),
            (4,  "P2P",      1701.72m,  4.03m, "Completed", "REF509038", "ACC-002008", 15, "ZWG"),
            (0,  "BillPay",  2298.48m,  3.16m, "Completed", "REF721653", "ACC-002016", 27, "ZWG"),
            (18, "Transfer", 3547.80m, 14.69m, "Completed", "REF473763", "ACC-002011", 8,  "ZWG"),
            (9,  "P2P",      1054.51m, 15.92m, "Failed",    "REF161480", "ACC-002000", 1,  "USD"),
            (10, "Purchase", 1423.63m,  9.95m, "Pending",   "REF763146", "ACC-002004", 8,  "ZWG"),
            (12, "P2P",      1198.64m,  2.84m, "Pending",   "REF650011", "ACC-002014", 18, "USD"),
            (10, "P2P",       661.14m,  8.90m, "Failed",    "REF609035", "ACC-002001", 25, "ZWG"),
            (3,  "BillPay",  4791.65m, 16.59m, "Reversed",  "REF339741", "ACC-002000", 18, "ZWG"),
            (1,  "Purchase", 3274.55m,  1.69m, "Reversed",  "REF394718", "ACC-002000", 12, "ZWG"),
            (14, "P2P",       931.43m, 22.85m, "Reversed",  "REF074803", "ACC-002004", 19, "ZWG"),
            (3,  "BillPay",  3162.06m, 23.67m, "Pending",   "REF073416", "ACC-002018", 4,  "ZWG"),
            (10, "Transfer", 3139.14m, 22.35m, "Pending",   "REF618126", "ACC-002017", 17, "ZWG"),
            (3,  "Transfer", 1113.32m,  8.19m, "Failed",    "REF777283", "ACC-002016", 27, "ZWG"),
            (18, "CashOut",  4060.76m, 21.20m, "Pending",   "REF992851", "ACC-002017", 19, "ZWG"),
            (0,  "Transfer", 2394.30m,  5.06m, "Reversed",  "REF308295", "ACC-002010", 26, "USD"),
            (8,  "BillPay",  3507.08m, 17.42m, "Completed", "REF763068", "ACC-002018", 11, "ZWG"),
            (0,  "Transfer",  452.05m, 13.08m, "Completed", "REF362208", "ACC-002012", 23, "ZWG"),
            (3,  "BillPay",  2244.11m,  8.39m, "Reversed",  "REF450980", "ACC-002012", 27, "USD"),
            (9,  "Transfer", 1505.56m, 19.77m, "Failed",    "REF098404", "ACC-002017", 16, "ZWG"),
            (5,  "CashOut",  2107.26m,  8.41m, "Reversed",  "REF168147", "ACC-002001", 4,  "ZWG"),
            (4,  "P2P",      4774.78m, 23.26m, "Completed", "REF469016", "ACC-002015", 18, "USD"),
            (9,  "CashOut",  2424.56m, 23.01m, "Failed",    "REF826615", "ACC-002018", 20, "ZWG"),
        };

        var txns = rows.Select((r, i) =>
        {
            var acctId = r.cur == "USD" ? UsdAcct(r.ai) : ZwgAcct(r.ai);
            return New<Transaction>(TxnUuid(i + 1), t =>
            {
                t.AccountId = acctId;
                t.Type = r.type;
                t.Amount = r.amount;
                t.Fee = r.fee;
                t.Status = r.status;
                t.Reference = r.reference;
                t.Description = $"{r.type} transaction";
                t.CounterpartyName = r.cp;
                t.CounterpartyPhone = $"+26377{i:0000000}";
                t.BalanceAfter = 0m; // simplified
                t.Currency = r.cur;
                t.TenantId = "unibank";
                t.CreatedAt = Ago(r.days);
                t.CompletedAt = r.status == "Completed" ? Ago(r.days) : null;
            });
        }).ToList();

        db.Transactions.AddRange(txns);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [Transactions] seeded {txns.Count}");
    }

    // ── 4. Disputes ────────────────────────────────────────────────────────
    // 15 rows (seed=77)

    private static async Task SeedDisputesAsync(UniBankDbContext db)
    {
        if (await db.Disputes.AnyAsync()) return;

        // (txnRow, acctCustomerIdx, type, status, resolution, refund, agentAdminIdx, filedDays, resolvedHours)
        // acctCustomerIdx refers to ZwgAcct(idx) unless noted
        var rows = new (int txn, int ai, DisputeType dtype, DisputeStatus dstatus, string res, decimal refund, int agent, int days, int hrs)[]
        {
            (1,  2,  DisputeType.Unauthorized,      DisputeStatus.Open,          "",         0m,      -1, 9,  0),
            (2,  17, DisputeType.ServiceNotReceived, DisputeStatus.Investigating, "",         0m,       0, 8,  0),
            (49, 15, DisputeType.IncorrectAmount,    DisputeStatus.Resolved,      "Refunded", 331.03m,  0, 29, 37),
            (34, 11, DisputeType.Other,              DisputeStatus.Resolved,      "Rejected",  73.21m,  6, 4,  16),
            (33, 3,  DisputeType.Other,              DisputeStatus.Open,          "",         0m,      -1, 2,  0),
            (12, 0,  DisputeType.IncorrectAmount,    DisputeStatus.Investigating, "",         0m,       6, 2,  0),
            (11, 13, DisputeType.ServiceNotReceived, DisputeStatus.Resolved,      "Refunded", 356.58m,  0, 8,  14),
            (7,  0,  DisputeType.ServiceNotReceived, DisputeStatus.Resolved,      "Rejected",   4.20m,  0, 19, 28),
            (32, 2,  DisputeType.IncorrectAmount,    DisputeStatus.Open,          "",         0m,      -1, 18, 0),
            (2,  15, DisputeType.Duplicate,          DisputeStatus.Investigating, "",         0m,       1, 5,  0),
            (42, 9,  DisputeType.Unauthorized,       DisputeStatus.Resolved,      "Refunded", 331.73m,  1, 0,  40),
            (38, 11, DisputeType.IncorrectAmount,    DisputeStatus.Resolved,      "Rejected", 246.56m,  6, 1,  46),
            (34, 11, DisputeType.Other,              DisputeStatus.Open,          "",         0m,      -1, 10, 0),
            (13, 15, DisputeType.ServiceNotReceived, DisputeStatus.Investigating, "",         0m,       6, 13, 0),
            (32, 6,  DisputeType.ServiceNotReceived, DisputeStatus.Resolved,      "Refunded", 110.91m,  0, 19, 41),
        };

        // Admin UUID map by index: 0=admin(1), 1=kyc(2), 2=fraud(3), 6=branch(7)
        Guid? AgentId(int idx) => idx switch
        {
            0 => AdminUuid(1),
            1 => AdminUuid(2),
            2 => AdminUuid(3),
            6 => AdminUuid(7),
            _ => null,
        };

        var disputes = rows.Select((r, i) => New<Dispute>(DisputeUuid(i + 1), d =>
        {
            d.TransactionId = TxnUuid(r.txn);
            d.AccountId = ZwgAcct(r.ai);
            d.Type = r.dtype;
            d.Description = "Customer reported issue with transaction";
            d.Status = r.dstatus;
            d.Resolution = string.IsNullOrEmpty(r.res) ? null : r.res;
            d.RefundAmount = r.refund > 0 ? r.refund : null;
            d.RefundCurrency = "ZWG";
            d.AdminUserId = AgentId(r.agent);
            d.ResolvedAt = r.dstatus == DisputeStatus.Resolved
                ? Ago(r.days).AddHours(r.hrs) : null;
            d.CreatedAt = Ago(r.days);
        })).ToList();

        db.Disputes.AddRange(disputes);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [Disputes] seeded {disputes.Count}");
    }

    // ── 5. Fraud Alerts ────────────────────────────────────────────────────
    // 12 rows (seed=55)

    private static async Task SeedFraudAlertsAsync(UniBankDbContext db)
    {
        if (await db.FraudAlerts.AnyAsync()) return;

        // (acctCustomerIdx, txnRow, alertType, severity, description, status, createdDays)
        var rows = new (int ai, int txn, string type, string severity, string desc, string status, int days)[]
        {
            (0,  12, "AmountAnomaly",   "High",   "Suspicious geoanomaly detected",     "New",       4),
            (0,  30, "GeoAnomaly",      "Medium", "Suspicious geoanomaly detected",     "Reviewed",  5),
            (1,  29, "DeviceAnomaly",   "Low",    "Suspicious patternmatch detected",   "Escalated", 13),
            (2,  46, "AmountAnomaly",   "High",   "Suspicious velocityanomaly detected","Dismissed", 9),
            (19, 39, "GeoAnomaly",      "Medium", "Suspicious velocityanomaly detected","New",       7),
            (19,  3, "PatternMatch",    "Low",    "Suspicious patternmatch detected",   "Reviewed",  7),
            (1,  47, "AmountAnomaly",   "High",   "Suspicious deviceanomaly detected",  "Escalated", 6),
            (1,  40, "DeviceAnomaly",   "Medium", "Suspicious amountanomaly detected",  "Dismissed", 1),
            (11,  3, "DeviceAnomaly",   "Low",    "Suspicious amountanomaly detected",  "New",       11),
            (19, 37, "DeviceAnomaly",   "High",   "Suspicious patternmatch detected",   "Reviewed",  0),
            (3,  14, "VelocityAnomaly", "Medium", "Suspicious velocityanomaly detected","Escalated", 10),
            (3,  38, "GeoAnomaly",      "Low",    "Suspicious velocityanomaly detected","Dismissed", 4),
        };

        var alerts = rows.Select((r, i) => New<FraudAlert>(FraudUuid(i + 1), fa =>
        {
            fa.AccountId = ZwgAcct(r.ai);
            fa.TransactionId = TxnUuid(r.txn);
            fa.AlertType = r.type;
            fa.Severity = r.severity;
            fa.Description = r.desc;
            fa.Status = r.status;
            fa.TenantId = "unibank";
            fa.CreatedAt = Ago(r.days);
        })).ToList();

        db.FraudAlerts.AddRange(alerts);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [FraudAlerts] seeded {alerts.Count}");
    }

    // ── 6. KYC Documents ──────────────────────────────────────────────────
    // 6 rows (seed=33)

    private static async Task SeedKycDocumentsAsync(UniBankDbContext db)
    {
        if (await db.KycDocuments.AnyAsync()) return;

        var rows = new (int ai, string name, int submittedDays, string aiDecision)[]
        {
            (0,  "tendai_moyo",          0, "AutoApproved"),
            (1,  "chiedza_mutasa",        2, "AutoApproved"),
            (2,  "farai_chikwanha",       5, "AutoApproved"),
            (3,  "rudo_nyamupfukudza",    4, "Rejected"),
            (4,  "blessing_chikowore",    5, "AutoApproved"),
            (8,  "simba_jongwe",          5, "AutoApproved"),
        };

        var docs = rows.Select((r, i) => New<KycDocument>(KycUuid(i + 1), k =>
        {
            k.AccountId = ZwgAcct(r.ai);
            k.DocumentType = "NationalId";
            k.FileName = $"kyc_{r.name}.jpg";
            k.ContentType = "image/jpeg";
            k.FileSizeBytes = 180000 + (i * 7300);
            k.FilePath = $"/kyc/demo/kyc_{r.name}.jpg";
            k.EncryptionKeyRef = "demo-key-ref";
            k.ChecksumSha256 = $"{i + 1:00000000000000000000000000000000000000000000000000000000000000}";
            k.Status = r.aiDecision == "Rejected" ? "rejected" : "approved";
            k.TenantId = "unibank";
            k.VerifiedAt = r.aiDecision != "Rejected" ? Ago(r.submittedDays) : null;
            k.CreatedAt = Ago(r.submittedDays);
        })).ToList();

        db.KycDocuments.AddRange(docs);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [KycDocuments] seeded {docs.Count}");
    }

    // ── 7. Loans ──────────────────────────────────────────────────────────
    // 5 rows (seed=88)

    private static async Task SeedLoansAsync(UniBankDbContext db)
    {
        if (await db.Set<Loan>().AnyAsync()) return;

        // (customerIdx, principal, rate, tenure, monthlyPayment, purpose, creditScore, appliedDays)
        var rows = new (int ai, decimal principal, decimal rate, int tenure, decimal monthly, string purpose, int score, int days)[]
        {
            (0,  5186m, 18.5m,  6, 910m, "Business",         701, 6),
            (1,  7607m, 21.0m, 18, 490m, "Education",        588, 0),
            (2,  7535m, 24.0m, 24, 380m, "Medical",          737, 5),
            (3,  1382m, 18.5m, 12, 130m, "Home Improvement",  629, 4),
            (4,  2180m, 21.0m, 18, 140m, "Agriculture",      533, 6),
        };

        var references = new[] { "LOAN-006000", "LOAN-006001", "LOAN-006002", "LOAN-006003", "LOAN-006004" };

        var loans = rows.Select((r, i) => New<Loan>(LoanUuid(i + 1), l =>
        {
            l.AccountId = ZwgAcct(r.ai);
            l.Principal = r.principal;
            l.OutstandingBalance = r.principal;
            l.InterestRate = r.rate;
            l.TenureMonths = r.tenure;
            l.MonthlyPayment = r.monthly;
            l.Purpose = r.purpose;
            l.Status = "Pending";
            l.CreditScore = r.score;
            l.PaymentsMade = 0;
            l.Reference = references[i];
            l.Currency = "ZWG";
            l.TenantId = "unibank";
            l.CreatedAt = Ago(r.days);
        })).ToList();

        db.Set<Loan>().AddRange(loans);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [Loans] seeded {loans.Count}");
    }

    // ── 8. Merchants ──────────────────────────────────────────────────────
    // 8 rows (Merchants.jsx STUB_MERCHANTS)

    private static async Task SeedMerchantsAsync(UniBankDbContext db)
    {
        if (await db.Merchants.AnyAsync()) return;

        var rows = new (string code, int ai, string name, string type, string cat, string addr, string status, string kycStatus, int? activatedDaysAgo, int createdDaysAgo)[]
        {
            ("M001", 0,  "OK Supermarket - Borrowdale", "Retail",    "Retail",          "55 Borrowdale Rd, Harare",          "Active",    "Approved", 282, 282),
            ("M002", 1,  "TM Pick n Pay - Eastgate",    "Retail",    "Retail",          "Eastgate Mall, Harare",             "Active",    "Approved", 247, 247),
            ("M003", 2,  "Chicken Inn - Samora",         "FoodBev",   "Food & Beverage", "Samora Machel Ave, Harare",         "Active",    "Approved", 226, 226),
            ("M004", 3,  "N. Richards Pharmacy",         "Health",    "Health",          "12 Park St, Harare",                "Suspended", "Approved", 205, 205),
            ("M005", 4,  "Zuva Fuel - Msasa",            "Fuel",      "Fuel",            "Msasa Industrial, Harare",          "Active",    "Approved", 163, 163),
            ("M006", 5,  "Edgars - Joina City",          "Clothing",  "Clothing",        "Joina City Mall, Harare",           "Pending",   "Pending",  null, 4),
            ("M007", 6,  "Bon Marche - Avondale",        "Retail",    "Retail",          "Avondale Shopping Centre, Harare",  "Active",    "Approved", 320, 320),
            ("M008", 7,  "Delta Beverages - Depot",      "Wholesale", "Wholesale",       "Workington Industrial, Harare",     "Closed",    "Approved", 495, 495),
        };

        var merchants = rows.Select((r, i) => New<Merchant>(MerchantUuid(i + 1), m =>
        {
            m.MerchantCode = r.code;
            m.OwnerAccountId = ZwgAcct(r.ai);
            m.BusinessName = r.name;
            m.BusinessType = r.type;
            m.CategoryCode = r.cat;
            m.BusinessAddress = r.addr;
            m.IsAgent = false;
            m.AgentTermsAccepted = false;
            m.Status = r.status;
            m.KycStatus = r.kycStatus;
            m.TenantId = "unibank";
            m.ActivatedAt = r.activatedDaysAgo.HasValue ? Ago(r.activatedDaysAgo.Value) : null;
            m.CreatedAt = Ago(r.createdDaysAgo);
        })).ToList();

        db.Merchants.AddRange(merchants);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [Merchants] seeded {merchants.Count}");
    }

    // ── 9. Bill Providers ─────────────────────────────────────────────────

    private static async Task SeedBillProvidersAsync(UniBankDbContext db)
    {
        if (await db.BillProviders.AnyAsync()) return;

        var rows = new (string name, string code, string cat, bool meter, bool acctNum, decimal min, decimal max)[]
        {
            ("ZESA Holdings",                      "ZESA",   "Utilities", true,  false, 5m,   50000m),
            ("TelOne",                             "TELONE", "Telecom",   false, true,  2m,   10000m),
            ("NetOne",                             "NETONE", "Telecom",   false, true,  1m,    5000m),
            ("Econet Wireless",                    "ECONET", "Telecom",   false, true,  1m,    5000m),
            ("Zimbabwe National Water Authority",  "ZINWA",  "Utilities", false, true,  5m,   20000m),
        };

        var providers = rows.Select((r, i) => New<BillProvider>(BillUuid(i + 1), p =>
        {
            p.Name = r.name;
            p.Code = r.code;
            p.Category = r.cat;
            p.RequiresMeterNumber = r.meter;
            p.RequiresAccountNumber = r.acctNum;
            p.MinAmount = r.min;
            p.MaxAmount = r.max;
            p.Currency = "ZWG";
            p.Status = "active";
            p.CountryCode = "ZW";
            p.TenantId = "unibank";
        })).ToList();

        db.BillProviders.AddRange(providers);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [BillProviders] seeded {providers.Count}");
    }

    // ── 10. System Configs ─────────────────────────────────────────────────
    // 22 rows (SystemConfig.jsx INITIAL_CONFIGS)

    private static async Task SeedSystemConfigsAsync(UniBankDbContext db)
    {
        if (await db.SystemConfigs.AnyAsync()) return;

        var rows = new (string key, string value)[]
        {
            ("card.bin_prefix",                 "\"6275\""),
            ("card.pan_length",                 "\"16\""),
            ("account.daily_limit_zwg",         "\"50000\""),
            ("account.daily_limit_usd",         "\"10000\""),
            ("account.monthly_limit_zwg",       "\"200000\""),
            ("account.monthly_limit_usd",       "\"50000\""),
            ("otp.ttl_seconds",                 "\"300\""),
            ("otp.max_attempts",                "\"5\""),
            ("pin.max_failed_attempts",         "\"5\""),
            ("pin.lockout_minutes",             "\"30\""),
            ("kyc.face_match_auto_approve",     "\"0.80\""),
            ("kyc.face_match_reject",           "\"0.40\""),
            ("fraud.velocity_limit_count",      "\"10\""),
            ("fraud.velocity_window_minutes",   "\"60\""),
            ("fraud.high_value_threshold_zwg",  "\"100000\""),
            ("fraud.high_value_threshold_usd",  "\"5000\""),
            ("loan.max_tenure_months",          "\"48\""),
            ("loan.min_credit_score",           "\"200\""),
            ("loan.income_variance_threshold",  "\"10\""),
            ("switch.gateway_url",              "\"http://synergy-switch:5002\""),
            ("ai.ollama_url",                   "\"http://unibank-ollama:11434\""),
            ("ai.model_name",                   "\"qwen3-vl\""),
        };

        var configs = rows.Select((r, i) => New<SystemConfig>(CfgUuid(i + 1), c =>
        {
            c.Key = r.key;
            c.ValueJson = r.value;
            c.TenantId = null;
        })).ToList();

        db.SystemConfigs.AddRange(configs);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [SystemConfigs] seeded {configs.Count}");
    }

    // ── 11. Audit Logs ────────────────────────────────────────────────────
    // 60 rows (seed=66)

    private static async Task SeedAuditLogsAsync(UniBankDbContext db)
    {
        if (await db.AuditLogs.AnyAsync()) return;

        // (adminIdx 1-7, action, entityId, hoursAgo, ip)
        var rows = new (int adm, string action, string entity, double hrs, string ip)[]
        {
            (1,"Account Action",    "ACC-001017", 45,  "192.168.1.42"),
            (4,"Login",             "ACC-001016", 140, "192.168.1.176"),
            (3,"KYC Review",        "ACC-001016", 47,  "192.168.1.135"),
            (7,"KYC Review",        "ACC-001010", 51,  "192.168.1.105"),
            (4,"KYC Review",        "ACC-001017", 68,  "192.168.1.216"),
            (6,"Login",             "ACC-001003", 76,  "192.168.1.71"),
            (1,"KYC Review",        "ACC-001006", 22,  "192.168.1.148"),
            (5,"Account Action",    "ACC-001018", 68,  "192.168.1.29"),
            (3,"Config Change",     "ACC-001013", 121, "192.168.1.1"),
            (6,"Account Action",    "ACC-001007", 167, "192.168.1.205"),
            (5,"Dispute Resolution","ACC-001013", 24,  "192.168.1.181"),
            (7,"Login",             "ACC-001004", 1,   "192.168.1.254"),
            (5,"KYC Review",        "ACC-001001", 9,   "192.168.1.31"),
            (5,"Dispute Resolution","ACC-001019", 92,  "192.168.1.221"),
            (1,"KYC Review",        "ACC-001005", 52,  "192.168.1.245"),
            (5,"Login",             "ACC-001006", 43,  "192.168.1.212"),
            (5,"KYC Review",        "ACC-001000", 73,  "192.168.1.78"),
            (4,"Dispute Resolution","ACC-001017", 161, "192.168.1.165"),
            (3,"KYC Review",        "ACC-001003", 147, "192.168.1.4"),
            (6,"Login",             "ACC-001010", 130, "192.168.1.106"),
            (4,"Config Change",     "ACC-001019", 129, "192.168.1.37"),
            (6,"Config Change",     "ACC-001003", 45,  "192.168.1.229"),
            (1,"KYC Review",        "ACC-001012", 132, "192.168.1.61"),
            (5,"Dispute Resolution","ACC-001000", 28,  "192.168.1.155"),
            (1,"Login",             "ACC-001003", 57,  "192.168.1.197"),
            (6,"Config Change",     "ACC-001016", 157, "192.168.1.244"),
            (4,"Login",             "ACC-001018", 162, "192.168.1.118"),
            (5,"Config Change",     "ACC-001014", 126, "192.168.1.206"),
            (7,"Login",             "ACC-001014", 130, "192.168.1.229"),
            (7,"KYC Review",        "ACC-001005", 107, "192.168.1.62"),
            (5,"Dispute Resolution","ACC-001006", 30,  "192.168.1.18"),
            (4,"KYC Review",        "ACC-001019", 4,   "192.168.1.1"),
            (3,"Dispute Resolution","ACC-001013", 69,  "192.168.1.222"),
            (4,"Login",             "ACC-001007", 89,  "192.168.1.21"),
            (5,"Dispute Resolution","ACC-001001", 118, "192.168.1.176"),
            (1,"Account Action",    "ACC-001018", 115, "192.168.1.13"),
            (1,"Config Change",     "ACC-001007", 82,  "192.168.1.3"),
            (2,"Config Change",     "ACC-001019", 85,  "192.168.1.208"),
            (1,"KYC Review",        "ACC-001012", 161, "192.168.1.225"),
            (4,"Account Action",    "ACC-001015", 167, "192.168.1.110"),
            (4,"KYC Review",        "ACC-001003", 166, "192.168.1.47"),
            (1,"Dispute Resolution","ACC-001019", 138, "192.168.1.83"),
            (1,"Config Change",     "ACC-001000", 27,  "192.168.1.95"),
            (2,"Config Change",     "ACC-001012", 160, "192.168.1.67"),
            (4,"Account Action",    "ACC-001011", 50,  "192.168.1.22"),
            (2,"Login",             "ACC-001000", 28,  "192.168.1.127"),
            (2,"Account Action",    "ACC-001018", 31,  "192.168.1.184"),
            (3,"KYC Review",        "ACC-001008", 140, "192.168.1.246"),
            (2,"Login",             "ACC-001017", 40,  "192.168.1.26"),
            (7,"Config Change",     "ACC-001002", 102, "192.168.1.173"),
            (1,"Login",             "ACC-001000", 0,   "192.168.1.17"),
            (4,"Login",             "ACC-001018", 91,  "192.168.1.52"),
            (6,"Account Action",    "ACC-001004", 66,  "192.168.1.108"),
            (4,"Login",             "ACC-001001", 58,  "192.168.1.169"),
            (6,"Dispute Resolution","ACC-001014", 129, "192.168.1.174"),
            (6,"KYC Review",        "ACC-001014", 148, "192.168.1.159"),
            (6,"Account Action",    "ACC-001013", 150, "192.168.1.28"),
            (1,"Login",             "ACC-001005", 112, "192.168.1.172"),
            (5,"Dispute Resolution","ACC-001004", 163, "192.168.1.107"),
            (5,"Dispute Resolution","ACC-001017", 157, "192.168.1.116"),
        };

        var logs = rows.Select((r, i) => New<AuditLog>(AuditUuid(i + 1), al =>
        {
            al.AdminUserId = AdminUuid(r.adm);
            al.Action = r.action;
            al.EntityType = "Account";
            al.EntityId = r.entity;
            al.Details = """{"browser":"Chrome 130"}""";
            al.IpAddress = r.ip;
            al.CreatedAt = AgoH(r.hrs);
        })).ToList();

        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync();
        Console.WriteLine($"  [AuditLogs] seeded {logs.Count}");
    }

    // ── Reflection helper — sets Id on protected setter ───────────────────

    private static T New<T>(Guid id, Action<T> configure) where T : class, new()
    {
        var entity = new T();
        // Set Id via reflection since AggregateRoot uses a protected setter
        var idProp = typeof(T).GetProperty("Id")
            ?? entity.GetType().BaseType?.GetProperty("Id")
            ?? entity.GetType().BaseType?.BaseType?.GetProperty("Id");
        idProp?.SetValue(entity, id);
        configure(entity);
        return entity;
    }
}
