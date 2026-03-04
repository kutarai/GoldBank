# STORY-015: Account Profile View & Edit

**Epic:** EPIC-002
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a registered user
I want to view and edit my profile details
So that my information is always up to date

---

## Description

### Background
Once a user's account is activated (STORY-013), they need the ability to view their profile information and update certain fields. In UniBank's model, some fields are sourced from the KYC process (full name, ID number, phone number) and are therefore read-only -- they represent verified identity data that cannot be changed without a new KYC review. Other fields (display name, email, address, preferred language) are user-managed and can be updated freely.

This is particularly important in the Southern African context where users may move between regions, change their preferred language (e.g., from Chichewa to English in Malawi), or want to update their communication preferences. The profile view also serves as a trust signal -- users can verify that UniBank has their correct details on file.

All profile changes are logged in an audit trail for compliance purposes, ensuring a complete history of data modifications.

### Scope
**In scope:**
- `GetProfile` gRPC endpoint returning full user profile
- `UpdateProfile` gRPC endpoint for modifiable fields
- Editable fields: `display_name`, `email`, `address`, `preferred_language`
- Read-only fields: `full_name`, `id_number`, `phone_number` (sourced from KYC)
- Field-level validation (email format, address length, supported languages)
- Audit log entry for every profile change (before/after values)
- Immediate persistence of changes

**Out of scope:**
- Profile photo upload (future enhancement)
- Phone number change (requires KYC re-verification, separate story)
- KYC field correction request workflow
- Profile data export (GDPR-style, future story)
- Notification preferences management (separate story)

### User Flow
1. User navigates to "My Profile" from the app menu or settings
2. App calls `AccountService.GetProfile` with the user's account ID (from JWT)
3. Server returns the complete profile with field metadata (editable/read-only)
4. App displays the profile with editable fields as input fields and read-only fields as static text
5. User modifies one or more editable fields
6. User taps "Save Changes"
7. App calls `AccountService.UpdateProfile` with the modified fields
8. Server validates each field:
   - `display_name`: 2-100 characters, no special characters except spaces and hyphens
   - `email`: valid email format (optional field)
   - `address`: max 500 characters
   - `preferred_language`: must be in tenant's supported language list
9. Server persists changes and creates an audit log entry
10. Server returns the updated profile
11. App displays success confirmation

---

## Acceptance Criteria

- [ ] `GetProfile` returns all user profile fields including both editable and read-only fields
- [ ] Each field in the response includes metadata indicating whether it is editable or read-only
- [ ] `UpdateProfile` allows modification of: `display_name`, `email`, `address`, `preferred_language`
- [ ] `UpdateProfile` rejects attempts to modify read-only fields (`full_name`, `id_number`, `phone_number`) with a clear error
- [ ] Field validation is enforced: invalid email format, overly long fields, or unsupported languages are rejected with field-specific error messages
- [ ] Changes are persisted immediately and reflected in subsequent `GetProfile` calls
- [ ] Every profile change is logged in the audit trail with before and after values, timestamp, and actor
- [ ] `GetProfile` is only accessible by the authenticated account owner (authorization enforced)
- [ ] `UpdateProfile` with no actual changes (same values submitted) returns success without creating an audit entry

---

## Technical Notes

### Components
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: Add `GetProfile` and `UpdateProfile` gRPC methods
  - `ProfileValidator.cs`: Field-level validation rules
  - `AccountRepository.cs`: Profile read/write operations
- **AuditModule** (`src/Modules/Audit/`):
  - `AuditService.cs`: Log profile changes with before/after diff
  - `AuditLogRepository.cs`: Persist audit entries
- **SharedKernel** (`src/SharedKernel/`):
  - `LanguageConfiguration.cs`: Tenant-supported language list

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  rpc GetProfile(GetProfileRequest) returns (GetProfileResponse);
  rpc UpdateProfile(UpdateProfileRequest) returns (UpdateProfileResponse);
}

message GetProfileRequest {
  string account_id = 1;               // Validated against JWT subject
}

