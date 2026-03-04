# STORY-051: Merchant Profile Management

**Epic:** EPIC-010
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a merchant
I want to view and update my business profile
So that my details are accurate

---

## Description

### Background
After successful merchant registration (STORY-050), merchants need the ability to view their business profile and update certain operational details. Keeping the merchant profile current is important for several reasons: the business address and GPS coordinates power the agent/merchant locator feature used by customers to find nearby service points, operating hours inform users when the merchant is available, and the display name is shown to customers during payment transactions.

Similar to personal profile management (STORY-015), some fields are sourced from the merchant KYC process and are read-only (business registration details, owner information), while operational fields can be updated freely. GPS coordinates are particularly important -- they must be kept current because the agent/merchant locator (a future feature) will use them to help users find the nearest cash-in/cash-out point, which is a core value proposition for serving unbanked populations in Southern Africa.

### Scope
**In scope:**
- `MerchantService.GetProfile` gRPC endpoint returning full merchant profile
- `MerchantService.UpdateProfile` gRPC endpoint for modifiable fields
- Editable fields: `display_name`, `business_address`, `operating_hours`, `gps_coordinates`
- Read-only fields: `business_registration_number`, `owner_account_id`, `merchant_code`, `business_type`
- GPS coordinate update for accurate locator positioning
- Operating hours in structured format (per day of week)
- Immediate persistence of changes
- Audit log entry for every profile change

**Out of scope:**
- Merchant profile photo or logo upload
- Business type change (requires re-KYC)
- Ownership transfer between personal accounts
- Operating hours enforcement (blocking transactions outside operating hours)
- Public merchant directory or search API (future feature)
- Merchant rating or review system

### User Flow
1. Merchant navigates to "Business Profile" from the merchant dashboard
2. App calls `MerchantService.GetProfile` with the merchant ID
3. Server returns the complete merchant profile with field metadata (editable/read-only)
4. App displays the profile:
   - Read-only section: Merchant ID, business registration number, owner name, business type
   - Editable section: Display name, address, GPS location, operating hours
5. Merchant taps "Edit" on the editable section
6. Merchant modifies one or more fields:
   - Display name: how the business appears to customers
   - Address: updated business address
   - GPS: tap "Update Location" to capture current device GPS or enter manually
   - Operating hours: set hours for each day of the week (or mark as closed)
7. Merchant taps "Save Changes"
8. App calls `MerchantService.UpdateProfile` with the modified fields
9. Server validates each field
10. Server persists changes and creates audit log entry
11. Server returns the updated profile
12. App displays success confirmation
13. Updated GPS coordinates are immediately available for the locator feature

---

## Acceptance Criteria

- [ ] `GetProfile` returns all merchant profile fields including both editable and read-only fields
- [ ] Each field in the response includes metadata indicating whether it is editable or read-only
- [ ] `UpdateProfile` allows modification of: `display_name`, `business_address`, `operating_hours`, `gps_coordinates`
- [ ] `UpdateProfile` rejects attempts to modify read-only fields (`business_registration_number`, `owner_account_id`, `merchant_code`, `business_type`)
- [ ] GPS coordinates are validated (latitude -90 to 90, longitude -180 to 180)
- [ ] Operating hours are stored in a structured format supporting per-day-of-week hours
- [ ] Changes are persisted immediately and reflected in subsequent `GetProfile` calls
- [ ] Every profile change is logged in the audit trail with before and after values
- [ ] Only the merchant owner (authenticated account matching `owner_account_id`) can view and edit the profile
- [ ] Updated GPS coordinates are reflected in spatial queries immediately (no caching delay)
- [ ] `UpdateProfile` with no actual changes returns success without creating an audit entry

---

## Technical Notes

### Components
- **MerchantModule** (`src/Modules/Merchant/`):
  - `MerchantService.cs`: Add `GetProfile` and `UpdateProfile` gRPC methods
  - `MerchantProfileValidator.cs`: Field-level validation rules
  - `MerchantRepository.cs`: Profile read/write operations
- **AuditModule** (`src/Modules/Audit/`):
  - `AuditService.cs`: Log merchant profile changes (reused from STORY-015)
- **SharedKernel** (`src/SharedKernel/`):
  - `OperatingHours.cs`: Structured operating hours value object
  - `GpsCoordinates.cs`: GPS coordinate value object with validation

### API / gRPC Endpoints

**Service:** `MerchantService`

