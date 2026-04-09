-- =============================================================================
-- 003_demo_seed.sql  —  Comprehensive demo data matching bank-client stub data
-- Generated: 2026-03-24
-- UUID scheme: XX000000-0000-4000-8000-NNNNNNNNNNNN
--   01=tenant  02=admin_user  03=account  04=transaction  05=dispute
--   06=fraud_alert  07=kyc_document  08=loan  09=merchant  0a=bill_provider
--   0b=system_config  0c=audit_log
-- RNG seeds: customers=42, transactions=99, disputes=77, fraudAlerts=55,
--            kycQueue=33, loans=88, auditTrail=66
-- =============================================================================

BEGIN;

-- =============================================================================
-- 0. TENANT
-- =============================================================================
INSERT INTO bank.tenants
  ("Id","Name","Code","SchemaName","ConfigJson","BrandingJson","Status","MaxUsers","CountryCode","CurrencyCode","Timezone","CreatedAt","UpdatedAt")
VALUES
  ('01000000-0000-4000-8000-000000000001','UniBank Zimbabwe','unibank','bank','{}','{}','Active',100000,'ZW','ZWG','Africa/Harare',NOW(),NOW())
ON CONFLICT ("Code") DO NOTHING;

-- =============================================================================
-- 1. ADMIN USERS  (7 users — password placeholder hashes, password = username)
-- =============================================================================
INSERT INTO bank.admin_users
  ("Id",username,password_hash,email,full_name,role,tenant_id,is_active,created_at)
VALUES
  ('02000000-0000-4000-8000-000000000001','admin',      '$2a$10$demoSeedHashAdminXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','admin@unibank.co.zw',      'System Administrator','Admin',             'unibank',true, NOW()-INTERVAL '365 days'),
  ('02000000-0000-4000-8000-000000000002','kyc',        '$2a$10$demoSeedHashKycXXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','kyc@unibank.co.zw',        'KYC Officer',         'KycOfficer',        'unibank',true, NOW()-INTERVAL '300 days'),
  ('02000000-0000-4000-8000-000000000003','fraud',      '$2a$10$demoSeedHashFraudXXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','fraud@unibank.co.zw',      'Fraud Analyst',       'FraudAnalyst',      'unibank',true, NOW()-INTERVAL '280 days'),
  ('02000000-0000-4000-8000-000000000004','support',    '$2a$10$demoSeedHashSupportXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','support@unibank.co.zw',    'Customer Support',    'CustomerService',   'unibank',true, NOW()-INTERVAL '250 days'),
  ('02000000-0000-4000-8000-000000000005','loans',      '$2a$10$demoSeedHashLoansXXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','loans@unibank.co.zw',      'Loan Officer',        'LoanOfficer',       'unibank',true, NOW()-INTERVAL '220 days'),
  ('02000000-0000-4000-8000-000000000006','compliance', '$2a$10$demoSeedHashComplyXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','compliance@unibank.co.zw', 'Compliance Officer',  'ComplianceOfficer', 'unibank',true, NOW()-INTERVAL '200 days'),
  ('02000000-0000-4000-8000-000000000007','branch',     '$2a$10$demoSeedHashBranchXXXuJ3NZ6hqKDR4k3vMt7lYwP2nQsR8gT.','branch@unibank.co.zw',     'Branch Manager',      'BranchManager',     'unibank',true, NOW()-INTERVAL '180 days')
ON CONFLICT (username) DO NOTHING;

-- =============================================================================
-- 2. ACCOUNTS — 20 customers × 2 currencies = 40 rows  (seed=42)
--    Row numbering: customer_index*2+1=ZWG, customer_index*2+2=USD
--    Status: Active→active, Suspended→suspended, Frozen→frozen, Closed→closed
-- =============================================================================
INSERT INTO bank.accounts
  ("Id",phone,phone_country_code,pin_hash,tenant_id,status,kyc_level,
   daily_limit,monthly_limit,balance,available_balance,currency,
   first_name,last_name,email,national_id,last_login_at,created_at)