message GetProfileResponse {
  string account_id = 1;
  string account_number = 2;
  ProfileField full_name = 3;
  ProfileField id_number = 4;
  ProfileField phone_number = 5;
  ProfileField display_name = 6;
  ProfileField email = 7;
  ProfileField address = 8;
  ProfileField preferred_language = 9;
  string account_status = 10;
  string kyc_status = 11;
  google.protobuf.Timestamp created_at = 12;
  google.protobuf.Timestamp last_updated = 13;
}

message ProfileField {
  string value = 1;
  bool is_editable = 2;
  string field_name = 3;
}

message UpdateProfileRequest {
  string account_id = 1;
  optional string display_name = 2;
  optional string email = 3;
  optional string address = 4;
  optional string preferred_language = 5;
}

message UpdateProfileResponse {
  bool success = 1;
  GetProfileResponse updated_profile = 2;
  string message = 3;
  repeated FieldError errors = 4;       // Field-specific validation errors
}

message FieldError {
  string field_name = 1;
  string error_message = 2;
}
```

### Database Changes

No new tables are required. This story operates on the existing `accounts` table.

**Column additions to `accounts` table (if not already present):**

```sql
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS display_name VARCHAR(100);
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS email VARCHAR(255);
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS address VARCHAR(500);
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS preferred_language VARCHAR(10) DEFAULT 'en';
```

**Audit log table** (if not already created by shared audit module):

```sql
CREATE TABLE IF NOT EXISTS audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type VARCHAR(50) NOT NULL,
    entity_id UUID NOT NULL,
    action VARCHAR(50) NOT NULL,
    actor_id UUID NOT NULL,
    actor_type VARCHAR(30) NOT NULL DEFAULT 'user',
    changes JSONB NOT NULL,               -- {"field": {"old": "value1", "new": "value2"}}
    ip_address INET,
    device_id VARCHAR(64),
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_log_entity ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_log_actor ON audit_log(actor_id);
CREATE INDEX idx_audit_log_created_at ON audit_log(created_at);
```

### Security Considerations
- **Authorization:** `GetProfile` and `UpdateProfile` must verify that the `account_id` in the request matches the `sub` claim in the JWT. Users cannot view or edit other users' profiles.
- **Input Sanitization:** All editable fields must be sanitized to prevent XSS and SQL injection. HTML tags stripped from all text fields.
- **Email Privacy:** If email is provided, it should not be exposed in any public-facing API or notification without user consent.
- **Audit Integrity:** Audit log entries are append-only. No updates or deletes permitted on the audit_log table. The `changes` field stores the diff in a structured JSONB format for queryability.
- **Rate Limiting:** `UpdateProfile` limited to 10 calls per hour per account to prevent abuse.
- **PII Handling:** Profile data is PII. Access logs and audit trails must comply with the tenant's data protection regulations.

### Edge Cases
- **Empty optional fields:** If a user clears an optional field (e.g., removes email), the field should be set to NULL. The API should accept empty strings and treat them as NULL.
- **Concurrent updates:** If two requests update the profile simultaneously, use optimistic concurrency (version column on accounts table). Second request receives a conflict error and must retry with fresh data.
- **Language not supported by tenant:** If the requested language is not in the tenant's supported language list, return a validation error with the list of supported languages.
- **Profile fetch for pending_kyc account:** Should still return available data (phone number, basic registration info) even before KYC completion.
- **Unicode characters in display_name:** Support Unicode (for local names in Chichewa, Portuguese, etc.) but reject control characters and emoji.
- **Very long address strings:** Enforce the 500-character limit server-side. Consider structured address fields in a future iteration.

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Activation on KYC Approval (account must be active for full profile access)

**Blocked Stories:**
- None directly, but profile data is consumed by many downstream features

**External Dependencies:**
- None

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] GetProfile returns correct data with proper editable/read-only field metadata
- [ ] UpdateProfile validates all fields correctly and persists changes
- [ ] Read-only field modification attempts are rejected
- [ ] Audit log entries verified for all profile changes
- [ ] Authorization verified: users cannot access other users' profiles
- [ ] Unicode character support verified for display_name and address fields

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
