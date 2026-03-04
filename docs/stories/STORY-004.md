# STORY-004: gRPC Proto Definitions & Shared Contracts

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want all gRPC service contracts defined in .proto files,
So that all services have type-safe communication contracts.

---

## Description

### Background
UniBank's microservice architecture relies on gRPC for all inter-service communication. Defining the protobuf contracts upfront allows teams to work in parallel -- frontend and backend developers can generate client/server stubs from the same `.proto` files, ensuring type safety and API compatibility.

All proto files are centralized in the `UniBank.Protos` project, versioned under the `unibank.v1` package namespace. This project compiles the `.proto` files into C# classes that are referenced by all other projects. The contracts cover all 10 services in the UniBank platform.

### Scope

**In scope:**
- Proto files for all 10 gRPC services
- Request and response message types for all RPC methods
- Common/shared message types (Money, Pagination, Timestamp wrappers)
- Server streaming RPCs for transaction history and report exports
- Proto file compilation configuration in `.csproj`
- Versioning strategy (`unibank.v1.*`)
- Enum definitions for statuses, types, and categories
- Field validation annotations via `buf` or comments

**Out of scope:**
- gRPC service implementations (handled in individual service stories)
- gRPC-Web or REST transcoding
- Proto file linting CI checks (STORY-006)
- Client SDK generation for mobile apps

### User Flow
1. Developer adds or modifies a `.proto` file in `UniBank.Protos/Protos/`
2. Developer runs `dotnet build` on the Protos project
3. `Grpc.Tools` generates C# classes for messages and service stubs
4. Other projects reference `UniBank.Protos` and use the generated types
5. Server projects implement the generated service base classes
6. Client projects use the generated client stubs

---

## Acceptance Criteria

- [ ] Proto files exist for all 10 services: Account, Payment, Transfer, Agent, BillPay, Merchant, Terminal, Admin, Reporting, HSM
- [ ] All proto files compile without errors via `dotnet build`
- [ ] Each service is namespaced as `unibank.v1.{service_name}` (e.g., `unibank.v1.accounts`)
- [ ] Request and response messages are defined for all RPC methods listed in the technical specification
- [ ] Streaming RPCs are defined for: `GetTransactions`, `SearchTransactions`, `GetMerchantTransactions`, `ExportReport`
- [ ] Common message types are defined in `common.proto`: `Money`, `PaginationRequest`, `PaginationResponse`, `DateRange`, `StatusResponse`
- [ ] Enum types are defined for all status fields (account status, transaction type/status, KYC status, etc.)
- [ ] Proto files include descriptive comments for all services, methods, messages, and fields
- [ ] `UniBank.Protos.csproj` is configured to generate both server and client stubs
- [ ] All other projects can reference `UniBank.Protos` and use generated types

---

## Technical Notes

### Components

**Project:** `UniBank.Protos`

**File Structure:**
```
UniBank.Protos/
  UniBank.Protos.csproj
  Protos/
    common.proto
    account_service.proto
    payment_service.proto
    transfer_service.proto
    agent_service.proto
    billpay_service.proto
    merchant_service.proto
    terminal_service.proto
    admin_service.proto
    reporting_service.proto
    hsm_service.proto
```

**UniBank.Protos.csproj Configuration:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Grpc.Tools" PrivateAssets="All" />
    <PackageReference Include="Google.Protobuf" />
    <PackageReference Include="Grpc.Net.Client" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="Protos/*.proto" GrpcServices="Both" />
  </ItemGroup>
</Project>
```

### Proto File Definitions

**common.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.common;
option csharp_namespace = "UniBank.Protos.Common";

import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";

message Money {
  string amount = 1;         // Decimal as string to avoid floating point issues
  string currency = 2;       // ISO 4217 currency code (e.g., "ZAR")
}

message PaginationRequest {
  int32 page = 1;            // 1-based page number
  int32 page_size = 2;       // Items per page (max 100)
  string sort_by = 3;        // Field to sort by
  bool descending = 4;       // Sort direction
}

message PaginationResponse {
  int32 total_count = 1;
  int32 page = 2;
  int32 page_size = 3;
  int32 total_pages = 4;
  bool has_next = 5;
  bool has_previous = 6;
}

message DateRange {
  google.protobuf.Timestamp from = 1;
  google.protobuf.Timestamp to = 2;
}

message StatusResponse {
  bool success = 1;
  string message = 2;
  string error_code = 3;
}
```

