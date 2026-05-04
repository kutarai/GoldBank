-- Seed gold-coin assets for the customer behind +263771000001 (John Moyo).
-- Person-scoped (customer_id) so the assets are visible regardless of which
-- currency account is "active" in the mobile session.
-- Idempotent: ON CONFLICT DO NOTHING on natural keys.

\set ON_ERROR_STOP on
BEGIN;

-- Deposit house (one trusted custody facility)
INSERT INTO bank.deposit_houses
  ("Id", name, address, city, contact_phone, contact_email,
   license_number, trust_status, is_active, tenant_id, created_at)
VALUES
  ('0e000000-0000-4000-8000-000000000001',
   'GoldVault Harare',
   '23 Samora Machel Avenue',
   'Harare',
   '+263 242 705 050',
   'custody@goldvault.co.zw',
   'DH-LIC-001',
   'Verified',
   true,
   '00000000-0000-0000-0000-000000000000',
   NOW() - INTERVAL '180 days')
ON CONFLICT (tenant_id, license_number) DO NOTHING;

-- 5 gold coin assets attached to the Customer (person), not the account.
WITH cust AS (
  SELECT c."Id" AS id, c.tenant_id
  FROM bank.customers c
  WHERE c.phone = '+263771000001' AND c.deleted_at IS NULL
  LIMIT 1
), house AS (
  SELECT "Id" AS id FROM bank.deposit_houses WHERE license_number = 'DH-LIC-001'
)
INSERT INTO bank.assets
  ("Id", customer_id, deposit_house_id, receipt_number, asset_type, description,
   quantity, unit, weight_grams, purity, receipt_image_path, receipt_date,
   last_valuation_amount, last_valuation_date, last_verification_date,
   verification_status, status, tenant_id, is_deleted, created_at)
SELECT
  v."Id", cust.id, house.id, v.receipt_number, 'GoldCoin', v.description,
  v.quantity, 'coins', v.weight_grams, v.purity,
  '/assets/demo/' || v.receipt_number || '.jpg',
  NOW() - (v.days_ago || ' days')::INTERVAL,
  v.last_valuation_amount,
  NOW() - (v.days_ago || ' days')::INTERVAL,
  NOW() - (v.days_ago || ' days')::INTERVAL,
  'Verified', 'Active', cust.tenant_id::uuid, false,
  NOW() - (v.days_ago || ' days')::INTERVAL
FROM cust, house, (VALUES
  ('0a000000-0000-4000-8000-000000000001'::uuid, 'GV-2026-0101', '1 oz South African Krugerrand (2024 mint)',     2,  62.20,   0.916700,   5400.00,  120),
  ('0a000000-0000-4000-8000-000000000002'::uuid, 'GV-2026-0102', '1 oz American Gold Eagle (2025 mint)',          1,  31.10,   0.916700,   2800.00,   95),
  ('0a000000-0000-4000-8000-000000000003'::uuid, 'GV-2026-0103', '1/2 oz Canadian Gold Maple Leaf (2024 mint)',   3,  46.65,   0.999900,   4350.00,   60),
  ('0a000000-0000-4000-8000-000000000004'::uuid, 'GV-2026-0104', '1 oz Austrian Vienna Philharmonic (2024)',      1,  31.10,   0.999900,   2950.00,   30),
  ('0a000000-0000-4000-8000-000000000005'::uuid, 'GV-2026-0105', '1/4 oz British Britannia (2025 mint)',          4,  31.10,   0.999900,   3000.00,    7)
) AS v(
  "Id", receipt_number, description, quantity, weight_grams, purity, last_valuation_amount, days_ago
)
ON CONFLICT (deposit_house_id, receipt_number) DO NOTHING;

COMMIT;

-- Verification
\echo
\echo '== Customer for +263771000001 =='
SELECT "Id" AS customer_id, first_name, last_name, phone
FROM bank.customers
WHERE phone = '+263771000001';

\echo
\echo '== Assets attached to that customer =='
SELECT a.receipt_number,
       a.description,
       a.quantity || ' ' || a.unit AS qty,
       a.weight_grams || ' g' AS weight,
       a.purity,
       '$' || a.last_valuation_amount AS value_usd,
       a.status
FROM bank.assets a
JOIN bank.customers c ON c."Id" = a.customer_id
WHERE c.phone = '+263771000001'
ORDER BY a.created_at;

\echo
\echo '== Total custody value (USD) =='
SELECT '$' || SUM(a.last_valuation_amount)::TEXT AS total_value
FROM bank.assets a
JOIN bank.customers c ON c."Id" = a.customer_id
WHERE c.phone = '+263771000001' AND a.is_deleted = false;