VALUES
-- 0: Tendai Moyo | Active | kyc=2
('03000000-0000-4000-8000-000000000001','+263770003287','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,50000,200000,36771.18,36771.18,'ZWG','Tendai',   'Moyo',          'tendai@email.co.zw',    '63-9758737T50',NOW()-INTERVAL '2 days', NOW()-INTERVAL '67 days'),
('03000000-0000-4000-8000-000000000002','+263770003287','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,10000, 50000,  526.61,  526.61,'USD','Tendai',   'Moyo',          'tendai@email.co.zw',    '63-9758737T50',NOW()-INTERVAL '2 days', NOW()-INTERVAL '67 days'),
-- 1: Chiedza Mutasa | Active | kyc=1
('03000000-0000-4000-8000-000000000003','+263775304489','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000, 5354.36, 5354.36,'ZWG','Chiedza',  'Mutasa',        'chiedza@email.co.zw',   '63-2453886T24',NOW()-INTERVAL '6 days', NOW()-INTERVAL '163 days'),
('03000000-0000-4000-8000-000000000004','+263775304489','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000, 1630.98, 1630.98,'USD','Chiedza',  'Mutasa',        'chiedza@email.co.zw',   '63-2453886T24',NOW()-INTERVAL '6 days', NOW()-INTERVAL '163 days'),
-- 2: Farai Chikwanha | Active | kyc=1
('03000000-0000-4000-8000-000000000005','+263771882741','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000, 5172.87, 5172.87,'ZWG','Farai',    'Chikwanha',     'farai@email.co.zw',     '63-7370189T07',NOW()-INTERVAL '11 days',NOW()-INTERVAL '96 days'),
('03000000-0000-4000-8000-000000000006','+263771882741','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000, 1614.75, 1614.75,'USD','Farai',    'Chikwanha',     'farai@email.co.zw',     '63-7370189T07',NOW()-INTERVAL '11 days',NOW()-INTERVAL '96 days'),
-- 3: Rudo Nyamupfukudza | Suspended | kyc=0
('03000000-0000-4000-8000-000000000007','+263775390093','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',0,50000,200000,43126.27,43126.27,'ZWG','Rudo',     'Nyamupfukudza', 'rudo@email.co.zw',      '63-2334748T01',NOW()-INTERVAL '6 days', NOW()-INTERVAL '84 days'),
('03000000-0000-4000-8000-000000000008','+263775390093','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',0,10000, 50000,  929.19,  929.19,'USD','Rudo',     'Nyamupfukudza', 'rudo@email.co.zw',      '63-2334748T01',NOW()-INTERVAL '6 days', NOW()-INTERVAL '84 days'),
-- 4: Blessing Chikowore | Frozen | kyc=3
('03000000-0000-4000-8000-000000000009','+263770230257','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   3,50000,200000,45771.96,45771.96,'ZWG','Blessing', 'Chikowore',     'blessing@email.co.zw',  '63-6235701T34',NOW()-INTERVAL '10 days',NOW()-INTERVAL '102 days'),
('03000000-0000-4000-8000-00000000000a','+263770230257','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   3,10000, 50000, 1571.67, 1571.67,'USD','Blessing', 'Chikowore',     'blessing@email.co.zw',  '63-6235701T34',NOW()-INTERVAL '10 days',NOW()-INTERVAL '102 days'),
-- 5: Tatenda Mashava | Closed | kyc=1
('03000000-0000-4000-8000-00000000000b','+263773756331','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   1,50000,200000,17586.84,17586.84,'ZWG','Tatenda',  'Mashava',       'tatenda@email.co.zw',   '63-5286581T15',NOW()-INTERVAL '0 days', NOW()-INTERVAL '65 days'),
('03000000-0000-4000-8000-00000000000c','+263773756331','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   1,10000, 50000, 1281.89, 1281.89,'USD','Tatenda',  'Mashava',       'tatenda@email.co.zw',   '63-5286581T15',NOW()-INTERVAL '0 days', NOW()-INTERVAL '65 days'),
-- 6: Nyasha Dube | Active | kyc=1
('03000000-0000-4000-8000-00000000000d','+263774538185','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000, 9639.03, 9639.03,'ZWG','Nyasha',   'Dube',          'nyasha@email.co.zw',    '63-0891291T98',NOW()-INTERVAL '7 days', NOW()-INTERVAL '177 days'),
('03000000-0000-4000-8000-00000000000e','+263774538185','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000,  129.47,  129.47,'USD','Nyasha',   'Dube',          'nyasha@email.co.zw',    '63-0891291T98',NOW()-INTERVAL '7 days', NOW()-INTERVAL '177 days'),
-- 7: Rumbidzai Hwami | Active | kyc=2
('03000000-0000-4000-8000-00000000000f','+263774337300','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,50000,200000, 9337.01, 9337.01,'ZWG','Rumbidzai','Hwami',          'rumbidzai@email.co.zw', '63-6842624T39',NOW()-INTERVAL '2 days', NOW()-INTERVAL '179 days'),
('03000000-0000-4000-8000-000000000010','+263774337300','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,10000, 50000, 1087.40, 1087.40,'USD','Rumbidzai','Hwami',          'rumbidzai@email.co.zw', '63-6842624T39',NOW()-INTERVAL '2 days', NOW()-INTERVAL '179 days'),
-- 8: Simba Jongwe | Active | kyc=2
('03000000-0000-4000-8000-000000000011','+263774389686','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,50000,200000, 8903.47, 8903.47,'ZWG','Simba',    'Jongwe',        'simba@email.co.zw',     '63-0296979T13',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '145 days'),
('03000000-0000-4000-8000-000000000012','+263774389686','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   2,10000, 50000, 1621.56, 1621.56,'USD','Simba',    'Jongwe',        'simba@email.co.zw',     '63-0296979T13',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '145 days'),
-- 9: Kudzi Mhike | Suspended | kyc=1
('03000000-0000-4000-8000-000000000013','+263776374355','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',1,50000,200000,12469.53,12469.53,'ZWG','Kudzi',    'Mhike',         'kudzi@email.co.zw',     '63-9835989T34',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '93 days'),
('03000000-0000-4000-8000-000000000014','+263776374355','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',1,10000, 50000, 1013.45, 1013.45,'USD','Kudzi',    'Mhike',         'kudzi@email.co.zw',     '63-9835989T34',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '93 days'),
-- 10: Tawanda Nzira | Frozen | kyc=1
('03000000-0000-4000-8000-000000000015','+263777683265','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   1,50000,200000,33322.17,33322.17,'ZWG','Tawanda',  'Nzira',         'tawanda@email.co.zw',   '63-9214295T46',NOW()-INTERVAL '6 days', NOW()-INTERVAL '83 days'),
('03000000-0000-4000-8000-000000000016','+263777683265','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   1,10000, 50000, 1830.25, 1830.25,'USD','Tawanda',  'Nzira',         'tawanda@email.co.zw',   '63-9214295T46',NOW()-INTERVAL '6 days', NOW()-INTERVAL '83 days'),
-- 11: Grace Mapfumo | Closed | kyc=1
('03000000-0000-4000-8000-000000000017','+263779772308','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   1,50000,200000,39709.24,39709.24,'ZWG','Grace',    'Mapfumo',       'grace@email.co.zw',     '63-8267703T52',NOW()-INTERVAL '7 days', NOW()-INTERVAL '133 days'),
('03000000-0000-4000-8000-000000000018','+263779772308','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   1,10000, 50000, 1731.03, 1731.03,'USD','Grace',    'Mapfumo',       'grace@email.co.zw',     '63-8267703T52',NOW()-INTERVAL '7 days', NOW()-INTERVAL '133 days'),
-- 12: Tinashe Gumbo | Active | kyc=1
('03000000-0000-4000-8000-000000000019','+263773073923','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000,15566.27,15566.27,'ZWG','Tinashe',  'Gumbo',         'tinashe@email.co.zw',   '63-0629195T48',NOW()-INTERVAL '9 days', NOW()-INTERVAL '102 days'),
('03000000-0000-4000-8000-00000000001a','+263773073923','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000,  893.15,  893.15,'USD','Tinashe',  'Gumbo',         'tinashe@email.co.zw',   '63-0629195T48',NOW()-INTERVAL '9 days', NOW()-INTERVAL '102 days'),
-- 13: Patience Mwale | Active | kyc=0
('03000000-0000-4000-8000-00000000001b','+263774562389','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   0,50000,200000,35052.03,35052.03,'ZWG','Patience', 'Mwale',         'patience@email.co.zw',  '63-1251519T42',NOW()-INTERVAL '13 days',NOW()-INTERVAL '80 days'),
('03000000-0000-4000-8000-00000000001c','+263774562389','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   0,10000, 50000,  776.52,  776.52,'USD','Patience', 'Mwale',         'patience@email.co.zw',  '63-1251519T42',NOW()-INTERVAL '13 days',NOW()-INTERVAL '80 days'),
-- 14: Lloyd Phiri | Active | kyc=1
('03000000-0000-4000-8000-00000000001d','+263772925215','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000,12183.51,12183.51,'ZWG','Lloyd',    'Phiri',         'lloyd@email.co.zw',     '63-0178186T47',NOW()-INTERVAL '13 days',NOW()-INTERVAL '127 days'),
('03000000-0000-4000-8000-00000000001e','+263772925215','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000,  731.33,  731.33,'USD','Lloyd',    'Phiri',         'lloyd@email.co.zw',     '63-0178186T47',NOW()-INTERVAL '13 days',NOW()-INTERVAL '127 days'),
-- 15: Edith Banda | Suspended | kyc=2
('03000000-0000-4000-8000-00000000001f','+263778215940','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',2,50000,200000, 6299.16, 6299.16,'ZWG','Edith',    'Banda',         'edith@email.co.zw',     '63-3916865T07',NOW()-INTERVAL '11 days',NOW()-INTERVAL '23 days'),
('03000000-0000-4000-8000-000000000020','+263778215940','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','suspended',2,10000, 50000,  800.63,  800.63,'USD','Edith',    'Banda',         'edith@email.co.zw',     '63-3916865T07',NOW()-INTERVAL '11 days',NOW()-INTERVAL '23 days'),
-- 16: Moses Sithole | Frozen | kyc=1
('03000000-0000-4000-8000-000000000021','+263775976281','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   1,50000,200000,12680.39,12680.39,'ZWG','Moses',    'Sithole',       'moses@email.co.zw',     '63-2042175T28',NOW()-INTERVAL '8 days', NOW()-INTERVAL '56 days'),
('03000000-0000-4000-8000-000000000022','+263775976281','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','frozen',   1,10000, 50000,  772.93,  772.93,'USD','Moses',    'Sithole',       'moses@email.co.zw',     '63-2042175T28',NOW()-INTERVAL '8 days', NOW()-INTERVAL '56 days'),
-- 17: Janet Sibanda | Closed | kyc=0
('03000000-0000-4000-8000-000000000023','+263779891232','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   0,50000,200000,33091.91,33091.91,'ZWG','Janet',    'Sibanda',       'janet@email.co.zw',     '63-3581377T22',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '141 days'),
('03000000-0000-4000-8000-000000000024','+263779891232','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','closed',   0,10000, 50000, 1028.36, 1028.36,'USD','Janet',    'Sibanda',       'janet@email.co.zw',     '63-3581377T22',NOW()-INTERVAL '1 day',  NOW()-INTERVAL '141 days'),
-- 18: Charles Ngwenya | Active | kyc=0
('03000000-0000-4000-8000-000000000025','+263772988140','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   0,50000,200000,47821.52,47821.52,'ZWG','Charles',  'Ngwenya',       'charles@email.co.zw',   '63-4892821T36',NOW()-INTERVAL '7 days', NOW()-INTERVAL '106 days'),
('03000000-0000-4000-8000-000000000026','+263772988140','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   0,10000, 50000, 1451.49, 1451.49,'USD','Charles',  'Ngwenya',       'charles@email.co.zw',   '63-4892821T36',NOW()-INTERVAL '7 days', NOW()-INTERVAL '106 days'),
-- 19: Susan Tembo | Active | kyc=1
('03000000-0000-4000-8000-000000000027','+263772932938','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,50000,200000,20424.32,20424.32,'ZWG','Susan',    'Tembo',         'susan@email.co.zw',     '63-1086290T72',NOW()-INTERVAL '4 days', NOW()-INTERVAL '57 days'),
('03000000-0000-4000-8000-000000000028','+263772932938','+263','$2a$10$demoPinXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX','unibank','active',   1,10000, 50000,  858.61,  858.61,'USD','Susan',    'Tembo',         'susan@email.co.zw',     '63-1086290T72',NOW()-INTERVAL '4 days', NOW()-INTERVAL '57 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- UUID REFERENCE MAP (for FK lookups below)
--   Customer 0  Tendai Moyo      ZWG=03..01  USD=03..02
--   Customer 1  Chiedza Mutasa   ZWG=03..03  USD=03..04
--   Customer 2  Farai Chikwanha  ZWG=03..05  USD=03..06
--   Customer 3  Rudo             ZWG=03..07  USD=03..08
--   Customer 4  Blessing         ZWG=03..09  USD=03..0a
--   Customer 5  Tatenda          ZWG=03..0b  USD=03..0c
--   Customer 6  Nyasha Dube      ZWG=03..0d  USD=03..0e
--   Customer 7  Rumbidzai        ZWG=03..0f  USD=03..10
--   Customer 8  Simba            ZWG=03..11  USD=03..12
--   Customer 9  Kudzi            ZWG=03..13  USD=03..14
--   Customer 10 Tawanda          ZWG=03..15  USD=03..16
--   Customer 11 Grace            ZWG=03..17  USD=03..18
--   Customer 12 Tinashe          ZWG=03..19  USD=03..1a
--   Customer 13 Patience         ZWG=03..1b  USD=03..1c
--   Customer 14 Lloyd            ZWG=03..1d  USD=03..1e
--   Customer 15 Edith            ZWG=03..1f  USD=03..20
--   Customer 16 Moses            ZWG=03..21  USD=03..22
--   Customer 17 Janet            ZWG=03..23  USD=03..24
--   Customer 18 Charles          ZWG=03..25  USD=03..26
--   Customer 19 Susan            ZWG=03..27  USD=03..28
-- =============================================================================

-- =============================================================================
-- 3. TRANSACTIONS — 50 rows (seed=99)
-- =============================================================================
INSERT INTO bank.transactions
  ("Id",account_id,type,amount,fee,status,reference,description,
   counterparty_name,counterparty_phone,balance_after,currency,tenant_id,created_at)
VALUES
('04000000-0000-4000-8000-000000000001','03000000-0000-4000-8000-000000000001','BillPay', 2031.82,18.60,'Failed',   'REF657416','Bill payment',    'ACC-002004','+263770000004',34739.36,'ZWG','unibank',NOW()-INTERVAL '7 days'),
('04000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-000000000027','Transfer',2113.19, 7.32,'Pending',  'REF443788','Transfer',        'ACC-002015','+263770000015',18311.13,'ZWG','unibank',NOW()-INTERVAL '28 days'),
('04000000-0000-4000-8000-000000000003','03000000-0000-4000-8000-00000000000d','Transfer', 566.06,19.26,'Completed','REF104524','Transfer',        'ACC-002014','+263770000014', 9072.97,'ZWG','unibank',NOW()-INTERVAL '5 days'),
('04000000-0000-4000-8000-000000000004','03000000-0000-4000-8000-000000000006','P2P',     3494.93,21.80,'Pending',  'REF631217','P2P transfer',    'ACC-002017','+263770000017',-1880.18,'USD','unibank',NOW()-INTERVAL '3 days'),
('04000000-0000-4000-8000-000000000005','03000000-0000-4000-8000-000000000014','CashOut', 2911.14,12.51,'Completed','REF531836','Cash out',        'ACC-002011','+263770000011',-1897.69,'USD','unibank',NOW()-INTERVAL '29 days'),
('04000000-0000-4000-8000-000000000006','03000000-0000-4000-8000-000000000023','CashOut', 1078.89,14.18,'Pending',  'REF165220','Cash out',        'ACC-002017','+263770000017',32013.02,'ZWG','unibank',NOW()-INTERVAL '0 days'),
('04000000-0000-4000-8000-000000000007','03000000-0000-4000-8000-000000000017','Purchase',2921.31,16.94,'Reversed', 'REF348305','Purchase',        'ACC-002019','+263770000019',36787.93,'ZWG','unibank',NOW()-INTERVAL '24 days'),
('04000000-0000-4000-8000-000000000008','03000000-0000-4000-8000-000000000018','CashIn',  2339.63,11.12,'Reversed', 'REF157015','Cash in',         'ACC-002019','+263770000019', 3731.03,'USD','unibank',NOW()-INTERVAL '11 days'),
('04000000-0000-4000-8000-000000000009','03000000-0000-4000-8000-000000000027','CashOut',  785.37,23.91,'Pending',  'REF886226','Cash out',        'ACC-002016','+263770000016',19638.95,'ZWG','unibank',NOW()-INTERVAL '28 days'),
('04000000-0000-4000-8000-00000000000a','03000000-0000-4000-8000-000000000004','BillPay', 4070.48,13.01,'Reversed', 'REF795995','Bill payment',    'ACC-002006','+263770000006',-2439.50,'USD','unibank',NOW()-INTERVAL '19 days'),
('04000000-0000-4000-8000-00000000000b','03000000-0000-4000-8000-000000000007','CashIn',  3272.21, 5.46,'Failed',   'REF438166','Cash in',         'ACC-002005','+263770000005',46398.48,'ZWG','unibank',NOW()-INTERVAL '27 days'),
('04000000-0000-4000-8000-00000000000c','03000000-0000-4000-8000-000000000017','P2P',      915.05,21.50,'Pending',  'REF816983','P2P transfer',    'ACC-002001','+263770000001',38794.19,'ZWG','unibank',NOW()-INTERVAL '22 days'),
('04000000-0000-4000-8000-00000000000d','03000000-0000-4000-8000-000000000001','Transfer',  75.17,16.52,'Pending',  'REF851831','Transfer',        'ACC-002015','+263770000015',36696.01,'ZWG','unibank',NOW()-INTERVAL '27 days'),
('04000000-0000-4000-8000-00000000000e','03000000-0000-4000-8000-000000000021','P2P',     2266.38, 4.89,'Pending',  'REF474361','P2P transfer',    'ACC-002012','+263770000012',10414.01,'ZWG','unibank',NOW()-INTERVAL '3 days'),
('04000000-0000-4000-8000-00000000000f','03000000-0000-4000-8000-000000000011','Purchase',3335.02, 8.68,'Pending',  'REF040157','Purchase',        'ACC-002018','+263770000018', 5568.45,'ZWG','unibank',NOW()-INTERVAL '28 days'),
('04000000-0000-4000-8000-000000000010','03000000-0000-4000-8000-000000000025','CashIn',  3879.81,15.14,'Pending',  'REF544503','Cash in',         'ACC-002009','+263770000009',51701.33,'ZWG','unibank',NOW()-INTERVAL '16 days'),
('04000000-0000-4000-8000-000000000011','03000000-0000-4000-8000-000000000015','CashOut',  608.92,20.23,'Pending',  'REF223243','Cash out',        'ACC-002001','+263770000001',32713.25,'ZWG','unibank',NOW()-INTERVAL '24 days'),
('04000000-0000-4000-8000-000000000012','03000000-0000-4000-8000-000000000028','Purchase', 288.28, 0.88,'Completed','REF052678','Purchase',        'ACC-002007','+263770000007',  570.33,'USD','unibank',NOW()-INTERVAL '15 days'),
('04000000-0000-4000-8000-000000000013','03000000-0000-4000-8000-00000000001a','Purchase',1178.83,12.82,'Pending',  'REF279644','Purchase',        'ACC-002019','+263770000019', -285.68,'USD','unibank',NOW()-INTERVAL '22 days'),
('04000000-0000-4000-8000-000000000014','03000000-0000-4000-8000-00000000001b','CashIn',  4692.93,20.71,'Reversed', 'REF185377','Cash in',         'ACC-002012','+263770000012',39744.96,'ZWG','unibank',NOW()-INTERVAL '14 days'),
('04000000-0000-4000-8000-000000000015','03000000-0000-4000-8000-000000000009','BillPay', 2435.38, 6.89,'Failed',   'REF205148','Bill payment',    'ACC-002018','+263770000018',43336.58,'ZWG','unibank',NOW()-INTERVAL '7 days'),
('04000000-0000-4000-8000-000000000016','03000000-0000-4000-8000-000000000003','CashOut', 1225.10, 1.43,'Reversed', 'REF271312','Cash out',        'ACC-002018','+263770000018', 4129.26,'ZWG','unibank',NOW()-INTERVAL '12 days'),
('04000000-0000-4000-8000-000000000017','03000000-0000-4000-8000-00000000001d','P2P',     2270.97,15.72,'Pending',  'REF100227','P2P transfer',    'ACC-002010','+263770000010', 9912.54,'ZWG','unibank',NOW()-INTERVAL '22 days'),
('04000000-0000-4000-8000-000000000018','03000000-0000-4000-8000-00000000001d','P2P',     3901.52,14.61,'Reversed', 'REF337665','P2P transfer',    'ACC-002003','+263770000003', 8281.99,'ZWG','unibank',NOW()-INTERVAL '7 days'),
('04000000-0000-4000-8000-000000000019','03000000-0000-4000-8000-00000000000c','Transfer',4291.71, 3.77,'Reversed', 'REF912547','Transfer',        'ACC-002004','+263770000004',-2010.82,'USD','unibank',NOW()-INTERVAL '12 days'),
('04000000-0000-4000-8000-00000000001a','03000000-0000-4000-8000-000000000003','CashOut',   30.82,14.96,'Pending',  'REF566928','Cash out',        'ACC-002007','+263770000007', 5323.54,'ZWG','unibank',NOW()-INTERVAL '9 days'),
('04000000-0000-4000-8000-00000000001b','03000000-0000-4000-8000-000000000007','CashIn',   460.12,15.91,'Pending',  'REF171365','Cash in',         'ACC-002002','+263770000002',43586.39,'ZWG','unibank',NOW()-INTERVAL '0 days'),
('04000000-0000-4000-8000-00000000001c','03000000-0000-4000-8000-00000000000b','CashOut', 3261.30,13.12,'Completed','REF909715','Cash out',        'ACC-002012','+263770000012',14325.54,'ZWG','unibank',NOW()-INTERVAL '24 days'),
('04000000-0000-4000-8000-00000000001d','03000000-0000-4000-8000-000000000009','P2P',     1701.72, 4.03,'Completed','REF509038','P2P transfer',    'ACC-002008','+263770000008',44070.24,'ZWG','unibank',NOW()-INTERVAL '15 days'),
('04000000-0000-4000-8000-00000000001e','03000000-0000-4000-8000-000000000001','BillPay', 2298.48, 3.16,'Completed','REF721653','Bill payment',    'ACC-002016','+263770000016',34472.70,'ZWG','unibank',NOW()-INTERVAL '27 days'),
('04000000-0000-4000-8000-00000000001f','03000000-0000-4000-8000-000000000025','Transfer',3547.80,14.69,'Completed','REF473763','Transfer',        'ACC-002011','+263770000011',44273.72,'ZWG','unibank',NOW()-INTERVAL '8 days'),
('04000000-0000-4000-8000-000000000020','03000000-0000-4000-8000-000000000014','P2P',     1054.51,15.92,'Failed',   'REF161480','P2P transfer',    'ACC-002000','+263770000000',  -41.06,'USD','unibank',NOW()-INTERVAL '1 day'),
('04000000-0000-4000-8000-000000000021','03000000-0000-4000-8000-000000000015','Purchase',1423.63, 9.95,'Pending',  'REF763146','Purchase',        'ACC-002004','+263770000004',31898.54,'ZWG','unibank',NOW()-INTERVAL '8 days'),
('04000000-0000-4000-8000-000000000022','03000000-0000-4000-8000-00000000001a','P2P',     1198.64, 2.84,'Pending',  'REF650011','P2P transfer',    'ACC-002014','+263770000014', -305.49,'USD','unibank',NOW()-INTERVAL '18 days'),
('04000000-0000-4000-8000-000000000023','03000000-0000-4000-8000-000000000015','P2P',      661.14, 8.90,'Failed',   'REF609035','P2P transfer',    'ACC-002001','+263770000001',32661.03,'ZWG','unibank',NOW()-INTERVAL '25 days'),
('04000000-0000-4000-8000-000000000024','03000000-0000-4000-8000-000000000007','BillPay', 4791.65,16.59,'Reversed', 'REF339741','Bill payment',    'ACC-002000','+263770000000',38334.62,'ZWG','unibank',NOW()-INTERVAL '18 days'),
('04000000-0000-4000-8000-000000000025','03000000-0000-4000-8000-000000000003','Purchase',3274.55, 1.69,'Reversed', 'REF394718','Purchase',        'ACC-002000','+263770000000', 2079.81,'ZWG','unibank',NOW()-INTERVAL '12 days'),
('04000000-0000-4000-8000-000000000026','03000000-0000-4000-8000-00000000001d','P2P',      931.43,22.85,'Reversed', 'REF074803','P2P transfer',    'ACC-002004','+263770000004',11252.08,'ZWG','unibank',NOW()-INTERVAL '19 days'),
('04000000-0000-4000-8000-000000000027','03000000-0000-4000-8000-000000000007','BillPay', 3162.06,23.67,'Pending',  'REF073416','Bill payment',    'ACC-002018','+263770000018',39964.21,'ZWG','unibank',NOW()-INTERVAL '4 days'),
('04000000-0000-4000-8000-000000000028','03000000-0000-4000-8000-000000000015','Transfer',3139.14,22.35,'Pending',  'REF618126','Transfer',        'ACC-002017','+263770000017',30183.03,'ZWG','unibank',NOW()-INTERVAL '17 days'),
('04000000-0000-4000-8000-000000000029','03000000-0000-4000-8000-000000000007','Transfer',1113.32, 8.19,'Failed',   'REF777283','Transfer',        'ACC-002016','+263770000016',42012.95,'ZWG','unibank',NOW()-INTERVAL '27 days'),
('04000000-0000-4000-8000-00000000002a','03000000-0000-4000-8000-000000000025','CashOut', 4060.76,21.20,'Pending',  'REF992851','Cash out',        'ACC-002017','+263770000017',43760.76,'ZWG','unibank',NOW()-INTERVAL '19 days'),
('04000000-0000-4000-8000-00000000002b','03000000-0000-4000-8000-000000000002','Transfer',2394.30, 5.06,'Reversed', 'REF308295','Transfer',        'ACC-002010','+263770000010',-1867.69,'USD','unibank',NOW()-INTERVAL '26 days'),
('04000000-0000-4000-8000-00000000002c','03000000-0000-4000-8000-000000000011','BillPay', 3507.08,17.42,'Completed','REF763068','Bill payment',    'ACC-002018','+263770000018', 5396.39,'ZWG','unibank',NOW()-INTERVAL '11 days'),
('04000000-0000-4000-8000-00000000002d','03000000-0000-4000-8000-000000000001','Transfer', 452.05,13.08,'Completed','REF362208','Transfer',        'ACC-002012','+263770000012',36319.13,'ZWG','unibank',NOW()-INTERVAL '23 days'),
('04000000-0000-4000-8000-00000000002e','03000000-0000-4000-8000-000000000008','BillPay', 2244.11, 8.39,'Reversed', 'REF450980','Bill payment',    'ACC-002012','+263770000012',-1314.92,'USD','unibank',NOW()-INTERVAL '27 days'),
('04000000-0000-4000-8000-00000000002f','03000000-0000-4000-8000-000000000013','Transfer',1505.56,19.77,'Failed',   'REF098404','Transfer',        'ACC-002017','+263770000017',10963.97,'ZWG','unibank',NOW()-INTERVAL '16 days'),
('04000000-0000-4000-8000-000000000030','03000000-0000-4000-8000-00000000000b','CashOut', 2107.26, 8.41,'Reversed', 'REF168147','Cash out',        'ACC-002001','+263770000001',15479.58,'ZWG','unibank',NOW()-INTERVAL '4 days'),
('04000000-0000-4000-8000-000000000031','03000000-0000-4000-8000-00000000000a','P2P',     4774.78,23.26,'Completed','REF469016','P2P transfer',    'ACC-002015','+263770000015',-3203.11,'USD','unibank',NOW()-INTERVAL '18 days'),
('04000000-0000-4000-8000-000000000032','03000000-0000-4000-8000-000000000013','CashOut', 2424.56,23.01,'Failed',   'REF826615','Cash out',        'ACC-002018','+263770000018',10044.97,'ZWG','unibank',NOW()-INTERVAL '20 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 4. DISPUTES — 15 rows (seed=77)
-- =============================================================================
INSERT INTO bank.disputes
  ("Id",transaction_id,account_id,type,description,status,resolution,
   refund_amount,refund_currency,admin_user_id,resolved_at,created_at)
VALUES
('05000000-0000-4000-8000-000000000001','04000000-0000-4000-8000-000000000001','03000000-0000-4000-8000-000000000005','Unauthorized',       'Customer reported issue with transaction','Open',         '',        0.00,  'ZWG',NULL,                                    NULL,                                        NOW()-INTERVAL '9 days'),
('05000000-0000-4000-8000-000000000002','04000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-000000000023','ServiceNotRendered',  'Customer reported issue with transaction','Investigating','',        0.00,  'ZWG','02000000-0000-4000-8000-000000000001',NULL,                                        NOW()-INTERVAL '8 days'),
('05000000-0000-4000-8000-000000000003','04000000-0000-4000-8000-000000000031','03000000-0000-4000-8000-000000000015','WrongAmount',         'Customer reported issue with transaction','Resolved',    'Refunded',331.03,'ZWG','02000000-0000-4000-8000-000000000001',NOW()-INTERVAL '29 days'+INTERVAL '37 hours',NOW()-INTERVAL '29 days'),
('05000000-0000-4000-8000-000000000004','04000000-0000-4000-8000-000000000022','03000000-0000-4000-8000-000000000017','MerchantDispute',     'Customer reported issue with transaction','Resolved',    'Rejected', 73.21,'ZWG','02000000-0000-4000-8000-000000000007',NOW()-INTERVAL '4 days' +INTERVAL '16 hours',NOW()-INTERVAL '4 days'),
('05000000-0000-4000-8000-000000000005','04000000-0000-4000-8000-000000000021','03000000-0000-4000-8000-000000000007','MerchantDispute',     'Customer reported issue with transaction','Open',         '',        0.00,  'ZWG',NULL,                                    NULL,                                        NOW()-INTERVAL '2 days'),
('05000000-0000-4000-8000-000000000006','04000000-0000-4000-8000-000000000012','03000000-0000-4000-8000-000000000001','WrongAmount',         'Customer reported issue with transaction','Investigating','',        0.00,  'ZWG','02000000-0000-4000-8000-000000000007',NULL,                                        NOW()-INTERVAL '2 days'),
('05000000-0000-4000-8000-000000000007','04000000-0000-4000-8000-00000000000b','03000000-0000-4000-8000-00000000001b','ServiceNotRendered',  'Customer reported issue with transaction','Resolved',    'Refunded',356.58,'ZWG','02000000-0000-4000-8000-000000000001',NOW()-INTERVAL '8 days' +INTERVAL '14 hours',NOW()-INTERVAL '8 days'),
('05000000-0000-4000-8000-000000000008','04000000-0000-4000-8000-000000000007','03000000-0000-4000-8000-000000000001','ServiceNotRendered',  'Customer reported issue with transaction','Resolved',    'Rejected',  4.20,'ZWG','02000000-0000-4000-8000-000000000001',NOW()-INTERVAL '19 days'+INTERVAL '28 hours',NOW()-INTERVAL '19 days'),
('05000000-0000-4000-8000-000000000009','04000000-0000-4000-8000-000000000020','03000000-0000-4000-8000-000000000005','WrongAmount',         'Customer reported issue with transaction','Open',         '',        0.00,  'ZWG',NULL,                                    NULL,                                        NOW()-INTERVAL '18 days'),
('05000000-0000-4000-8000-00000000000a','04000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-00000000001f','Duplicate',           'Customer reported issue with transaction','Investigating','',        0.00,  'ZWG','02000000-0000-4000-8000-000000000002',NULL,                                        NOW()-INTERVAL '5 days'),
('05000000-0000-4000-8000-00000000000b','04000000-0000-4000-8000-00000000002a','03000000-0000-4000-8000-000000000013','Unauthorized',        'Customer reported issue with transaction','Resolved',    'Refunded',331.73,'ZWG','02000000-0000-4000-8000-000000000002',NOW()-INTERVAL '0 days' +INTERVAL '40 hours',NOW()-INTERVAL '0 days'),
('05000000-0000-4000-8000-00000000000c','04000000-0000-4000-8000-000000000026','03000000-0000-4000-8000-000000000017','WrongAmount',         'Customer reported issue with transaction','Resolved',    'Rejected',246.56,'ZWG','02000000-0000-4000-8000-000000000007',NOW()-INTERVAL '1 day'  +INTERVAL '46 hours',NOW()-INTERVAL '1 day'),
('05000000-0000-4000-8000-00000000000d','04000000-0000-4000-8000-000000000022','03000000-0000-4000-8000-000000000017','MerchantDispute',     'Customer reported issue with transaction','Open',         '',        0.00,  'ZWG',NULL,                                    NULL,                                        NOW()-INTERVAL '10 days'),
('05000000-0000-4000-8000-00000000000e','04000000-0000-4000-8000-000000000013','03000000-0000-4000-8000-00000000001f','ServiceNotRendered',  'Customer reported issue with transaction','Investigating','',        0.00,  'ZWG','02000000-0000-4000-8000-000000000007',NULL,                                        NOW()-INTERVAL '13 days'),
('05000000-0000-4000-8000-00000000000f','04000000-0000-4000-8000-000000000020','03000000-0000-4000-8000-00000000000d','ServiceNotRendered',  'Customer reported issue with transaction','Resolved',    'Refunded',110.91,'ZWG','02000000-0000-4000-8000-000000000001',NOW()-INTERVAL '19 days'+INTERVAL '41 hours',NOW()-INTERVAL '19 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 5. FRAUD ALERTS — 12 rows (seed=55)
-- =============================================================================
INSERT INTO bank.fraud_alerts
  ("Id",account_id,transaction_id,alert_type,severity,description,status,tenant_id,created_at)
VALUES
('06000000-0000-4000-8000-000000000001','03000000-0000-4000-8000-000000000001','04000000-0000-4000-8000-00000000000c','AmountAnomaly',  'High',  'Suspicious geoanomaly detected',     'New',      'unibank',NOW()-INTERVAL '4 days'),
('06000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-000000000001','04000000-0000-4000-8000-00000000001e','GeoAnomaly',     'Medium','Suspicious geoanomaly detected',     'Reviewed', 'unibank',NOW()-INTERVAL '5 days'),
('06000000-0000-4000-8000-000000000003','03000000-0000-4000-8000-000000000003','04000000-0000-4000-8000-00000000001d','DeviceAnomaly',  'Low',   'Suspicious patternmatch detected',   'Escalated','unibank',NOW()-INTERVAL '13 days'),
('06000000-0000-4000-8000-000000000004','03000000-0000-4000-8000-000000000005','04000000-0000-4000-8000-00000000002e','AmountAnomaly',  'High',  'Suspicious velocityanomaly detected','Dismissed','unibank',NOW()-INTERVAL '9 days'),
('06000000-0000-4000-8000-000000000005','03000000-0000-4000-8000-000000000027','04000000-0000-4000-8000-000000000027','GeoAnomaly',     'Medium','Suspicious velocityanomaly detected','New',      'unibank',NOW()-INTERVAL '7 days'),
('06000000-0000-4000-8000-000000000006','03000000-0000-4000-8000-000000000027','04000000-0000-4000-8000-000000000003','PatternMatch',   'Low',   'Suspicious patternmatch detected',   'Reviewed', 'unibank',NOW()-INTERVAL '7 days'),
('06000000-0000-4000-8000-000000000007','03000000-0000-4000-8000-000000000003','04000000-0000-4000-8000-00000000002f','AmountAnomaly',  'High',  'Suspicious deviceanomaly detected',  'Escalated','unibank',NOW()-INTERVAL '6 days'),
('06000000-0000-4000-8000-000000000008','03000000-0000-4000-8000-000000000003','04000000-0000-4000-8000-000000000028','DeviceAnomaly',  'Medium','Suspicious amountanomaly detected',  'Dismissed','unibank',NOW()-INTERVAL '1 day'),
('06000000-0000-4000-8000-000000000009','03000000-0000-4000-8000-000000000017','04000000-0000-4000-8000-000000000003','DeviceAnomaly',  'Low',   'Suspicious amountanomaly detected',  'New',      'unibank',NOW()-INTERVAL '11 days'),
('06000000-0000-4000-8000-00000000000a','03000000-0000-4000-8000-000000000027','04000000-0000-4000-8000-000000000025','DeviceAnomaly',  'High',  'Suspicious patternmatch detected',   'Reviewed', 'unibank',NOW()-INTERVAL '0 days'),
('06000000-0000-4000-8000-00000000000b','03000000-0000-4000-8000-000000000007','04000000-0000-4000-8000-00000000000e','VelocityAnomaly','Medium','Suspicious velocityanomaly detected','Escalated','unibank',NOW()-INTERVAL '10 days'),
('06000000-0000-4000-8000-00000000000c','03000000-0000-4000-8000-000000000007','04000000-0000-4000-8000-000000000026','GeoAnomaly',     'Low',   'Suspicious velocityanomaly detected','Dismissed','unibank',NOW()-INTERVAL '4 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 6. KYC DOCUMENTS — 6 rows (seed=33)
-- =============================================================================
INSERT INTO bank.kyc_documents
  ("Id",account_id,document_type,file_name,content_type,file_size_bytes,
   file_path,encryption_key_ref,checksum_sha256,status,tenant_id,verified_at,created_at)
VALUES
('07000000-0000-4000-8000-000000000001','03000000-0000-4000-8000-000000000001','NationalId','kyc_tendai_moyo.jpg',       'image/jpeg',204800,'/kyc/demo/kyc_tendai_moyo.jpg',       'demo-key-ref','abc1111111111111111111111111111111111111111111111111111111111111','Approved','unibank',NOW()-INTERVAL '0 days', NOW()-INTERVAL '0 days'),
('07000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-000000000003','NationalId','kyc_chiedza_mutasa.jpg',    'image/jpeg',187340,'/kyc/demo/kyc_chiedza_mutasa.jpg',    'demo-key-ref','abc2222222222222222222222222222222222222222222222222222222222222','Approved','unibank',NOW()-INTERVAL '2 days', NOW()-INTERVAL '2 days'),
('07000000-0000-4000-8000-000000000003','03000000-0000-4000-8000-000000000005','NationalId','kyc_farai_chikwanha.jpg',   'image/jpeg',215000,'/kyc/demo/kyc_farai_chikwanha.jpg',   'demo-key-ref','abc3333333333333333333333333333333333333333333333333333333333333','Approved','unibank',NOW()-INTERVAL '5 days', NOW()-INTERVAL '5 days'),
('07000000-0000-4000-8000-000000000004','03000000-0000-4000-8000-000000000007','NationalId','kyc_rudo_nyamupfukudza.jpg','image/jpeg',198400,'/kyc/demo/kyc_rudo_nyamupfukudza.jpg','demo-key-ref','abc4444444444444444444444444444444444444444444444444444444444444','Rejected','unibank',NULL,                        NOW()-INTERVAL '4 days'),
('07000000-0000-4000-8000-000000000005','03000000-0000-4000-8000-000000000009','NationalId','kyc_blessing_chikowore.jpg','image/jpeg',222100,'/kyc/demo/kyc_blessing_chikowore.jpg','demo-key-ref','abc5555555555555555555555555555555555555555555555555555555555555','Approved','unibank',NOW()-INTERVAL '5 days', NOW()-INTERVAL '5 days'),
('07000000-0000-4000-8000-000000000006','03000000-0000-4000-8000-000000000011','NationalId','kyc_simba_jongwe.jpg',      'image/jpeg',175600,'/kyc/demo/kyc_simba_jongwe.jpg',      'demo-key-ref','abc6666666666666666666666666666666666666666666666666666666666666','Approved','unibank',NOW()-INTERVAL '5 days', NOW()-INTERVAL '5 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 7. LOANS — 5 rows (seed=88)
-- =============================================================================
INSERT INTO bank.loans
  ("Id",account_id,principal,outstanding_balance,interest_rate,tenure_months,
   monthly_payment,purpose,status,credit_score,payments_made,reference,
   currency,tenant_id,created_at)
VALUES
('08000000-0000-4000-8000-000000000001','03000000-0000-4000-8000-000000000001',5186.00,5186.00,18.5000, 6, 910.00,'Business',        'Pending',701,0,'LOAN-006000','ZWG','unibank',NOW()-INTERVAL '6 days'),
('08000000-0000-4000-8000-000000000002','03000000-0000-4000-8000-000000000003',7607.00,7607.00,21.0000,18, 490.00,'Education',        'Pending',588,0,'LOAN-006001','ZWG','unibank',NOW()-INTERVAL '0 days'),
('08000000-0000-4000-8000-000000000003','03000000-0000-4000-8000-000000000005',7535.00,7535.00,24.0000,24, 380.00,'Medical',          'Pending',737,0,'LOAN-006002','ZWG','unibank',NOW()-INTERVAL '5 days'),
('08000000-0000-4000-8000-000000000004','03000000-0000-4000-8000-000000000007',1382.00,1382.00,18.5000,12, 130.00,'Home Improvement',  'Pending',629,0,'LOAN-006003','ZWG','unibank',NOW()-INTERVAL '4 days'),
('08000000-0000-4000-8000-000000000005','03000000-0000-4000-8000-000000000009',2180.00,2180.00,21.0000,18, 140.00,'Agriculture',       'Pending',533,0,'LOAN-006004','ZWG','unibank',NOW()-INTERVAL '6 days')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 8. MERCHANTS — 8 rows (Merchants.jsx)
-- =============================================================================
INSERT INTO bank.merchants
  ("Id",merchant_code,owner_account_id,business_name,business_type,
   category_code,business_address,is_agent,agent_terms_accepted,
   agent_terms_accepted_at,status,kyc_status,tenant_id,activated_at,created_at)
VALUES
('09000000-0000-4000-8000-000000000001','M001','03000000-0000-4000-8000-000000000001','OK Supermarket - Borrowdale','Retail',    'Retail',          '55 Borrowdale Rd, Harare',          false,false,NULL,                       'Active',   'Approved','unibank','2025-06-15 00:00:00+00','2025-06-15 00:00:00+00'),
('09000000-0000-4000-8000-000000000002','M002','03000000-0000-4000-8000-000000000003','TM Pick n Pay - Eastgate',   'Retail',    'Retail',          'Eastgate Mall, Harare',             false,false,NULL,                       'Active',   'Approved','unibank','2025-07-20 00:00:00+00','2025-07-20 00:00:00+00'),
('09000000-0000-4000-8000-000000000003','M003','03000000-0000-4000-8000-000000000005','Chicken Inn - Samora',       'FoodBev',   'Food & Beverage', 'Samora Machel Ave, Harare',         false,false,NULL,                       'Active',   'Approved','unibank','2025-08-10 00:00:00+00','2025-08-10 00:00:00+00'),
('09000000-0000-4000-8000-000000000004','M004','03000000-0000-4000-8000-000000000007','N. Richards Pharmacy',       'Health',    'Health',          '12 Park St, Harare',                false,false,NULL,                       'Suspended','Approved','unibank','2025-09-01 00:00:00+00','2025-09-01 00:00:00+00'),
('09000000-0000-4000-8000-000000000005','M005','03000000-0000-4000-8000-000000000009','Zuva Fuel - Msasa',          'Fuel',      'Fuel',            'Msasa Industrial, Harare',          false,false,NULL,                       'Active',   'Approved','unibank','2025-10-12 00:00:00+00','2025-10-12 00:00:00+00'),
('09000000-0000-4000-8000-000000000006','M006','03000000-0000-4000-8000-00000000000b','Edgars - Joina City',        'Clothing',  'Clothing',        'Joina City Mall, Harare',           false,false,NULL,                       'Pending',  'Pending', 'unibank',NULL,                      '2026-03-20 00:00:00+00'),
('09000000-0000-4000-8000-000000000007','M007','03000000-0000-4000-8000-00000000000d','Bon Marche - Avondale',      'Retail',    'Retail',          'Avondale Shopping Centre, Harare',  false,false,NULL,                       'Active',   'Approved','unibank','2025-05-08 00:00:00+00','2025-05-08 00:00:00+00'),
('09000000-0000-4000-8000-000000000008','M008','03000000-0000-4000-8000-00000000000f','Delta Beverages - Depot',    'Wholesale', 'Wholesale',       'Workington Industrial, Harare',     false,false,NULL,                       'Closed',   'Approved','unibank','2024-11-15 00:00:00+00','2024-11-15 00:00:00+00')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 9. BILL PROVIDERS — 5 rows
-- =============================================================================
INSERT INTO bank.bill_providers
  ("Id",name,code,category,requires_meter_number,requires_account_number,
   min_amount,max_amount,currency,status,country_code,tenant_id,created_at)
VALUES
('0a000000-0000-4000-8000-000000000001','ZESA Holdings',                        'ZESA',  'Utilities',true, false,5.00,  50000.00,'ZWG','Active','ZW','unibank',NOW()),
('0a000000-0000-4000-8000-000000000002','TelOne',                               'TELONE','Telecom',  false,true, 2.00,  10000.00,'ZWG','Active','ZW','unibank',NOW()),
('0a000000-0000-4000-8000-000000000003','NetOne',                               'NETONE','Telecom',  false,true, 1.00,   5000.00,'ZWG','Active','ZW','unibank',NOW()),
('0a000000-0000-4000-8000-000000000004','Econet Wireless',                      'ECONET','Telecom',  false,true, 1.00,   5000.00,'ZWG','Active','ZW','unibank',NOW()),
('0a000000-0000-4000-8000-000000000005','Zimbabwe National Water Authority',    'ZINWA', 'Utilities',false,true, 5.00,  20000.00,'ZWG','Active','ZW','unibank',NOW())
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 10. SYSTEM CONFIGS — 22 rows (SystemConfig.jsx)
-- =============================================================================
INSERT INTO bank.system_configs ("Id",key,value_json,tenant_id,created_at) VALUES
('0b000000-0000-4000-8000-000000000001','card.bin_prefix',                '"6275"',                        NULL,NOW()),
('0b000000-0000-4000-8000-000000000002','card.pan_length',                '"16"',                          NULL,NOW()),
('0b000000-0000-4000-8000-000000000003','account.daily_limit_zwg',        '"50000"',                       NULL,NOW()),
('0b000000-0000-4000-8000-000000000004','account.daily_limit_usd',        '"10000"',                       NULL,NOW()),
('0b000000-0000-4000-8000-000000000005','account.monthly_limit_zwg',      '"200000"',                      NULL,NOW()),
('0b000000-0000-4000-8000-000000000006','account.monthly_limit_usd',      '"50000"',                       NULL,NOW()),
('0b000000-0000-4000-8000-000000000007','otp.ttl_seconds',                '"300"',                         NULL,NOW()),
('0b000000-0000-4000-8000-000000000008','otp.max_attempts',               '"5"',                           NULL,NOW()),
('0b000000-0000-4000-8000-000000000009','pin.max_failed_attempts',        '"5"',                           NULL,NOW()),
('0b000000-0000-4000-8000-00000000000a','pin.lockout_minutes',            '"30"',                          NULL,NOW()),
('0b000000-0000-4000-8000-00000000000b','kyc.face_match_auto_approve',    '"0.80"',                        NULL,NOW()),
('0b000000-0000-4000-8000-00000000000c','kyc.face_match_reject',          '"0.40"',                        NULL,NOW()),
('0b000000-0000-4000-8000-00000000000d','fraud.velocity_limit_count',     '"10"',                          NULL,NOW()),
('0b000000-0000-4000-8000-00000000000e','fraud.velocity_window_minutes',  '"60"',                          NULL,NOW()),
('0b000000-0000-4000-8000-00000000000f','fraud.high_value_threshold_zwg', '"100000"',                      NULL,NOW()),
('0b000000-0000-4000-8000-000000000010','fraud.high_value_threshold_usd', '"5000"',                        NULL,NOW()),
('0b000000-0000-4000-8000-000000000011','loan.max_tenure_months',         '"48"',                          NULL,NOW()),
('0b000000-0000-4000-8000-000000000012','loan.min_credit_score',          '"200"',                         NULL,NOW()),
('0b000000-0000-4000-8000-000000000013','loan.income_variance_threshold', '"10"',                          NULL,NOW()),
('0b000000-0000-4000-8000-000000000014','switch.gateway_url',             '"http://synergy-switch:5002"',  NULL,NOW()),
('0b000000-0000-4000-8000-000000000015','ai.ollama_url',                  '"http://unibank-ollama:11434"', NULL,NOW()),
('0b000000-0000-4000-8000-000000000016','ai.model_name',                  '"qwen3-vl"',                    NULL,NOW())
ON CONFLICT (key,tenant_id) DO NOTHING;

-- =============================================================================
-- 11. AUDIT LOGS — 60 rows (seed=66)
-- =============================================================================
INSERT INTO bank.audit_logs
  ("Id",admin_user_id,action,entity_type,entity_id,details,ip_address,created_at)
VALUES
('0c000000-0000-4000-8000-000000000001','02000000-0000-4000-8000-000000000001','Account Action',    'Account','ACC-001017','{"browser":"Chrome 130"}','192.168.1.42', NOW()-INTERVAL '45 hours'),
('0c000000-0000-4000-8000-000000000002','02000000-0000-4000-8000-000000000004','Login',             'Account','ACC-001016','{"browser":"Chrome 130"}','192.168.1.176',NOW()-INTERVAL '140 hours'),
('0c000000-0000-4000-8000-000000000003','02000000-0000-4000-8000-000000000003','KYC Review',        'Account','ACC-001016','{"browser":"Chrome 130"}','192.168.1.135',NOW()-INTERVAL '47 hours'),
('0c000000-0000-4000-8000-000000000004','02000000-0000-4000-8000-000000000007','KYC Review',        'Account','ACC-001010','{"browser":"Chrome 130"}','192.168.1.105',NOW()-INTERVAL '51 hours'),
('0c000000-0000-4000-8000-000000000005','02000000-0000-4000-8000-000000000004','KYC Review',        'Account','ACC-001017','{"browser":"Chrome 130"}','192.168.1.216',NOW()-INTERVAL '68 hours'),
('0c000000-0000-4000-8000-000000000006','02000000-0000-4000-8000-000000000006','Login',             'Account','ACC-001003','{"browser":"Chrome 130"}','192.168.1.71', NOW()-INTERVAL '76 hours'),
('0c000000-0000-4000-8000-000000000007','02000000-0000-4000-8000-000000000001','KYC Review',        'Account','ACC-001006','{"browser":"Chrome 130"}','192.168.1.148',NOW()-INTERVAL '22 hours'),
('0c000000-0000-4000-8000-000000000008','02000000-0000-4000-8000-000000000005','Account Action',    'Account','ACC-001018','{"browser":"Chrome 130"}','192.168.1.29', NOW()-INTERVAL '68 hours'),
('0c000000-0000-4000-8000-000000000009','02000000-0000-4000-8000-000000000003','Config Change',     'Account','ACC-001013','{"browser":"Chrome 130"}','192.168.1.1',  NOW()-INTERVAL '121 hours'),
('0c000000-0000-4000-8000-00000000000a','02000000-0000-4000-8000-000000000006','Account Action',    'Account','ACC-001007','{"browser":"Chrome 130"}','192.168.1.205',NOW()-INTERVAL '167 hours'),
('0c000000-0000-4000-8000-00000000000b','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001013','{"browser":"Chrome 130"}','192.168.1.181',NOW()-INTERVAL '24 hours'),
('0c000000-0000-4000-8000-00000000000c','02000000-0000-4000-8000-000000000007','Login',             'Account','ACC-001004','{"browser":"Chrome 130"}','192.168.1.254',NOW()-INTERVAL '1 hours'),
('0c000000-0000-4000-8000-00000000000d','02000000-0000-4000-8000-000000000005','KYC Review',        'Account','ACC-001001','{"browser":"Chrome 130"}','192.168.1.31', NOW()-INTERVAL '9 hours'),
('0c000000-0000-4000-8000-00000000000e','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001019','{"browser":"Chrome 130"}','192.168.1.221',NOW()-INTERVAL '92 hours'),
('0c000000-0000-4000-8000-00000000000f','02000000-0000-4000-8000-000000000001','KYC Review',        'Account','ACC-001005','{"browser":"Chrome 130"}','192.168.1.245',NOW()-INTERVAL '52 hours'),
('0c000000-0000-4000-8000-000000000010','02000000-0000-4000-8000-000000000005','Login',             'Account','ACC-001006','{"browser":"Chrome 130"}','192.168.1.212',NOW()-INTERVAL '43 hours'),
('0c000000-0000-4000-8000-000000000011','02000000-0000-4000-8000-000000000005','KYC Review',        'Account','ACC-001000','{"browser":"Chrome 130"}','192.168.1.78', NOW()-INTERVAL '73 hours'),
('0c000000-0000-4000-8000-000000000012','02000000-0000-4000-8000-000000000004','Dispute Resolution','Account','ACC-001017','{"browser":"Chrome 130"}','192.168.1.165',NOW()-INTERVAL '161 hours'),
('0c000000-0000-4000-8000-000000000013','02000000-0000-4000-8000-000000000003','KYC Review',        'Account','ACC-001003','{"browser":"Chrome 130"}','192.168.1.4',  NOW()-INTERVAL '147 hours'),
('0c000000-0000-4000-8000-000000000014','02000000-0000-4000-8000-000000000006','Login',             'Account','ACC-001010','{"browser":"Chrome 130"}','192.168.1.106',NOW()-INTERVAL '130 hours'),
('0c000000-0000-4000-8000-000000000015','02000000-0000-4000-8000-000000000004','Config Change',     'Account','ACC-001019','{"browser":"Chrome 130"}','192.168.1.37', NOW()-INTERVAL '129 hours'),
('0c000000-0000-4000-8000-000000000016','02000000-0000-4000-8000-000000000006','Config Change',     'Account','ACC-001003','{"browser":"Chrome 130"}','192.168.1.229',NOW()-INTERVAL '45 hours'),
('0c000000-0000-4000-8000-000000000017','02000000-0000-4000-8000-000000000001','KYC Review',        'Account','ACC-001012','{"browser":"Chrome 130"}','192.168.1.61', NOW()-INTERVAL '132 hours'),
('0c000000-0000-4000-8000-000000000018','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001000','{"browser":"Chrome 130"}','192.168.1.155',NOW()-INTERVAL '28 hours'),
('0c000000-0000-4000-8000-000000000019','02000000-0000-4000-8000-000000000001','Login',             'Account','ACC-001003','{"browser":"Chrome 130"}','192.168.1.197',NOW()-INTERVAL '57 hours'),
('0c000000-0000-4000-8000-00000000001a','02000000-0000-4000-8000-000000000006','Config Change',     'Account','ACC-001016','{"browser":"Chrome 130"}','192.168.1.244',NOW()-INTERVAL '157 hours'),
('0c000000-0000-4000-8000-00000000001b','02000000-0000-4000-8000-000000000004','Login',             'Account','ACC-001018','{"browser":"Chrome 130"}','192.168.1.118',NOW()-INTERVAL '162 hours'),
('0c000000-0000-4000-8000-00000000001c','02000000-0000-4000-8000-000000000005','Config Change',     'Account','ACC-001014','{"browser":"Chrome 130"}','192.168.1.206',NOW()-INTERVAL '126 hours'),
('0c000000-0000-4000-8000-00000000001d','02000000-0000-4000-8000-000000000007','Login',             'Account','ACC-001014','{"browser":"Chrome 130"}','192.168.1.229',NOW()-INTERVAL '130 hours'),
('0c000000-0000-4000-8000-00000000001e','02000000-0000-4000-8000-000000000007','KYC Review',        'Account','ACC-001005','{"browser":"Chrome 130"}','192.168.1.62', NOW()-INTERVAL '107 hours'),
('0c000000-0000-4000-8000-00000000001f','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001006','{"browser":"Chrome 130"}','192.168.1.18', NOW()-INTERVAL '30 hours'),
('0c000000-0000-4000-8000-000000000020','02000000-0000-4000-8000-000000000004','KYC Review',        'Account','ACC-001019','{"browser":"Chrome 130"}','192.168.1.1',  NOW()-INTERVAL '4 hours'),
('0c000000-0000-4000-8000-000000000021','02000000-0000-4000-8000-000000000003','Dispute Resolution','Account','ACC-001013','{"browser":"Chrome 130"}','192.168.1.222',NOW()-INTERVAL '69 hours'),
('0c000000-0000-4000-8000-000000000022','02000000-0000-4000-8000-000000000004','Login',             'Account','ACC-001007','{"browser":"Chrome 130"}','192.168.1.21', NOW()-INTERVAL '89 hours'),
('0c000000-0000-4000-8000-000000000023','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001001','{"browser":"Chrome 130"}','192.168.1.176',NOW()-INTERVAL '118 hours'),
('0c000000-0000-4000-8000-000000000024','02000000-0000-4000-8000-000000000001','Account Action',    'Account','ACC-001018','{"browser":"Chrome 130"}','192.168.1.13', NOW()-INTERVAL '115 hours'),
('0c000000-0000-4000-8000-000000000025','02000000-0000-4000-8000-000000000001','Config Change',     'Account','ACC-001007','{"browser":"Chrome 130"}','192.168.1.3',  NOW()-INTERVAL '82 hours'),
('0c000000-0000-4000-8000-000000000026','02000000-0000-4000-8000-000000000002','Config Change',     'Account','ACC-001019','{"browser":"Chrome 130"}','192.168.1.208',NOW()-INTERVAL '85 hours'),
('0c000000-0000-4000-8000-000000000027','02000000-0000-4000-8000-000000000001','KYC Review',        'Account','ACC-001012','{"browser":"Chrome 130"}','192.168.1.225',NOW()-INTERVAL '161 hours'),
('0c000000-0000-4000-8000-000000000028','02000000-0000-4000-8000-000000000004','Account Action',    'Account','ACC-001015','{"browser":"Chrome 130"}','192.168.1.110',NOW()-INTERVAL '167 hours'),
('0c000000-0000-4000-8000-000000000029','02000000-0000-4000-8000-000000000004','KYC Review',        'Account','ACC-001003','{"browser":"Chrome 130"}','192.168.1.47', NOW()-INTERVAL '166 hours'),
('0c000000-0000-4000-8000-00000000002a','02000000-0000-4000-8000-000000000001','Dispute Resolution','Account','ACC-001019','{"browser":"Chrome 130"}','192.168.1.83', NOW()-INTERVAL '138 hours'),
('0c000000-0000-4000-8000-00000000002b','02000000-0000-4000-8000-000000000001','Config Change',     'Account','ACC-001000','{"browser":"Chrome 130"}','192.168.1.95', NOW()-INTERVAL '27 hours'),
('0c000000-0000-4000-8000-00000000002c','02000000-0000-4000-8000-000000000002','Config Change',     'Account','ACC-001012','{"browser":"Chrome 130"}','192.168.1.67', NOW()-INTERVAL '160 hours'),
('0c000000-0000-4000-8000-00000000002d','02000000-0000-4000-8000-000000000004','Account Action',    'Account','ACC-001011','{"browser":"Chrome 130"}','192.168.1.22', NOW()-INTERVAL '50 hours'),
('0c000000-0000-4000-8000-00000000002e','02000000-0000-4000-8000-000000000002','Login',             'Account','ACC-001000','{"browser":"Chrome 130"}','192.168.1.127',NOW()-INTERVAL '28 hours'),
('0c000000-0000-4000-8000-00000000002f','02000000-0000-4000-8000-000000000002','Account Action',    'Account','ACC-001018','{"browser":"Chrome 130"}','192.168.1.184',NOW()-INTERVAL '31 hours'),
('0c000000-0000-4000-8000-000000000030','02000000-0000-4000-8000-000000000003','KYC Review',        'Account','ACC-001008','{"browser":"Chrome 130"}','192.168.1.246',NOW()-INTERVAL '140 hours'),
('0c000000-0000-4000-8000-000000000031','02000000-0000-4000-8000-000000000002','Login',             'Account','ACC-001017','{"browser":"Chrome 130"}','192.168.1.26', NOW()-INTERVAL '40 hours'),
('0c000000-0000-4000-8000-000000000032','02000000-0000-4000-8000-000000000007','Config Change',     'Account','ACC-001002','{"browser":"Chrome 130"}','192.168.1.173',NOW()-INTERVAL '102 hours'),
('0c000000-0000-4000-8000-000000000033','02000000-0000-4000-8000-000000000001','Login',             'Account','ACC-001000','{"browser":"Chrome 130"}','192.168.1.17', NOW()-INTERVAL '0 hours'),
('0c000000-0000-4000-8000-000000000034','02000000-0000-4000-8000-000000000004','Login',             'Account','ACC-001018','{"browser":"Chrome 130"}','192.168.1.52', NOW()-INTERVAL '91 hours'),
('0c000000-0000-4000-8000-000000000035','02000000-0000-4000-8000-000000000006','Account Action',    'Account','ACC-001004','{"browser":"Chrome 130"}','192.168.1.108',NOW()-INTERVAL '66 hours'),
('0c000000-0000-4000-8000-000000000036','02000000-0000-4000-8000-000000000004','Login',             'Account','ACC-001001','{"browser":"Chrome 130"}','192.168.1.169',NOW()-INTERVAL '58 hours'),
('0c000000-0000-4000-8000-000000000037','02000000-0000-4000-8000-000000000006','Dispute Resolution','Account','ACC-001014','{"browser":"Chrome 130"}','192.168.1.174',NOW()-INTERVAL '129 hours'),
('0c000000-0000-4000-8000-000000000038','02000000-0000-4000-8000-000000000006','KYC Review',        'Account','ACC-001014','{"browser":"Chrome 130"}','192.168.1.159',NOW()-INTERVAL '148 hours'),
('0c000000-0000-4000-8000-000000000039','02000000-0000-4000-8000-000000000006','Account Action',    'Account','ACC-001013','{"browser":"Chrome 130"}','192.168.1.28', NOW()-INTERVAL '150 hours'),
('0c000000-0000-4000-8000-00000000003a','02000000-0000-4000-8000-000000000001','Login',             'Account','ACC-001005','{"browser":"Chrome 130"}','192.168.1.172',NOW()-INTERVAL '112 hours'),
('0c000000-0000-4000-8000-00000000003b','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001004','{"browser":"Chrome 130"}','192.168.1.107',NOW()-INTERVAL '163 hours'),
('0c000000-0000-4000-8000-00000000003c','02000000-0000-4000-8000-000000000005','Dispute Resolution','Account','ACC-001017','{"browser":"Chrome 130"}','192.168.1.116',NOW()-INTERVAL '157 hours')
ON CONFLICT DO NOTHING;

COMMIT;