**account_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.accounts;
option csharp_namespace = "UniBank.Protos.Accounts";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";

service AccountService {
  // Registration flow
  rpc Register(RegisterRequest) returns (RegisterResponse);
  rpc VerifyOTP(VerifyOTPRequest) returns (VerifyOTPResponse);
  rpc CreatePIN(CreatePINRequest) returns (CreatePINResponse);

  // Profile management
  rpc GetProfile(GetProfileRequest) returns (ProfileResponse);
  rpc UpdateProfile(UpdateProfileRequest) returns (ProfileResponse);

  // Balance and transactions
  rpc GetBalance(GetBalanceRequest) returns (BalanceResponse);
  rpc GetTransactions(GetTransactionsRequest) returns (stream TransactionResponse);
}

// --- Registration Messages ---
message RegisterRequest {
  string phone_number = 1;       // E.164 format: +27XXXXXXXXX
  string device_id = 2;          // Unique device identifier
  string tenant_id = 3;          // Tenant identifier
}

message RegisterResponse {
  bool success = 1;
  string message = 2;
  string registration_id = 3;    // Used to correlate OTP verification
  int32 otp_length = 4;          // Length of OTP sent (6)
  int32 otp_ttl_seconds = 5;     // TTL for OTP (300)
}

message VerifyOTPRequest {
  string registration_id = 1;
  string otp = 2;
  string phone_number = 3;
}

message VerifyOTPResponse {
  bool success = 1;
  string message = 2;
  string account_id = 3;
  string temporary_token = 4;    // JWT for PIN creation step
}

message CreatePINRequest {
  string account_id = 1;
  string pin = 2;                // 4-6 digit PIN
  string pin_confirmation = 3;   // Must match pin
}

message CreatePINResponse {
  bool success = 1;
  string message = 2;
  string auth_token = 3;         // Full JWT after PIN creation
  string refresh_token = 4;
}

// --- Profile Messages ---
message GetProfileRequest {
  string account_id = 1;
}

message UpdateProfileRequest {
  string account_id = 1;
  google.protobuf.StringValue first_name = 2;
  google.protobuf.StringValue last_name = 3;
  google.protobuf.StringValue email = 4;
  google.protobuf.StringValue date_of_birth = 5;  // ISO 8601
  google.protobuf.StringValue national_id = 6;
}

message ProfileResponse {
  string account_id = 1;
  string phone_number = 2;
  string first_name = 3;
  string last_name = 4;
  string email = 5;
  string date_of_birth = 6;
  string national_id = 7;
  AccountStatus status = 8;
  int32 kyc_level = 9;
  google.protobuf.Timestamp created_at = 10;
  google.protobuf.Timestamp last_login_at = 11;
}

// --- Balance Messages ---
message GetBalanceRequest {
  string account_id = 1;
}

message BalanceResponse {
  string account_id = 1;
  unibank.v1.common.Money balance = 2;
  unibank.v1.common.Money available_balance = 3;
  unibank.v1.common.Money daily_limit = 4;
  unibank.v1.common.Money daily_used = 5;
}

// --- Transaction Messages ---
message GetTransactionsRequest {
  string account_id = 1;
  unibank.v1.common.DateRange date_range = 2;
  TransactionType type_filter = 3;
  TransactionStatus status_filter = 4;
  unibank.v1.common.PaginationRequest pagination = 5;
}

message TransactionResponse {
  string transaction_id = 1;
  TransactionType type = 2;
  unibank.v1.common.Money amount = 3;
  unibank.v1.common.Money fee = 4;
  TransactionStatus status = 5;
  string reference = 6;
  string description = 7;
  string counterparty_name = 8;
  string counterparty_phone = 9;
  unibank.v1.common.Money balance_after = 10;
  google.protobuf.Timestamp created_at = 11;
  google.protobuf.Timestamp completed_at = 12;
}

// --- Enums ---
enum AccountStatus {
  ACCOUNT_STATUS_UNSPECIFIED = 0;
  ACCOUNT_STATUS_PENDING_KYC = 1;
  ACCOUNT_STATUS_ACTIVE = 2;
  ACCOUNT_STATUS_SUSPENDED = 3;
  ACCOUNT_STATUS_CLOSED = 4;
  ACCOUNT_STATUS_FROZEN = 5;
}

