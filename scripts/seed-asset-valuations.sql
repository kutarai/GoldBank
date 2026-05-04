-- Seeds two prior AssetValuation rows per gold-coin asset for John Moyo's portfolio.
-- This populates the Valuation History tab and lights up the % change column.
--
-- One older valuation (~60 days ago, 8% lower than current) gives each coin a
-- "first" entry; one recent valuation (~7 days ago, at current value) gives the
-- second entry so each row in the history table renders a +8.7% change.
-- Idempotent: ON CONFLICT on the deterministic UUIDs.

\set ON_ERROR_STOP on
BEGIN;

INSERT INTO bank.asset_valuations
  ("Id", asset_id, valuation_amount, currency, valuer_name, valuer_license,
   notes, tenant_id, created_at)
SELECT v."Id", v.asset_id, v.amount, 'USD', v.valuer, v.license,
       v.notes, '00000000-0000-0000-0000-000000000000', v.created_at
FROM (VALUES
  -- (Id, asset_id, amount, valuer, license, notes, created_at)
  -- AST 0a..0001  Krugerrand × 2     current $5,400  → prior $4,968
  ('1a000000-0000-4000-8000-000000000001'::uuid, '0a000000-0000-4000-8000-000000000001'::uuid, 4968.00, 'Mr. F. Chikwanda', 'ZW-VAL-0019', 'Annual revaluation, spot $1,985/oz.',                     NOW() - INTERVAL '62 days'),
  ('1a000000-0000-4000-8000-000000000002'::uuid, '0a000000-0000-4000-8000-000000000001'::uuid, 5400.00, 'Mrs. T. Ndhlovu',  'ZW-VAL-0047', 'Spot price up; coins inspected, capsules intact.',         NOW() - INTERVAL '7 days'),
  -- AST 0a..0002  American Eagle × 1 current $2,800  → prior $2,576
  ('1a000000-0000-4000-8000-000000000003'::uuid, '0a000000-0000-4000-8000-000000000002'::uuid, 2576.00, 'Mr. F. Chikwanda', 'ZW-VAL-0019', 'Initial intake valuation.',                                NOW() - INTERVAL '60 days'),
  ('1a000000-0000-4000-8000-000000000004'::uuid, '0a000000-0000-4000-8000-000000000002'::uuid, 2800.00, 'Mrs. T. Ndhlovu',  'ZW-VAL-0047', 'Quarterly revaluation. No physical changes.',              NOW() - INTERVAL '5 days'),
  -- AST 0a..0003  Maple Leaf 1/2oz × 3 current $4,350 → prior $4,002
  ('1a000000-0000-4000-8000-000000000005'::uuid, '0a000000-0000-4000-8000-000000000003'::uuid, 4002.00, 'Mr. S. Mlambo',    'ZW-VAL-0033', 'Initial valuation on intake.',                             NOW() - INTERVAL '58 days'),
  ('1a000000-0000-4000-8000-000000000006'::uuid, '0a000000-0000-4000-8000-000000000003'::uuid, 4350.00, 'Mrs. T. Ndhlovu',  'ZW-VAL-0047', 'Spot price increase. All coins verified.',                 NOW() - INTERVAL '6 days'),
  -- AST 0a..0004  Vienna Phil × 1    current $2,950  → prior $2,714
  ('1a000000-0000-4000-8000-000000000007'::uuid, '0a000000-0000-4000-8000-000000000004'::uuid, 2714.00, 'Mr. F. Chikwanda', 'ZW-VAL-0019', 'Coin in mint condition; assay matches.',                   NOW() - INTERVAL '55 days'),
  ('1a000000-0000-4000-8000-000000000008'::uuid, '0a000000-0000-4000-8000-000000000004'::uuid, 2950.00, 'Mrs. T. Ndhlovu',  'ZW-VAL-0047', 'Re-valued on spot uptick.',                                NOW() - INTERVAL '4 days'),
  -- AST 0a..0005  Britannia 1/4oz × 4 current $3,000 → prior $2,760
  ('1a000000-0000-4000-8000-000000000009'::uuid, '0a000000-0000-4000-8000-000000000005'::uuid, 2760.00, 'Mr. S. Mlambo',    'ZW-VAL-0033', 'Initial valuation on intake.',                             NOW() - INTERVAL '50 days'),
  ('1a000000-0000-4000-8000-00000000000a'::uuid, '0a000000-0000-4000-8000-000000000005'::uuid, 3000.00, 'Mrs. T. Ndhlovu',  'ZW-VAL-0047', 'Quarterly revaluation.',                                   NOW() - INTERVAL '3 days')
) AS v("Id", asset_id, amount, valuer, license, notes, created_at)
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

\echo
\echo '== Seeded asset valuations =='
SELECT v.created_at::date AS date,
       a.receipt_number,
       v.valuation_amount,
       v.currency,
       v.valuer_name
FROM bank.asset_valuations v
JOIN bank.assets a ON a."Id" = v.asset_id
ORDER BY a.receipt_number, v.created_at;
