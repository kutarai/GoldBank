# STORY-068: Configurable Branding per Tenant

**Epic:** EPIC-013 White-Label Configuration
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As a **deploying institution**
I want to **customize app branding (logo, colors, name)**
So that **the platform reflects my brand and my customers see a familiar experience**

---

## Description

### Background

GoldBank is a white-label platform — every deploying institution (tenant) should experience the platform as their own product, not as a generic banking app. When a customer in Zambia opens "Zanaco Mobile" and a customer in Malawi opens "NBS Direct", they should each see their own bank's branding despite both running on the same GoldBank platform underneath. This is the core of the white-label value proposition.

Branding customization must be achievable without code deployment. A deploying institution's marketing team should be able to update their logo, adjust brand colors, and change the app name through an admin interface. The changes propagate to both the mobile application (which fetches tenant configuration at login) and the Blazor admin portal (which uses CSS variables driven by tenant configuration).

Default branding is provided for tenants that have not yet configured their brand assets. This ensures the platform is functional immediately after tenant onboarding, while giving institutions the flexibility to brand at their own pace.

Branding configuration is cached in Redis to minimize database lookups on every request. A 5-minute TTL ensures that brand changes propagate within a reasonable timeframe without requiring cache invalidation.

**Functional Requirement:** FR-055

### Scope

**In scope:**
- Tenant branding configuration storage: app name, colors (primary, secondary, accent), logo URL, splash screen URL, font family
- `AdminService.UpdateTenantBranding` gRPC endpoint for branding management
- Branding asset (logo, splash) upload and secure storage
- Mobile app: fetch tenant branding on login, apply theme dynamically
- Blazor admin portal: CSS custom properties driven by tenant branding configuration
- Default branding fallback for unconfigured tenants
- Redis caching of branding configuration with 5-minute TTL
- Branding preview endpoint (see changes before publishing)
- Branding version history (ability to revert to previous branding)