enum TransactionType {
  TRANSACTION_TYPE_UNSPECIFIED = 0;
  TRANSACTION_TYPE_CASH_IN = 1;
  TRANSACTION_TYPE_CASH_OUT = 2;
  TRANSACTION_TYPE_P2P_SEND = 3;
  TRANSACTION_TYPE_P2P_RECEIVE = 4;
  TRANSACTION_TYPE_PAYMENT_NFC = 5;
  TRANSACTION_TYPE_PAYMENT_QR = 6;
  TRANSACTION_TYPE_BILL_PAYMENT = 7;
  TRANSACTION_TYPE_TRANSFER_DOMESTIC = 8;
  TRANSACTION_TYPE_TRANSFER_CROSS_BORDER = 9;
  TRANSACTION_TYPE_FEE = 10;
  TRANSACTION_TYPE_REVERSAL = 11;
  TRANSACTION_TYPE_SETTLEMENT = 12;
}

enum TransactionStatus {
  TRANSACTION_STATUS_UNSPECIFIED = 0;
  TRANSACTION_STATUS_PENDING = 1;
  TRANSACTION_STATUS_PROCESSING = 2;
  TRANSACTION_STATUS_COMPLETED = 3;
  TRANSACTION_STATUS_FAILED = 4;
  TRANSACTION_STATUS_REVERSED = 5;
}
```

**payment_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.payments;
option csharp_namespace = "UniBank.Protos.Payments";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service PaymentService {
  rpc InitiateNFCPayment(NFCPaymentRequest) returns (PaymentResponse);
  rpc GenerateQRCode(QRCodeRequest) returns (QRCodeResponse);
  rpc ProcessQRPayment(QRPaymentRequest) returns (PaymentResponse);
}

message NFCPaymentRequest {
  string account_id = 1;
  string merchant_id = 2;
  string terminal_id = 3;
  unibank.v1.common.Money amount = 4;
  string pin = 5;                // Encrypted PIN block
  string nfc_data = 6;           // NFC tag data
}

message QRCodeRequest {
  string merchant_id = 1;
  string terminal_id = 2;
  unibank.v1.common.Money amount = 3;
  string description = 4;
  int32 ttl_seconds = 5;         // QR code validity period
}

message QRCodeResponse {
  bool success = 1;
  string qr_code_data = 2;       // Encoded QR payload
  string payment_reference = 3;
  google.protobuf.Timestamp expires_at = 4;
}

message QRPaymentRequest {
  string account_id = 1;
  string qr_code_data = 2;       // Scanned QR payload
  string pin = 3;                // Encrypted PIN block
}

message PaymentResponse {
  bool success = 1;
  string message = 2;
  string transaction_id = 3;
  string reference = 4;
  unibank.v1.common.Money amount = 5;
  unibank.v1.common.Money fee = 6;
  unibank.v1.common.Money new_balance = 7;
  google.protobuf.Timestamp completed_at = 8;
}
```

**transfer_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.transfers;
option csharp_namespace = "UniBank.Protos.Transfers";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service TransferService {
  rpc SendP2P(P2PTransferRequest) returns (TransferResponse);
  rpc SendCrossBorder(CrossBorderTransferRequest) returns (TransferResponse);
}

message P2PTransferRequest {
  string sender_account_id = 1;
  string recipient_phone = 2;    // E.164 format
  unibank.v1.common.Money amount = 3;
  string description = 4;
  string pin = 5;                // Encrypted PIN block
}

message CrossBorderTransferRequest {
  string sender_account_id = 1;
  string recipient_phone = 2;
  string recipient_name = 3;
  string recipient_country = 4;  // ISO 3166-1 alpha-3
  unibank.v1.common.Money send_amount = 5;
  string receive_currency = 6;   // Destination currency
  string corridor_id = 7;        // Remittance corridor
  string pin = 8;
}

message TransferResponse {
  bool success = 1;
  string message = 2;
  string transaction_id = 3;
  string reference = 4;
  unibank.v1.common.Money amount_sent = 5;
  unibank.v1.common.Money amount_received = 6;
  unibank.v1.common.Money fee = 7;
  string exchange_rate = 8;
  unibank.v1.common.Money new_balance = 9;
  TransferStatus status = 10;
  google.protobuf.Timestamp estimated_delivery = 11;
}

