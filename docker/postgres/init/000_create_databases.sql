-- ============================================================================
-- Create additional databases needed by the UniBank platform
-- 000_create_databases.sql - Runs first (alphabetical ordering)
-- ============================================================================

-- SynergySwitch database (payment switch)
SELECT 'CREATE DATABASE synergy_switch'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'synergy_switch')\gexec
