# STORY-050: Merchant Registration & KYC

**Epic:** EPIC-010
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a merchant
I want to register my business with KYC
So that I can accept payments and act as agent

---

## Description

### Background
Merchants are a vital part of GoldBank's ecosystem in Southern Africa. They serve a dual purpose: accepting digital payments for goods and services, and optionally acting as agents who facilitate cash-in and cash-out for other users. In many parts of the region, agent networks are the primary way unbanked and underbanked populations access financial services -- agents are often small shop owners, market vendors, or mobile money kiosks.

Merchant registration requires the business owner to already have an active personal GoldBank account (completed personal KYC). The merchant registration process adds business-level KYC, including business name, type, location (GPS coordinates for the agent/merchant locator), and business registration document verification. The `is_agent` flag determines whether the merchant can perform cash-in/cash-out operations on behalf of other users, which is a regulated activity requiring additional compliance checks in most Southern African markets.

Each merchant receives a unique merchant ID that is used for payment acceptance, agent operations, and merchant-to-merchant transactions.

### Scope
**In scope:**
- `MerchantService.Register` gRPC endpoint for merchant registration
- Business details collection: business name, type, location (GPS), registration number
- Business KYC: upload business registration document
- Merchant KYC validation (document verification)
- Unique merchant ID generation
- Agent flag (`is_agent`) configurable at registration
- Merchant status management: `pending_kyc`, `active`, `suspended`
- Validation that the owner has an active personal account
- Audit logging of merchant registration

**Out of scope:**
- Agent cash-in/cash-out operations (future sprint stories)
- Merchant payment acceptance flow (future sprint)
- Merchant onboarding fee collection
- Agent float management and limits
- Merchant tier system (small, medium, large)
- Merchant aggregation or chain management

### User Flow
1. User with an active personal account navigates to "Register as Merchant" in the app
2. App verifies the user's personal account is active (status = `active`, KYC = `approved`)
3. User fills in business details:
   - Business name (required)
   - Business type (dropdown: retail, food_service, transport, telecom, general_agent, other)
   - Business registration number (optional in some markets)
   - Business address (required)
   - GPS location (captured from device or entered manually)
4. User selects whether to register as an agent (`is_agent` checkbox)
5. If registering as agent: app displays additional agent terms and conditions
6. User uploads business registration document (using gRPC streaming similar to STORY-011)
7. App sends `MerchantService.Register` request with all business details
8. Server validates:
   - Owner's personal account is active
   - Business name is unique within the tenant
   - GPS coordinates are valid
   - Document is valid (format, size)
9. Server creates merchant record with status `pending_kyc`
10. Server encrypts and stores the business registration document
11. Server generates a unique merchant ID (format: `MRC-{TENANT}-{SEQUENTIAL}`)
12. Server returns registration confirmation with merchant ID and status
13. Merchant KYC is reviewed (auto or manual depending on tenant config)
14. On KYC approval, merchant status transitions to `active`
15. User receives notification: "Your merchant account is now active. Merchant ID: MRC-XXX"

---

## Acceptance Criteria

- [ ] Only users with an active personal account (status = `active`, KYC = `approved`) can register as a merchant
- [ ] Business details are collected: business name, business type, registration number, address, GPS location
- [ ] Business registration document can be uploaded and is stored encrypted
- [ ] Merchant KYC status is validated (document verified)
- [ ] A unique merchant ID is generated in the format `MRC-{TENANT_CODE}-{SEQUENTIAL_NUMBER}`
- [ ] The `is_agent` flag is configurable at registration time
- [ ] Agent registration displays additional terms and conditions that must be accepted
- [ ] Merchant record is created with status `pending_kyc`
- [ ] Merchant status transitions to `active` upon KYC approval
- [ ] Registration is logged in the audit trail
- [ ] Duplicate business name within the same tenant is rejected
- [ ] GPS coordinates are validated (latitude -90 to 90, longitude -180 to 180)

---

## Technical Notes

