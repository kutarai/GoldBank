# EPIC-020: Asset Custody & Trust Management

**Priority:** Must Have
**Estimated Points:** 55
**Sprints:** 22–24
**Dependencies:** EPIC-017 (AI Vision — receipt OCR), EPIC-018 (Mobile UI)

---

## Business Context

GoldBank customers may hold physical assets — such as gold coins, precious metals, or other valuables — stored at designated safe deposit facilities. The bank provides a digital registry of these assets, tracks their custody status, and calculates the customer's net worth from periodic valuations. This service differentiates GoldBank from mobile-money-only competitors and opens revenue opportunities through custody fees, valuation services, and asset-backed lending.

## User Personas

- **Customer**: Deposits physical assets at a safe deposit house, uploads receipt, views asset portfolio and net worth
- **Bank Admin**: Manages deposit houses, reviews asset registrations, triggers valuations
- **Deposit House**: External partner system that holds assets in trust and issues receipts
- **Valuer**: Qualified professional who periodically values assets (trusted by the bank)

---

## Functional Requirements

### FR-001: Asset Registration via Receipt Upload
- Customer takes a photo of the safe deposit receipt
- AI (Qwen3-VL) extracts: deposit house name, receipt number, date, asset description, quantity, depositor name
- Customer confirms/edits extracted details (including quantity and asset type)
- System validates receipt number uniqueness (no duplicate registrations)
- Asset registered with status `pending_verification`

### FR-002: Asset Types & Valuation
- Supported asset types: Gold Coins, Gold Bars, Silver, Platinum, Precious Stones, Other
- Gold/Silver/Platinum: daily spot price fetched from a price feed (configurable API or manual admin entry)
- Precious Stones / Other: valued only via periodic manual valuation by qualified valuer
- Each asset stores: type, quantity, unit, weight (grams/oz), purity (for metals), last valuation amount, last valuation date

### FR-003: Daily Value Display
- For precious metals: current value = quantity × weight × purity × daily spot price
- For other assets: current value = last manual valuation amount
- Customer sees total portfolio value (sum of all assets) as "Net Worth from Assets"
- Value displayed in both ZWG and USD (using current exchange rate)

### FR-004: Deposit House Management (Admin)
- Admin registers trusted deposit houses: name, address, contact, license number, API endpoint (if available)
- Admin can activate/deactivate deposit houses
- Each deposit house has a trust status: Verified, Probationary, Suspended

### FR-005: Certificate Verification
- System periodically checks with deposit house systems to verify certificate validity
- If deposit house has an API: automated batch verification (configurable schedule, e.g., daily at 02:00)
- If no API: manual verification queue for admin to confirm with deposit house via other channels
- Verification statuses: Verified, Pending, Failed, Expired
- Customer notified if certificate verification fails

### FR-006: Periodic Valuation by Qualified Valuer
- Admin schedules valuations and assigns qualified valuers
- Valuer submits: asset ID, valuation amount, valuation date, valuation report (PDF/image upload)
- Valuation updates the asset's current value
- Historical valuations retained for audit trail
- Customer notified when a new valuation is recorded

### FR-007: Asset Removal / Release
- Customer requests asset release (withdrawal from deposit house)
- Request goes to admin for approval
- On approval: asset status changes to `released`, removed from portfolio calculation
- Deposit house notified of release authorization

### FR-008: Net Worth Dashboard
- Customer's home screen shows "Asset Portfolio" card with total net worth
- Breakdown by asset type (pie chart or list)
- Historical value chart (line graph over time)
- Link to full asset list

---

## Stories

### Sprint 22: Server Foundation (21 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-136 | Asset domain model + DB schema | 5 | Create Asset, DepositHouse, AssetValuation, DepositReceipt entities with EF Core config and migrations |
| STORY-137 | Asset gRPC service + proto | 5 | Define asset_service.proto with RPCs: RegisterAsset, ListMyAssets, GetAssetDetail, RemoveAsset, ListDepositHouses, GetDailyPrices |
| STORY-138 | Receipt OCR integration | 3 | AI handler that extracts deposit receipt fields via Qwen3-VL (reuses EPIC-017 DocumentOcrService pattern) |
| STORY-139 | Deposit house management (Admin) | 5 | Admin CRUD for deposit houses, trust status management, activation/deactivation |
| STORY-140 | Daily price feed service | 3 | Configurable price feed for gold/silver/platinum spot prices. SystemConfig key for API URL. Fallback to manual admin entry. Background service refreshes daily. |