**Out of scope:**
- Custom domain/URL configuration per tenant (separate infrastructure story)
- Custom email templates per tenant (separate story)
- Terminal UI branding (terminals have limited display customization)
- Custom login page layouts (only colors and assets are customizable)
- App store listing customization (handled by institution's deployment team)

### User Flow

**Admin configures branding:**
1. Admin logs into the admin portal for their institution
2. Admin navigates to Settings > Branding
3. Admin sees the current branding configuration: app name, primary color, secondary color, accent color, logo, splash screen, font family
4. Admin uploads a new logo (PNG/SVG, max 2MB, min 200x200px)
5. Admin adjusts the primary color using a color picker
6. Admin changes the app display name
7. Admin clicks "Preview" — a side panel shows a mockup of the mobile app with the new branding
8. Admin clicks "Publish" — the branding configuration is saved to the database and the Redis cache is invalidated
9. Within 5 minutes, all mobile app sessions for this tenant reflect the new branding (on next config fetch)
10. The Blazor admin portal immediately reflects the new branding for the admin (CSS variables updated)

**Mobile app applies branding:**
1. Customer opens the mobile app and enters their credentials
2. App authenticates and receives a JWT containing the tenant ID
3. App calls the branding endpoint to fetch tenant-specific branding configuration
4. App applies the branding: sets navigation bar color, button colors, app title, logo in header/splash
5. Branding configuration is cached locally on the device for offline use
6. On each app launch, the app checks for updated branding (ETag-based conditional fetch)

---

## Acceptance Criteria

- [ ] Logo, primary color, secondary color, accent color, app name, splash screen, and font family are configurable per tenant via `AdminService.UpdateTenantBranding` gRPC endpoint
- [ ] Branding changes are applied across the mobile app (on next config fetch) and Blazor admin portal (immediately for the current admin, within 5 minutes for other sessions)
- [ ] Branding changes do not require a code deployment — configuration-only change
- [ ] Default branding (GoldBank defaults) is applied for tenants that have not configured custom branding
- [ ] Branding configuration is cached in Redis with key pattern `tenant_config:{tenant_id}` and 5-minute TTL
- [ ] Logo and splash screen assets are stored securely and served via authenticated endpoint (no public URLs for brand assets)
- [ ] Brand assets are validated: logo must be PNG or SVG, max 2MB, minimum dimensions 200x200px; splash screen must be PNG or JPG, max 5MB
- [ ] Branding preview is available before publishing — admin can see a mockup of changes before they go live
- [ ] Branding version history is maintained — admin can revert to a previous branding configuration
- [ ] Tenant branding is isolated — Tenant A's branding configuration is not visible to Tenant B admins

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TenantBrandingService.cs` | `src/Modules/GoldBank.Admin/Services/` | gRPC service for branding management |
| `BrandingConfiguration.cs` | `src/Shared/GoldBank.SharedKernel/Tenancy/` | Branding value object / DTO |
| `BrandingAssetStore.cs` | `src/Modules/GoldBank.Admin/Storage/` | Brand asset upload and retrieval |
| `TenantConfigCache.cs` | `src/Shared/GoldBank.SharedKernel/Caching/` | Redis caching for tenant configuration |
| `ThemeProvider.razor` | `src/Web/GoldBank.AdminPortal/Components/` | Blazor component that injects CSS variables |
| `BrandingPreview.razor` | `src/Web/GoldBank.AdminPortal/Pages/Settings/` | Branding preview page |
| `BrandingHistoryEntity.cs` | `src/Modules/GoldBank.Admin/Domain/` | Branding version history entity |

### API / gRPC Endpoints

**Proto definition** (additions to `admin_service.proto`):

```protobuf
service AdminBrandingService {
  rpc GetTenantBranding (GetBrandingRequest) returns (GetBrandingResponse);
  rpc UpdateTenantBranding (UpdateBrandingRequest) returns (UpdateBrandingResponse);
  rpc UploadBrandAsset (stream UploadAssetRequest) returns (UploadAssetResponse);
  rpc GetBrandAsset (GetAssetRequest) returns (stream GetAssetResponse);
  rpc PreviewBranding (UpdateBrandingRequest) returns (PreviewBrandingResponse);
  rpc GetBrandingHistory (GetBrandingHistoryRequest) returns (GetBrandingHistoryResponse);
  rpc RevertBranding (RevertBrandingRequest) returns (UpdateBrandingResponse);
}

message GetBrandingRequest {
  string tenant_id = 1;
  string if_none_match = 2;       // ETag for conditional fetch (mobile caching)
}

message GetBrandingResponse {
  string app_name = 1;
  string primary_color = 2;       // Hex color code, e.g., "#1A73E8"
  string secondary_color = 3;
  string accent_color = 4;
  string logo_url = 5;            // Authenticated URL to logo asset
  string splash_url = 6;          // Authenticated URL to splash asset
  string font_family = 7;         // CSS font family, e.g., "Inter, sans-serif"
  string etag = 8;                // For conditional fetch
  bool is_default = 9;            // True if using default branding
}

message UpdateBrandingRequest {
  string tenant_id = 1;
  string app_name = 2;
  string primary_color = 3;
  string secondary_color = 4;
  string accent_color = 5;
  string logo_asset_id = 6;       // Reference to uploaded logo asset
  string splash_asset_id = 7;     // Reference to uploaded splash asset
  string font_family = 8;
}

message UpdateBrandingResponse {
  bool success = 1;
  string error_message = 2;
  string version_id = 3;          // Branding version for history/revert
  string etag = 4;
}

message UploadAssetRequest {
  oneof data {
    AssetMetadata metadata = 1;   // First message: file metadata
    bytes chunk = 2;              // Subsequent messages: file chunks
  }
}

message AssetMetadata {
  string tenant_id = 1;
  string asset_type = 2;          // "logo" or "splash"
  string file_name = 3;
  string content_type = 4;        // "image/png", "image/svg+xml", "image/jpeg"
  int64 file_size_bytes = 5;
}

message UploadAssetResponse {
  string asset_id = 1;
  string asset_url = 2;           // Authenticated URL
  bool success = 3;
  string error_message = 4;
}

message PreviewBrandingResponse {
  string preview_url = 1;         // Temporary preview URL (expires in 30 minutes)
  bool success = 2;
  string error_message = 3;
}

message GetBrandingHistoryRequest {
  string tenant_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}

message GetBrandingHistoryResponse {
  repeated BrandingVersion versions = 1;
  int32 total_count = 2;
}

message BrandingVersion {
  string version_id = 1;
  string changed_by = 2;
  int64 changed_at_unix = 3;
  string change_summary = 4;      // e.g., "Updated logo and primary color"
}

message RevertBrandingRequest {
  string tenant_id = 1;
  string version_id = 2;
}
```

### Database Changes

**branding_json column on tenants table:**

```sql
ALTER TABLE shared.tenants
    ADD COLUMN branding_json JSONB NOT NULL DEFAULT '{}'::JSONB;

COMMENT ON COLUMN shared.tenants.branding_json IS 'Tenant branding configuration: app_name, primary_color, secondary_color, accent_color, logo_url, splash_url, font_family';
```

**Branding JSON structure:**

```json
{
  "app_name": "Zanaco Mobile",
  "primary_color": "#1A73E8",
  "secondary_color": "#174EA6",
  "accent_color": "#FBBC04",
  "logo_url": "/api/assets/tenant/TENANT-ZA-001/logo/v3",
  "splash_url": "/api/assets/tenant/TENANT-ZA-001/splash/v2",
  "font_family": "Inter, sans-serif"
}
```

**Default branding (applied when branding_json is empty):**

```json
{
  "app_name": "GoldBank",
  "primary_color": "#0D47A1",
  "secondary_color": "#1565C0",
  "accent_color": "#FF6F00",
  "logo_url": "/api/assets/default/logo",
  "splash_url": "/api/assets/default/splash",
  "font_family": "Roboto, sans-serif"
}
```

**branding_history table:**

```sql
CREATE TABLE shared.branding_history (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(50) NOT NULL,
    version_number  INT NOT NULL,
    branding_json   JSONB NOT NULL,
    changed_by      VARCHAR(100) NOT NULL,
    change_summary  TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, version_number)
);

