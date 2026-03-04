-- ============================================================================
-- UniBank Multi-Tenant Database Schema
-- 003_seed_data.sql - Seed data for development
-- ============================================================================

-- Seed default tenant for development
INSERT INTO public.tenants (name, code, schema_name, country_code, currency_code, timezone, status)
VALUES ('UniBank Default', 'unibank_default', 'tenant_unibank_default', 'ZAF', 'ZAR', 'Africa/Johannesburg', 'active');

-- Provision the default tenant schema
SELECT public.provision_tenant_schema('tenant_unibank_default');

-- Seed system configuration
INSERT INTO public.system_config (key, value, description) VALUES
('otp.length', '6', 'OTP digit length'),
('otp.ttl_seconds', '300', 'OTP time-to-live in seconds'),
('otp.max_attempts', '3', 'Maximum OTP verification attempts'),
('pin.min_length', '4', 'Minimum PIN length'),
('pin.max_length', '6', 'Maximum PIN length'),
('pin.max_attempts', '3', 'Maximum PIN entry attempts before lock'),
('pin.lock_duration_minutes', '30', 'PIN lock duration in minutes'),
('transaction.daily_limit_default', '1000.00', 'Default daily transaction limit'),
('transaction.monthly_limit_default', '5000.00', 'Default monthly transaction limit'),
('session.timeout_minutes', '15', 'Session auto-timeout in minutes'),
('kyc.level_0_daily_limit', '500.00', 'Daily limit for KYC level 0'),
('kyc.level_1_daily_limit', '2000.00', 'Daily limit for KYC level 1'),
('kyc.level_2_daily_limit', '10000.00', 'Daily limit for KYC level 2'),
('kyc.level_3_daily_limit', '50000.00', 'Daily limit for KYC level 3');

-- Seed bill providers
INSERT INTO public.bill_providers (name, code, category, countries) VALUES
('Eskom', 'eskom', 'electricity', '{ZAF}'),
('City Power', 'city_power', 'electricity', '{ZAF}'),
('Rand Water', 'rand_water', 'water', '{ZAF}'),
('Vodacom', 'vodacom', 'airtime', '{ZAF}'),
('MTN', 'mtn', 'airtime', '{ZAF}'),
('Cell C', 'cell_c', 'airtime', '{ZAF}'),
('Telkom', 'telkom', 'airtime', '{ZAF}'),
('DStv', 'dstv', 'television', '{ZAF}'),
('ZESA', 'zesa', 'electricity', '{ZWE}'),
('Econet', 'econet', 'airtime', '{ZWE}'),
('BPC', 'bpc', 'electricity', '{BWA}'),
('Orange', 'orange_bw', 'airtime', '{BWA}');