enum TransferStatus {
  TRANSFER_STATUS_UNSPECIFIED = 0;
  TRANSFER_STATUS_PENDING = 1;
  TRANSFER_STATUS_PROCESSING = 2;
  TRANSFER_STATUS_COMPLETED = 3;
  TRANSFER_STATUS_FAILED = 4;
}
```

**agent_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.agents;
option csharp_namespace = "UniBank.Protos.Agents";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service AgentService {
  rpc CashIn(CashInRequest) returns (CashOperationResponse);
  rpc CashOut(CashOutRequest) returns (CashOperationResponse);
  rpc GetFloatBalance(FloatBalanceRequest) returns (FloatBalanceResponse);
  rpc GetCommissionReport(CommissionReportRequest) returns (CommissionReportResponse);
}

message CashInRequest {
  string agent_id = 1;
  string customer_phone = 2;
  unibank.v1.common.Money amount = 3;
  string agent_pin = 4;
}

message CashOutRequest {
  string agent_id = 1;
  string customer_account_id = 2;
  unibank.v1.common.Money amount = 3;
  string customer_pin = 4;
  string agent_pin = 5;
}

message CashOperationResponse {
  bool success = 1;
  string message = 2;
  string transaction_id = 3;
  string reference = 4;
  unibank.v1.common.Money amount = 5;
  unibank.v1.common.Money commission = 6;
  unibank.v1.common.Money new_float_balance = 7;
  google.protobuf.Timestamp completed_at = 8;
}

message FloatBalanceRequest {
  string agent_id = 1;
}

message FloatBalanceResponse {
  string agent_id = 1;
  unibank.v1.common.Money float_balance = 2;
  unibank.v1.common.Money float_limit = 3;
  unibank.v1.common.Money available_float = 4;
}

message CommissionReportRequest {
  string agent_id = 1;
  unibank.v1.common.DateRange date_range = 2;
}

message CommissionReportResponse {
  string agent_id = 1;
  unibank.v1.common.Money total_commission = 2;
  int32 total_transactions = 3;
  repeated CommissionLineItem items = 4;
}

message CommissionLineItem {
  string transaction_type = 1;
  int32 count = 2;
  unibank.v1.common.Money total_amount = 3;
  unibank.v1.common.Money total_commission = 4;
}
```

**billpay_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.billpay;
option csharp_namespace = "UniBank.Protos.BillPay";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service BillPayService {
  rpc ListProviders(ListProvidersRequest) returns (ListProvidersResponse);
  rpc PayBill(PayBillRequest) returns (PayBillResponse);
  rpc SaveBiller(SaveBillerRequest) returns (unibank.v1.common.StatusResponse);
  rpc GetSavedBillers(GetSavedBillersRequest) returns (GetSavedBillersResponse);
}

message ListProvidersRequest {
  string category = 1;           // Filter by category (optional)
  string country_code = 2;       // Filter by country (optional)
}

message ListProvidersResponse {
  repeated BillProvider providers = 1;
}

message BillProvider {
  string provider_id = 1;
  string name = 2;
  string code = 3;
  string category = 4;
  bool requires_meter_number = 5;
  bool requires_account_number = 6;
  unibank.v1.common.Money min_amount = 7;
  unibank.v1.common.Money max_amount = 8;
}

message PayBillRequest {
  string account_id = 1;
  string provider_id = 2;
  string billing_reference = 3;  // Meter number, account number, etc.
  unibank.v1.common.Money amount = 4;
  string pin = 5;
}

message PayBillResponse {
  bool success = 1;
  string message = 2;
  string transaction_id = 3;
  string reference = 4;
  string token = 5;              // Electricity token, voucher code, etc.
  unibank.v1.common.Money amount = 6;
  unibank.v1.common.Money fee = 7;
  unibank.v1.common.Money new_balance = 8;
  google.protobuf.Timestamp completed_at = 9;
}

message SaveBillerRequest {
  string account_id = 1;
  string provider_id = 2;
  string billing_reference = 3;
  string nickname = 4;           // User-friendly name
}

message GetSavedBillersRequest {
  string account_id = 1;
}

message GetSavedBillersResponse {
  repeated SavedBiller billers = 1;
}