```protobuf
service MerchantService {
  rpc GetProfile(GetMerchantProfileRequest) returns (GetMerchantProfileResponse);
  rpc UpdateProfile(UpdateMerchantProfileRequest) returns (UpdateMerchantProfileResponse);
}

message GetMerchantProfileRequest {
  string merchant_id = 1;
}

message GetMerchantProfileResponse {
  string merchant_id = 1;
  string merchant_code = 2;
  MerchantProfileField business_name = 3;
  MerchantProfileField display_name = 4;
  MerchantProfileField business_type = 5;
  MerchantProfileField business_registration_number = 6;
  MerchantProfileField business_address = 7;
  MerchantProfileField owner_name = 8;
  GpsLocation gps_location = 9;
  OperatingHoursSchedule operating_hours = 10;
  bool is_agent = 11;
  string status = 12;
  string kyc_status = 13;
  google.protobuf.Timestamp created_at = 14;
  google.protobuf.Timestamp last_updated = 15;
}

message MerchantProfileField {
  string value = 1;
  bool is_editable = 2;
  string field_name = 3;
}

message GpsLocation {
  double latitude = 1;
  double longitude = 2;
  double accuracy_meters = 3;
}

message OperatingHoursSchedule {
  repeated DayHours days = 1;
}

message DayHours {
  string day_of_week = 1;               // "monday", "tuesday", ..., "sunday"
  bool is_open = 2;
  string open_time = 3;                  // "08:00" (24-hour format)
  string close_time = 4;                 // "17:00"
}

message UpdateMerchantProfileRequest {
  string merchant_id = 1;
  optional string display_name = 2;
  optional string business_address = 3;
  optional GpsLocation gps_location = 4;
  optional OperatingHoursSchedule operating_hours = 5;
}

message UpdateMerchantProfileResponse {
  bool success = 1;
  GetMerchantProfileResponse updated_profile = 2;
  string message = 3;
  repeated MerchantFieldError errors = 4;
}

message MerchantFieldError {
  string field_name = 1;
  string error_message = 2;
}
```

### Database Changes

**Column additions to `merchants` table:**

```sql
ALTER TABLE merchants ADD COLUMN IF NOT EXISTS display_name VARCHAR(200);
ALTER TABLE merchants ADD COLUMN IF NOT EXISTS operating_hours JSONB;
```

**Operating Hours JSON Schema:**

```json
{
  "monday": { "is_open": true, "open_time": "08:00", "close_time": "17:00" },
  "tuesday": { "is_open": true, "open_time": "08:00", "close_time": "17:00" },
  "wednesday": { "is_open": true, "open_time": "08:00", "close_time": "17:00" },
  "thursday": { "is_open": true, "open_time": "08:00", "close_time": "17:00" },
  "friday": { "is_open": true, "open_time": "08:00", "close_time": "17:00" },
  "saturday": { "is_open": true, "open_time": "09:00", "close_time": "13:00" },
  "sunday": { "is_open": false, "open_time": null, "close_time": null }
}
```

**Spatial index update** (if not created in STORY-050):

```sql
-- Ensure spatial index exists for location-based queries
CREATE INDEX IF NOT EXISTS idx_merchants_location ON merchants USING GIST (
    ST_SetSRID(ST_MakePoint(gps_longitude, gps_latitude), 4326)
) WHERE gps_latitude IS NOT NULL AND gps_longitude IS NOT NULL;
```

### Security Considerations
- **Authorization:** Only the merchant owner (the account whose `owner_account_id` matches the JWT `sub` claim) can view or edit the merchant profile. No cross-merchant access.
- **Input Sanitization:** All text fields sanitized to prevent XSS and injection attacks. HTML tags stripped.
- **GPS Privacy:** Merchant GPS coordinates are semi-public (used for the locator). Merchants consent to location sharing as part of registration. However, coordinates should not be shared with precision beyond what is needed for the locator (~50 meter accuracy).
- **Audit Integrity:** Profile changes are logged with full before/after diff. Audit entries are append-only.
- **Rate Limiting:** Profile updates limited to 20 per hour per merchant to prevent abuse.
- **Operating Hours Validation:** Times validated to ensure `open_time < close_time` and both are valid 24-hour format times. Invalid schedules rejected with specific error messages.

### Edge Cases
- **Merchant profile accessed by non-owner:** Return `PERMISSION_DENIED`. The requesting account must be the `owner_account_id`.
- **GPS coordinates far from business address:** Accept the update but log a warning if the GPS coordinates change by more than 10km from the previous position. This could indicate GPS spoofing or a genuine relocation.
- **Operating hours spanning midnight:** Support next-day close times (e.g., open_time: "22:00", close_time: "02:00") by storing the close time as-is and handling the wraparound in the locator logic.
- **Display name set to empty:** If display_name is cleared, fall back to `business_name` for display purposes. The `display_name` field is optional.
- **Concurrent profile updates:** Use optimistic concurrency with a version column on the merchants table. Second concurrent update receives a conflict error.
- **Merchant account suspended:** Allow `GetProfile` (read-only) for suspended merchants so the owner can view their details. Block `UpdateProfile` with a clear message.
- **Invalid operating hours format:** Return field-specific validation errors. Common mistakes: "8:00" instead of "08:00", using AM/PM instead of 24-hour format.
- **All days marked as closed:** Accept the update but log a warning. A merchant with no operating hours may confuse customers using the locator.
- **Unicode in display_name and address:** Support full Unicode for local language names (Chichewa, Portuguese, Swahili, etc.). Reject control characters.

---

## Dependencies

**Prerequisite Stories:**
- STORY-050: Merchant Registration & KYC (merchant record must exist)

**Blocked Stories:**
- Future merchant/agent locator feature (uses GPS coordinates and operating hours)
- Future merchant payment acceptance (uses display_name for customer-facing transactions)

**External Dependencies:**
- PostGIS extension for PostgreSQL (for spatial indexing, shared with STORY-050)

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
- [ ] Read-only field modification attempts are rejected with clear error
- [ ] GPS coordinate updates reflected immediately in spatial queries
- [ ] Operating hours stored and retrieved correctly in structured format
- [ ] Audit log entries verified for all profile changes
- [ ] Authorization verified: non-owner access denied
- [ ] Unicode character support verified for display_name and address

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