CREATE INDEX idx_branding_history_tenant ON shared.branding_history (tenant_id, version_number DESC);
```

**brand_assets table:**

```sql
CREATE TABLE shared.brand_assets (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(50) NOT NULL,
    asset_type      VARCHAR(20) NOT NULL CHECK (asset_type IN ('logo', 'splash')),
    file_name       VARCHAR(255) NOT NULL,
    content_type    VARCHAR(50) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    storage_path    TEXT NOT NULL,          -- Path in encrypted file storage
    checksum_sha256 VARCHAR(64) NOT NULL,
    uploaded_by     VARCHAR(100) NOT NULL,
    uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_brand_assets_tenant ON shared.brand_assets (tenant_id, asset_type);
```

### Blazor Admin Portal Integration

**ThemeProvider.razor component** (injects CSS custom properties):

```csharp
// ThemeProvider.razor.cs
public partial class ThemeProvider : ComponentBase
{
    [Inject] private ITenantBrandingService BrandingService { get; set; }
    [Inject] private ITenantProvider TenantProvider { get; set; }

    private BrandingConfiguration _branding;

    protected override async Task OnInitializedAsync()
    {
        var tenantId = TenantProvider.GetCurrentTenantId();
        _branding = await BrandingService.GetBrandingAsync(tenantId);
    }

    // Renders: <style>:root { --primary-color: #1A73E8; --secondary-color: ... }</style>
}
```

**CSS variable usage throughout admin portal:**

```css
:root {
    --primary-color: #0D47A1;       /* Overridden by ThemeProvider */
    --secondary-color: #1565C0;
    --accent-color: #FF6F00;
    --font-family: 'Roboto', sans-serif;
}

.navbar { background-color: var(--primary-color); }
.btn-primary { background-color: var(--primary-color); }
.btn-accent { background-color: var(--accent-color); }
body { font-family: var(--font-family); }
```

### Redis Caching Strategy

```
Key:     tenant_config:{tenant_id}
Value:   Serialized BrandingConfiguration JSON
TTL:     300 seconds (5 minutes)
Evict:   On branding update (explicit cache invalidation via DEL)
```

Cache flow:
1. Request arrives for tenant branding
2. Check Redis cache `tenant_config:{tenant_id}`
3. If hit: return cached branding
4. If miss: query PostgreSQL, populate cache, return branding
5. On branding update: delete cache key, next request repopulates

### Security Considerations

- **Asset Authentication:** Brand assets (logos, splash screens) are served via authenticated endpoints. The request must include a valid JWT with the matching tenant ID. No public URLs prevent brand asset scraping.
- **Asset Encryption:** Uploaded brand assets are encrypted at rest in storage. They are decrypted on retrieval and served over HTTPS.
- **Asset Validation:** Uploaded files are validated for: file type (PNG, SVG, JPEG only — no executables), file size (logo max 2MB, splash max 5MB), dimensions (logo min 200x200), and content type header matching actual file content (no content-type spoofing).
- **Color Validation:** Hex color codes are validated against the pattern `^#[0-9A-Fa-f]{6}$`. Invalid colors are rejected.
- **XSS Prevention:** App name and font family strings are sanitized to prevent CSS/HTML injection. Font family values are validated against an allowed list.
- **Tenant Isolation:** Branding configuration and assets are strictly scoped to the tenant. The Redis cache key includes the tenant ID, and database queries always filter by tenant.
- **Rate Limiting:** The branding update endpoint is rate-limited (max 10 updates per tenant per hour) to prevent abuse.

### Edge Cases

- **Tenant with no branding configured:** Default branding is returned. The `is_default` flag is set to `true` in the response so the client can distinguish between configured and default branding.
- **Invalid logo dimensions:** Upload is rejected with a clear error message specifying minimum dimensions. The admin portal shows the requirement before upload.
- **Large logo file:** Files exceeding the 2MB limit are rejected at the gRPC streaming upload level — the server closes the stream after reading the metadata with file size. This avoids storing oversized files.
- **Concurrent branding updates:** Optimistic concurrency using the ETag. If two admins edit branding simultaneously, the second save receives a conflict error and must refresh before retrying.
- **Redis cache failure:** If Redis is unavailable, the system falls back to direct PostgreSQL query. Branding is served with higher latency but without interruption. A background alert notifies infrastructure of the Redis issue.
- **Mobile app offline:** The mobile app caches the last-known branding configuration locally. If the app cannot fetch updated branding at startup, it uses the cached version. On first launch with no cache, it uses embedded default branding.
- **SVG injection:** SVG files are sanitized to remove `<script>` tags, event handlers, and external references before storage. Only safe SVG elements are retained.
- **Font family not available:** If the specified font family is not available on the client device, the CSS `font-family` value includes fallback fonts (e.g., `"Inter, Helvetica Neue, sans-serif"`).

---

## Dependencies

**Prerequisite Stories:**
- STORY-003: Tenant Management & Multi-Tenancy — tenant records and tenant context infrastructure must exist

**Blocked Stories:**
- STORY-069: Tenant Data Isolation Verification — branding is part of the tenant configuration that must be isolated
- Mobile app white-label stories — branding API is consumed by the mobile app for theming

**External Dependencies:**
- Redis instance for branding configuration caching
- Encrypted file storage for brand assets (local filesystem in dev, object storage in production)
- Grafana/monitoring for cache hit rate metrics

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — branding CRUD, validation, caching, default fallback
- [ ] Integration tests passing — full branding update flow with Redis cache and PostgreSQL
- [ ] Blazor admin portal renders correctly with tenant-specific branding (visual verification)
- [ ] Mobile app branding endpoint returns correct configuration with ETag support
- [ ] Asset upload and retrieval tested — logo and splash screen round-trip verified
- [ ] Default branding verified — unconfigured tenant receives default values
- [ ] Cache invalidation verified — branding update clears Redis cache, next request returns updated branding
- [ ] Branding history and revert tested — admin can view history and revert to a previous version
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