message SavedBiller {
  string id = 1;
  string provider_id = 2;
  string provider_name = 3;
  string billing_reference = 4;
  string nickname = 5;
  google.protobuf.Timestamp last_paid_at = 6;
}
```

**merchant_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.merchants;
option csharp_namespace = "UniBank.Protos.Merchants";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service MerchantService {
  rpc Register(MerchantRegisterRequest) returns (MerchantRegisterResponse);
  rpc GetProfile(MerchantProfileRequest) returns (MerchantProfileResponse);
  rpc GetTransactions(MerchantTransactionsRequest) returns (stream MerchantTransactionResponse);
  rpc GetSettlements(MerchantSettlementsRequest) returns (MerchantSettlementsResponse);
}

message MerchantRegisterRequest {
  string account_id = 1;
  string business_name = 2;
  string business_type = 3;
  string registration_number = 4;
  string tax_id = 5;
  string category_code = 6;     // MCC code
  MerchantAddress address = 7;
}

message MerchantAddress {
  string line1 = 1;
  string line2 = 2;
  string city = 3;
  string province = 4;
  string postal_code = 5;
  string country_code = 6;
}

message MerchantRegisterResponse {
  bool success = 1;
  string message = 2;
  string merchant_id = 3;
  MerchantStatus status = 4;
}

message MerchantProfileRequest {
  string merchant_id = 1;
}

message MerchantProfileResponse {
  string merchant_id = 1;
  string business_name = 2;
  string business_type = 3;
  string category_code = 4;
  MerchantAddress address = 5;
  MerchantStatus status = 6;
  string commission_rate = 7;
  string settlement_frequency = 8;
  google.protobuf.Timestamp created_at = 9;
}

message MerchantTransactionsRequest {
  string merchant_id = 1;
  unibank.v1.common.DateRange date_range = 2;
  unibank.v1.common.PaginationRequest pagination = 3;
}

message MerchantTransactionResponse {
  string transaction_id = 1;
  unibank.v1.common.Money amount = 2;
  unibank.v1.common.Money fee = 3;
  string reference = 4;
  string payment_method = 5;     // NFC, QR
  string terminal_id = 6;
  google.protobuf.Timestamp created_at = 7;
}

message MerchantSettlementsRequest {
  string merchant_id = 1;
  unibank.v1.common.DateRange date_range = 2;
}

message MerchantSettlementsResponse {
  repeated Settlement settlements = 1;
}

message Settlement {
  string settlement_id = 1;
  unibank.v1.common.Money amount = 2;
  int32 transaction_count = 3;
  string status = 4;
  google.protobuf.Timestamp settlement_date = 5;
  google.protobuf.Timestamp paid_at = 6;
}

enum MerchantStatus {
  MERCHANT_STATUS_UNSPECIFIED = 0;
  MERCHANT_STATUS_PENDING = 1;
  MERCHANT_STATUS_ACTIVE = 2;
  MERCHANT_STATUS_SUSPENDED = 3;
  MERCHANT_STATUS_CLOSED = 4;
}
```

**terminal_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.terminals;
option csharp_namespace = "UniBank.Protos.Terminals";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service TerminalService {
  rpc RegisterTerminal(RegisterTerminalRequest) returns (RegisterTerminalResponse);
  rpc GetTerminalStatus(TerminalStatusRequest) returns (TerminalStatusResponse);
  rpc PushUpdate(PushUpdateRequest) returns (unibank.v1.common.StatusResponse);
}

message RegisterTerminalRequest {
  string merchant_id = 1;
  string serial_number = 2;
  string model = 3;
  string firmware_version = 4;
  string location_description = 5;
}

message RegisterTerminalResponse {
  bool success = 1;
  string message = 2;
  string terminal_id = 3;
  string mqtt_topic_prefix = 4;  // Topic prefix for MQTT communication
  string initial_config = 5;     // JSON configuration for terminal
}

message TerminalStatusRequest {
  string terminal_id = 1;
}

message TerminalStatusResponse {
  string terminal_id = 1;
  string serial_number = 2;
  string model = 3;
  string firmware_version = 4;
  TerminalStatus status = 5;
  google.protobuf.Timestamp last_heartbeat = 6;
  google.protobuf.Timestamp last_key_injection = 7;
  string ip_address = 8;
}

message PushUpdateRequest {
  string terminal_id = 1;
  UpdateType update_type = 2;
  bytes payload = 3;            // Firmware binary or config JSON
  string version = 4;
}

enum TerminalStatus {
  TERMINAL_STATUS_UNSPECIFIED = 0;
  TERMINAL_STATUS_INACTIVE = 1;
  TERMINAL_STATUS_ACTIVE = 2;
  TERMINAL_STATUS_OFFLINE = 3;
  TERMINAL_STATUS_DECOMMISSIONED = 4;
}