### Components
- **MerchantModule** (`src/Modules/Merchant/`):
  - `MerchantService.cs`: gRPC service for merchant operations
  - `MerchantRegistrationHandler.cs`: Registration business logic
  - `MerchantKYCService.cs`: Business document verification
  - `MerchantIdGenerator.cs`: Unique merchant ID generation
  - `MerchantRepository.cs`: Data access for merchants table
- **KYCModule** (`src/Modules/KYC/`):
  - `DocumentEncryptionService.cs`: Reused from STORY-011 for business document encryption
  - `DocumentStorageService.cs`: Reused for business document storage
- **AccountModule** (`src/Modules/Account/`):
  - Account status verification for owner validation
- **NotificationModule** (`src/Modules/Notification/`):
  - Merchant activation notification

### API / gRPC Endpoints

**Service:** `MerchantService`

```protobuf
service MerchantService {
  rpc Register(RegisterMerchantRequest) returns (RegisterMerchantResponse);
  rpc UploadBusinessDocument(stream UploadBusinessDocumentRequest) returns (UploadBusinessDocumentResponse);
  rpc GetMerchantStatus(GetMerchantStatusRequest) returns (GetMerchantStatusResponse);
}

message RegisterMerchantRequest {
  string owner_account_id = 1;
  string business_name = 2;
  string business_type = 3;               // "retail", "food_service", "transport", "telecom", "general_agent", "other"
  string business_registration_number = 4; // Optional in some markets
  string business_address = 5;
  GpsLocation location = 6;
  bool is_agent = 7;
  bool agent_terms_accepted = 8;          // Required if is_agent = true
  string tenant_id = 9;
}

message GpsLocation {
  double latitude = 1;
  double longitude = 2;
  double accuracy_meters = 3;             // GPS accuracy indicator
}

message RegisterMerchantResponse {
  string merchant_id = 1;                 // Generated unique ID
  string status = 2;                      // "pending_kyc"
  string message = 3;
  google.protobuf.Timestamp created_at = 4;
}

message UploadBusinessDocumentRequest {
  oneof payload {
    BusinessDocumentMetadata metadata = 1;
    bytes chunk = 2;
  }
}

message BusinessDocumentMetadata {
  string merchant_id = 1;
  string document_type = 2;              // "business_registration", "tax_certificate", "license"
  string file_name = 3;
  string content_type = 4;
  int64 file_size = 5;
}

message UploadBusinessDocumentResponse {
  string document_id = 1;
  string status = 2;
  string message = 3;
}

message GetMerchantStatusRequest {
  string merchant_id = 1;
}

message GetMerchantStatusResponse {
  string merchant_id = 1;
  string business_name = 2;
  string status = 3;
  string kyc_status = 4;
  bool is_agent = 5;
  google.protobuf.Timestamp last_updated = 6;
}
```

### Database Changes

**Table:** `merchants` (schema: `{tenant_schema}`)

```sql
CREATE TABLE merchants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_code VARCHAR(30) NOT NULL UNIQUE,   -- MRC-{TENANT}-{SEQ}
    owner_account_id UUID NOT NULL REFERENCES accounts(id),
    business_name VARCHAR(200) NOT NULL,
    business_type VARCHAR(50) NOT NULL,
    business_registration_number VARCHAR(100),
    business_address VARCHAR(500) NOT NULL,
    gps_latitude DECIMAL(10,7),
    gps_longitude DECIMAL(10,7),
    gps_accuracy_meters DECIMAL(8,2),
    is_agent BOOLEAN NOT NULL DEFAULT FALSE,
    agent_terms_accepted BOOLEAN NOT NULL DEFAULT FALSE,
    agent_terms_accepted_at TIMESTAMPTZ,
    status VARCHAR(30) NOT NULL DEFAULT 'pending_kyc',
    kyc_status VARCHAR(30) NOT NULL DEFAULT 'pending',
    tenant_id UUID NOT NULL,
    activated_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_merchant_owner FOREIGN KEY (owner_account_id) REFERENCES accounts(id),
    CONSTRAINT uq_merchant_business_name_tenant UNIQUE (business_name, tenant_id)
);

CREATE INDEX idx_merchants_owner ON merchants(owner_account_id);
CREATE INDEX idx_merchants_status ON merchants(status);
CREATE INDEX idx_merchants_tenant ON merchants(tenant_id);
CREATE INDEX idx_merchants_location ON merchants USING GIST (
    ST_SetSRID(ST_MakePoint(gps_longitude, gps_latitude), 4326)
) WHERE gps_latitude IS NOT NULL AND gps_longitude IS NOT NULL;
CREATE INDEX idx_merchants_is_agent ON merchants(is_agent) WHERE is_agent = TRUE;
```

