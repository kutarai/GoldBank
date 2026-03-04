-- ============================================================================
-- UniBank Multi-Tenant Database Schema
-- 001_init_schema.sql - Public schema tables
-- ============================================================================

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- public.tenants
CREATE TABLE public.tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    schema_name VARCHAR(63) NOT NULL UNIQUE,
    config_json JSONB NOT NULL DEFAULT '{}',
    branding_json JSONB NOT NULL DEFAULT '{}',
    status VARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'suspended', 'decommissioned')),
    max_users INT NOT NULL DEFAULT 1000000,
    country_code VARCHAR(3) NOT NULL DEFAULT 'ZAF',
    currency_code VARCHAR(3) NOT NULL DEFAULT 'ZAR',
    timezone VARCHAR(50) NOT NULL DEFAULT 'Africa/Johannesburg',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- public.bill_providers
CREATE TABLE public.bill_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    category VARCHAR(50) NOT NULL,
    config JSONB NOT NULL DEFAULT '{}',
    countries TEXT[] NOT NULL DEFAULT '{ZAF}',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- public.system_config
CREATE TABLE public.system_config (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key VARCHAR(200) NOT NULL,
    value JSONB NOT NULL,
    tenant_id UUID REFERENCES public.tenants(id),
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(key, tenant_id)
);