enum UpdateType {
  UPDATE_TYPE_UNSPECIFIED = 0;
  UPDATE_TYPE_FIRMWARE = 1;
  UPDATE_TYPE_CONFIG = 2;
  UPDATE_TYPE_KEYS = 3;
}
```

**admin_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.admin;
option csharp_namespace = "UniBank.Protos.Admin";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service AdminService {
  rpc SearchCustomers(SearchCustomersRequest) returns (SearchCustomersResponse);
  rpc ManageAccount(ManageAccountRequest) returns (unibank.v1.common.StatusResponse);
  rpc ManageMerchant(ManageMerchantRequest) returns (unibank.v1.common.StatusResponse);
  rpc SearchTransactions(SearchTransactionsRequest) returns (stream AdminTransactionResponse);
  rpc ReviewKYC(ReviewKYCRequest) returns (unibank.v1.common.StatusResponse);
  rpc UpdateSystemConfig(UpdateSystemConfigRequest) returns (unibank.v1.common.StatusResponse);
}

message SearchCustomersRequest {
  string query = 1;              // Phone, name, national ID
  string status_filter = 2;
  unibank.v1.common.PaginationRequest pagination = 3;
}

message SearchCustomersResponse {
  repeated CustomerSummary customers = 1;
  unibank.v1.common.PaginationResponse pagination = 2;
}

message CustomerSummary {
  string account_id = 1;
  string phone_number = 2;
  string full_name = 3;
  string status = 4;
  int32 kyc_level = 5;
  unibank.v1.common.Money balance = 6;
  google.protobuf.Timestamp created_at = 7;
  google.protobuf.Timestamp last_login_at = 8;
}

message ManageAccountRequest {
  string account_id = 1;
  AccountAction action = 2;
  string reason = 3;
  string admin_id = 4;
}

enum AccountAction {
  ACCOUNT_ACTION_UNSPECIFIED = 0;
  ACCOUNT_ACTION_SUSPEND = 1;
  ACCOUNT_ACTION_ACTIVATE = 2;
  ACCOUNT_ACTION_CLOSE = 3;
  ACCOUNT_ACTION_FREEZE = 4;
  ACCOUNT_ACTION_UNFREEZE = 5;
  ACCOUNT_ACTION_RESET_PIN = 6;
}

message ManageMerchantRequest {
  string merchant_id = 1;
  MerchantAction action = 2;
  string reason = 3;
  string admin_id = 4;
}

enum MerchantAction {
  MERCHANT_ACTION_UNSPECIFIED = 0;
  MERCHANT_ACTION_APPROVE = 1;
  MERCHANT_ACTION_SUSPEND = 2;
  MERCHANT_ACTION_ACTIVATE = 3;
  MERCHANT_ACTION_CLOSE = 4;
}

message SearchTransactionsRequest {
  string account_id = 1;
  string merchant_id = 2;
  string reference = 3;
  string type_filter = 4;
  string status_filter = 5;
  unibank.v1.common.DateRange date_range = 6;
  unibank.v1.common.Money min_amount = 7;
  unibank.v1.common.Money max_amount = 8;
  unibank.v1.common.PaginationRequest pagination = 9;
}

message AdminTransactionResponse {
  string transaction_id = 1;
  string account_id = 2;
  string account_phone = 3;
  string type = 4;
  unibank.v1.common.Money amount = 5;
  unibank.v1.common.Money fee = 6;
  string status = 7;
  string reference = 8;
  string counterparty_info = 9;
  google.protobuf.Timestamp created_at = 10;
}

message ReviewKYCRequest {
  string document_id = 1;
  KYCDecision decision = 2;
  string notes = 3;
  string admin_id = 4;
}

enum KYCDecision {
  KYC_DECISION_UNSPECIFIED = 0;
  KYC_DECISION_APPROVE = 1;
  KYC_DECISION_REJECT = 2;
  KYC_DECISION_REQUEST_RESUBMIT = 3;
}

message UpdateSystemConfigRequest {
  string key = 1;
  string value_json = 2;
  string tenant_id = 3;         // Empty for global config
  string admin_id = 4;
}
```

**reporting_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.reporting;
option csharp_namespace = "UniBank.Protos.Reporting";

import "Protos/common.proto";
import "google/protobuf/timestamp.proto";

service ReportingService {
  rpc GetDashboard(DashboardRequest) returns (DashboardResponse);
  rpc GetUserGrowthReport(UserGrowthRequest) returns (UserGrowthResponse);
  rpc GetMerchantReport(MerchantReportRequest) returns (MerchantReportResponse);
  rpc GetRevenueReport(RevenueReportRequest) returns (RevenueReportResponse);
  rpc GetReconReport(ReconReportRequest) returns (ReconReportResponse);
  rpc ExportReport(ExportReportRequest) returns (stream ExportChunk);
}

