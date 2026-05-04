-- Normalise text-typed tenant_id columns to "goldbank" so the bank-teller
-- tenant gate accepts every customer/account.
\set ON_ERROR_STOP on
BEGIN;

UPDATE bank.customers
SET    tenant_id = 'goldbank', updated_at = NOW()
WHERE  tenant_id <> 'goldbank' AND deleted_at IS NULL;

UPDATE bank.accounts
SET    tenant_id = 'goldbank', updated_at = NOW()
WHERE  tenant_id <> 'goldbank' AND deleted_at IS NULL;

UPDATE bank.ekub_groups
SET    tenant_id = 'goldbank', updated_at = NOW()
WHERE  tenant_id <> 'goldbank';

UPDATE bank.ekub_memberships
SET    tenant_id = 'goldbank', updated_at = NOW()
WHERE  tenant_id <> 'goldbank';

UPDATE bank.ekub_contributions
SET    tenant_id = 'goldbank', updated_at = NOW()
WHERE  tenant_id <> 'goldbank';

COMMIT;

\echo
\echo '== After normalisation =='
SELECT 'customers'         AS tbl, tenant_id, COUNT(*) FROM bank.customers          GROUP BY tenant_id
UNION ALL
SELECT 'accounts'          AS tbl, tenant_id, COUNT(*) FROM bank.accounts           GROUP BY tenant_id
UNION ALL
SELECT 'ekub_groups'       AS tbl, tenant_id, COUNT(*) FROM bank.ekub_groups        GROUP BY tenant_id
UNION ALL
SELECT 'ekub_memberships'  AS tbl, tenant_id, COUNT(*) FROM bank.ekub_memberships   GROUP BY tenant_id
UNION ALL
SELECT 'ekub_contributions'AS tbl, tenant_id, COUNT(*) FROM bank.ekub_contributions GROUP BY tenant_id
ORDER BY 1, 2;