**Table:** `merchant_documents` (schema: `{tenant_schema}`)

```sql
CREATE TABLE merchant_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(id),
    document_type VARCHAR(50) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(50) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    encryption_key_ref VARCHAR(255) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'uploaded',
    checksum_sha256 VARCHAR(64) NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_merchant_doc_merchant FOREIGN KEY (merchant_id) REFERENCES merchants(id)
);

CREATE INDEX idx_merchant_documents_merchant ON merchant_documents(merchant_id);
```

**Sequence for Merchant Code Generation:**

```sql
CREATE SEQUENCE merchant_code_seq START WITH 10001;
-- Merchant code: MRC-{TENANT_CODE}-{nextval('merchant_code_seq')}
-- Example: MRC-MW-10001, MRC-ZM-10002
```

### Security Considerations
- **Owner Verification:** The `owner_account_id` must match the JWT `sub` claim. A user cannot register a merchant on behalf of another user.
- **Document Security:** Business registration documents follow the same encryption-at-rest standard as personal KYC documents (STORY-011). AES-256-GCM with HSM-managed keys.
- **Agent Compliance:** The `is_agent` flag has regulatory implications. Agent registration may require additional compliance checks depending on the tenant's regulatory environment. The `agent_terms_accepted` flag and timestamp provide an audit trail for consent.
- **GPS Spoofing:** GPS coordinates are captured at the time of registration and can be updated later. However, GPS spoofing is a known risk. For agent operations, consider periodic location verification (future enhancement).
- **Business Name Uniqueness:** Enforced at the database level per tenant to prevent confusion and potential fraud.
- **Rate Limiting:** Merchant registration limited to 1 attempt per personal account per 24 hours to prevent spam registrations.

### Edge Cases
- **Owner account suspended after merchant registration:** Merchant account should also be suspended. Implement a Wolverine event handler that suspends related merchant accounts when a personal account is suspended.
- **Duplicate merchant registration:** If the owner already has an active or pending merchant, reject the registration with a clear message. One personal account = one merchant account.
- **GPS coordinates not available:** Allow registration without GPS (set to NULL) but flag for manual review. GPS is important for the merchant/agent locator but should not block registration.
- **Business registration number format varies by country:** Accept as free-text string. Tenant-specific validation rules can be added as needed.
- **Agent terms not accepted but is_agent = true:** Reject registration with a validation error. Agent terms must be explicitly accepted.
- **Very long business names:** Enforce 200-character limit. Support Unicode for local language business names.
- **Merchant code collision:** The database sequence ensures uniqueness. If the sequence is somehow corrupted, the unique constraint on `merchant_code` prevents duplicates.
- **Multiple owners for same business:** Not supported in initial release. One merchant per owner account. Business partnership support is a future enhancement.

---

## Dependencies

**Prerequisite Stories:**
- STORY-003: Project Architecture & Module Structure (Merchant module scaffolding)

**Blocked Stories:**
- STORY-051: Merchant Profile Management (requires merchant record to exist)
- Future agent operation stories (cash-in, cash-out)
- Future payment acceptance stories

**External Dependencies:**
- PostGIS extension for PostgreSQL (for spatial indexing of merchant locations)
- HSM or Key Management Service (shared with personal KYC document encryption)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Merchant registration verified end-to-end: business details, document upload, merchant ID generation
- [ ] Owner validation verified: only active accounts can register merchants
- [ ] Agent flag verified: agent terms acceptance enforced when is_agent = true
- [ ] Business name uniqueness verified within tenant
- [ ] GPS spatial index verified: location queries work correctly
- [ ] Document encryption verified: business documents stored encrypted
- [ ] Merchant code generation verified: sequential, unique, properly formatted

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