message DashboardRequest {
  unibank.v1.common.DateRange date_range = 1;
}

message DashboardResponse {
  int64 total_users = 1;
  int64 active_users = 2;
  int64 total_transactions = 3;
  unibank.v1.common.Money total_volume = 4;
  unibank.v1.common.Money total_revenue = 5;
  int32 active_merchants = 6;
  int32 active_agents = 7;
  int32 active_terminals = 8;
  repeated DailyMetric daily_metrics = 9;
}

message DailyMetric {
  string date = 1;               // YYYY-MM-DD
  int64 transactions = 2;
  string volume = 3;
  int64 new_users = 4;
}

message UserGrowthRequest {
  unibank.v1.common.DateRange date_range = 1;
  string granularity = 2;        // daily, weekly, monthly
}

message UserGrowthResponse {
  repeated GrowthDataPoint data_points = 1;
  int64 total_registered = 2;
  int64 total_active = 3;
  string growth_rate = 4;        // Percentage
}

message GrowthDataPoint {
  string period = 1;
  int64 new_registrations = 2;
  int64 active_users = 3;
  int64 churned_users = 4;
}

message MerchantReportRequest {
  unibank.v1.common.DateRange date_range = 1;
  string merchant_id = 2;        // Optional: specific merchant
}

message MerchantReportResponse {
  repeated MerchantMetric merchants = 1;
  unibank.v1.common.Money total_volume = 2;
  int32 total_transactions = 3;
}

message MerchantMetric {
  string merchant_id = 1;
  string business_name = 2;
  int32 transaction_count = 3;
  unibank.v1.common.Money volume = 4;
  unibank.v1.common.Money commission = 5;
}

message RevenueReportRequest {
  unibank.v1.common.DateRange date_range = 1;
  string granularity = 2;
}

message RevenueReportResponse {
  repeated RevenueDataPoint data_points = 1;
  unibank.v1.common.Money total_revenue = 2;
  repeated RevenueByType revenue_by_type = 3;
}

message RevenueDataPoint {
  string period = 1;
  unibank.v1.common.Money revenue = 2;
  int32 transaction_count = 3;
}

message RevenueByType {
  string transaction_type = 1;
  unibank.v1.common.Money revenue = 2;
  int32 count = 3;
  string percentage = 4;
}

message ReconReportRequest {
  string batch_date = 1;         // YYYY-MM-DD
  string partner_code = 2;
}

message ReconReportResponse {
  string batch_date = 1;
  string partner_code = 2;
  int32 total_transactions = 3;
  unibank.v1.common.Money total_amount = 4;
  int32 matched_count = 5;
  int32 unmatched_count = 6;
  string status = 7;
  repeated ReconDiscrepancy discrepancies = 8;
}

message ReconDiscrepancy {
  string transaction_reference = 1;
  unibank.v1.common.Money our_amount = 2;
  unibank.v1.common.Money partner_amount = 3;
  string discrepancy_type = 4;   // missing, amount_mismatch, status_mismatch
}

message ExportReportRequest {
  string report_type = 1;        // dashboard, user_growth, merchant, revenue, recon
  unibank.v1.common.DateRange date_range = 2;
  string format = 3;             // csv, xlsx, pdf
}

message ExportChunk {
  bytes data = 1;
  int32 chunk_number = 2;
  int32 total_chunks = 3;
  string filename = 4;
  string content_type = 5;
}
```

**hsm_service.proto:**
```protobuf
syntax = "proto3";
package unibank.v1.hsm;
option csharp_namespace = "UniBank.Protos.HSM";

service HSMService {
  rpc GenerateKey(GenerateKeyRequest) returns (GenerateKeyResponse);
  rpc DeriveSessionKey(DeriveSessionKeyRequest) returns (DeriveSessionKeyResponse);
  rpc EncryptPINBlock(EncryptPINBlockRequest) returns (PINBlockResponse);
  rpc DecryptPINBlock(DecryptPINBlockRequest) returns (PINBlockResponse);
  rpc GenerateMAC(GenerateMACRequest) returns (MACResponse);
  rpc GenerateToken(GenerateTokenRequest) returns (GenerateTokenResponse);
}