### Sprint 23: Mobile App + Valuation (18 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-141 | Mobile: Asset list + registration flow | 8 | New "Assets" tab in mobile nav. Asset list screen. Registration flow: capture receipt photo → AI OCR → confirm details → submit. |
| STORY-142 | Mobile: Asset detail + value display | 5 | Asset detail screen with current value (spot price calc for metals, last valuation for others). Dual currency display (ZWG + USD). |
| STORY-143 | Valuation workflow (Admin + Valuer) | 5 | Admin schedules valuation, assigns valuer. Valuer submits valuation amount + report. Updates asset value. Customer notification. |

### Sprint 24: Verification + Net Worth (16 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-144 | Certificate verification service | 5 | Background job checks deposit house APIs for certificate validity. Manual verification queue for houses without API. Notifications on failure. |
| STORY-145 | Asset release workflow | 3 | Customer requests release → admin approves → deposit house notified → asset removed from portfolio |
| STORY-146 | Net worth dashboard (Mobile) | 5 | HomeScreen "Asset Portfolio" card. Full portfolio screen with breakdown by type, value chart, net worth in ZWG + USD. |
| STORY-147 | Bank-client: Asset management screens | 3 | Deposit house management, asset review queue, valuation assignment, certificate verification status in admin portal |

---

## Data Model

```
Asset
├── Id (UUID)
├── AccountId (UUID, FK → accounts)
├── DepositHouseId (UUID, FK → deposit_houses)
├── ReceiptNumber (string, unique per deposit house)
├── AssetType (enum: GoldCoin, GoldBar, Silver, Platinum, PreciousStone, Other)
├── Description (string)
├── Quantity (decimal)
├── Unit (string: "coins", "bars", "grams", "oz", "carats")
├── WeightGrams (decimal, nullable — for metals)
├── Purity (decimal, nullable — e.g., 0.999 for 99.9% gold)
├── ReceiptImagePath (string)
├── ReceiptDate (DateTime)
├── LastValuationAmount (decimal)
├── LastValuationDate (DateTime?)
├── LastVerificationDate (DateTime?)
├── VerificationStatus (enum: Pending, Verified, Failed, Expired)
├── Status (enum: PendingVerification, Active, Released, Suspended)
├── TenantId, CreatedAt, UpdatedAt, DeletedAt

DepositHouse
├── Id (UUID)
├── Name (string)
├── Address (string)
├── City (string)
├── ContactPhone (string)
├── ContactEmail (string)
├── LicenseNumber (string)
├── ApiEndpoint (string, nullable)
├── TrustStatus (enum: Verified, Probationary, Suspended)
├── IsActive (bool)
├── TenantId, CreatedAt, UpdatedAt

AssetValuation
├── Id (UUID)
├── AssetId (UUID, FK → assets)
├── ValuationAmount (decimal)
├── Currency (string)
├── ValuerName (string)
├── ValuerLicense (string)
├── ReportImagePath (string, nullable)
├── Notes (string)
├── CreatedAt

DailyPrice
├── Id (UUID)
├── AssetType (string: "gold", "silver", "platinum")
├── PricePerGramUsd (decimal)
├── PricePerOzUsd (decimal)
├── Source (string: "api", "manual")
├── Date (DateOnly)
├── CreatedAt
```

## AI Integration

Receipt OCR prompt for Qwen3-VL:
```
Extract the following fields from this safe deposit receipt image:
- Deposit house name
- Receipt number
- Date of deposit
- Depositor name
- Asset description
- Quantity
- Any weight or purity information

Return as JSON.
```

## Security Considerations

- Receipt images stored encrypted (reuse DocumentStorageService from KYC module)
- Asset values are sensitive PII — only visible to the owning customer and authorized admin roles
- Certificate verification API calls use mTLS where supported
- Valuation reports stored with integrity hash
- Audit trail for all admin actions on assets

## Price Feed Configuration

SystemConfig keys:
- `asset.price_feed_url`: API endpoint for daily metal prices (e.g., goldapi.io, metals-api.com)
- `asset.price_feed_api_key`: API key (stored encrypted)
- `asset.price_refresh_cron`: Cron schedule for price refresh (default: "0 2 * * *" — daily at 02:00)
- `asset.gold_price_manual_usd`: Manual fallback gold price per oz in USD
- `asset.silver_price_manual_usd`: Manual fallback silver price per oz in USD
- `asset.platinum_price_manual_usd`: Manual fallback platinum price per oz in USD
