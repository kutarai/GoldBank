-- Ages the most-recent valuation on AST-000003 (Maple Leaf) and AST-000005
-- (Britannia) so the Valuation Queue tab has overdue items to act on.
-- (Queue rule: lastValued > 30 days ago → overdue.)

\set ON_ERROR_STOP on
BEGIN;

-- AST-000003 — push to ~50 days ago (so 20 days overdue)
UPDATE bank.assets
SET    last_valuation_date = NOW() - INTERVAL '50 days',
       updated_at          = NOW()
WHERE  "Id" = '0a000000-0000-4000-8000-000000000003';

UPDATE bank.asset_valuations
SET    created_at = NOW() - INTERVAL '50 days',
       updated_at = NOW()
WHERE  "Id" = '1a000000-0000-4000-8000-000000000006'; -- 0103's "recent" valuation row

-- AST-000005 — push to ~45 days ago (so 15 days overdue)
UPDATE bank.assets
SET    last_valuation_date = NOW() - INTERVAL '45 days',
       updated_at          = NOW()
WHERE  "Id" = '0a000000-0000-4000-8000-000000000005';

UPDATE bank.asset_valuations
SET    created_at = NOW() - INTERVAL '45 days',
       updated_at = NOW()
WHERE  "Id" = '1a000000-0000-4000-8000-00000000000a'; -- 0105's "recent" valuation row

COMMIT;

\echo
\echo '== Asset valuation ages =='
SELECT a.receipt_number,
       a.last_valuation_amount AS current_value,
       a.last_valuation_date::date AS last_valued,
       (NOW()::date - a.last_valuation_date::date)::int AS days_since_valued,
       GREATEST(0, (NOW()::date - a.last_valuation_date::date)::int - 30) AS days_overdue
FROM   bank.assets a
ORDER  BY a.receipt_number;