message GenerateKeyRequest {
  KeyType key_type = 1;
  int32 key_length = 2;          // 128, 192, or 256 bits
  string key_label = 3;
}

message GenerateKeyResponse {
  bool success = 1;
  string key_id = 2;
  string key_check_value = 3;    // KCV for verification
  string encrypted_key = 4;      // Key encrypted under master key
}

message DeriveSessionKeyRequest {
  string master_key_id = 1;
  string derivation_data = 2;    // Terminal ID + sequence number
  KeyType derived_key_type = 3;
}

message DeriveSessionKeyResponse {
  bool success = 1;
  string session_key_id = 2;
  string key_check_value = 3;
}

message EncryptPINBlockRequest {
  string pin = 1;                // Clear PIN (transmitted securely)
  string account_number = 2;
  string encryption_key_id = 3;
  PINBlockFormat format = 4;
}

message DecryptPINBlockRequest {
  bytes encrypted_pin_block = 1;
  string account_number = 2;
  string decryption_key_id = 3;
  PINBlockFormat format = 4;
}

message PINBlockResponse {
  bool success = 1;
  bytes pin_block = 2;
  string clear_pin = 3;         // Only for decrypt (handle with extreme care)
}

message GenerateMACRequest {
  bytes data = 1;
  string mac_key_id = 2;
  MACAlgorithm algorithm = 3;
}

message MACResponse {
  bool success = 1;
  bytes mac = 2;
}

message GenerateTokenRequest {
  string pan = 1;                // Primary Account Number to tokenize
  string token_type = 2;        // payment, display
}

message GenerateTokenResponse {
  bool success = 1;
  string token = 2;
  string token_reference = 3;
}

enum KeyType {
  KEY_TYPE_UNSPECIFIED = 0;
  KEY_TYPE_MASTER = 1;           // Zone Master Key
  KEY_TYPE_PIN_ENCRYPTION = 2;   // PIN Encryption Key
  KEY_TYPE_MAC = 3;              // Message Authentication Code Key
  KEY_TYPE_DATA_ENCRYPTION = 4;  // Data Encryption Key
  KEY_TYPE_SESSION = 5;          // Session Key
}

enum PINBlockFormat {
  PIN_BLOCK_FORMAT_UNSPECIFIED = 0;
  PIN_BLOCK_FORMAT_ISO_0 = 1;    // ISO 9564 Format 0
  PIN_BLOCK_FORMAT_ISO_3 = 2;    // ISO 9564 Format 3
  PIN_BLOCK_FORMAT_ISO_4 = 3;    // ISO 9564 Format 4 (AES)
}

enum MACAlgorithm {
  MAC_ALGORITHM_UNSPECIFIED = 0;
  MAC_ALGORITHM_HMAC_SHA256 = 1;
  MAC_ALGORITHM_CMAC_AES = 2;
  MAC_ALGORITHM_CBC_MAC = 3;
}
```

### API / gRPC Endpoints
All endpoints are defined in the proto files above. This story defines the contracts; implementations are in separate stories.

### Database Changes
None. Proto files do not affect the database.

### Security Considerations
- PIN fields in messages should be encrypted in transit (gRPC TLS handles this)
- HSM service proto should only be accessible from internal services, never from client-facing gateway
- Clear PIN in `PINBlockResponse` must be handled with extreme care and never logged
- Consider adding `google.api.http` annotations if REST transcoding is needed later

### Edge Cases
- Proto file backward compatibility: adding fields is safe (additive changes), removing or renumbering fields is breaking
- Enum zero values must always be `UNSPECIFIED` per protobuf best practices
- Streaming RPCs need careful handling of connection drops and resumption
- Large `ExportChunk` streams should use reasonable chunk sizes (64KB-256KB)

---

## Dependencies

**Prerequisite Stories:**
- STORY-001: Solution Scaffolding & Project Structure (Protos project must exist)

**Blocked Stories:**
- STORY-005: API Gateway with gRPC Interceptors (needs compiled proto contracts)
- STORY-009: User Self-Registration (needs AccountService proto)
- STORY-010: Create Account PIN (needs AccountService proto)
- All service implementation stories

**External Dependencies:**
- `Grpc.Tools` NuGet package for proto compilation
- `Google.Protobuf` NuGet package for runtime support

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) -- proto compilation verification
- [ ] Integration tests passing -- N/A for proto definitions
- [ ] Code reviewed and approved
- [ ] Documentation updated (proto file comments serve as API documentation)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
