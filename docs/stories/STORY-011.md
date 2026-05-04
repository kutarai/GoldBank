# STORY-011: KYC - National ID Document Upload

**Epic:** EPIC-001
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a new user
I want to upload my national ID document
So that my identity can be verified

---

## Description

### Background
GoldBank serves the unbanked population across Southern Africa, where Know Your Customer (KYC) compliance is a regulatory requirement for all financial services. National ID document upload is the first step in the identity verification pipeline. Users must submit a clear image of their government-issued national identification document (e.g., Malawian National ID, Zambian NRC, Mozambican BI) before their account can be activated. This document will later be cross-referenced against a selfie photograph for identity confirmation (STORY-012).

The upload must handle the realities of the target market: users may be on low-bandwidth connections, using entry-level Android devices, and photographing documents in suboptimal lighting conditions. The system must securely store these sensitive documents with AES-256 encryption at rest, complying with data protection regulations such as Malawi's Data Protection Act and similar frameworks across the region.

### Scope
**In scope:**
- Capture national ID image via device camera or upload from gallery
- Validate image format (JPEG, PNG) and enforce maximum file size (10MB)
- Stream the file to the server via gRPC client streaming
- Encrypt the document at rest using AES-256 with HSM-managed keys
- Create a KYC record in the `kyc_documents` table
- Return upload confirmation with document status
- Support for multiple Southern African national ID document types
- Tenant-specific document type configuration

**Out of scope:**
- OCR or automated data extraction from the document image
- Selfie capture and photo matching (STORY-012)
- KYC approval workflow (handled downstream)
- Document re-upload after rejection (future story)
- Multi-document upload in a single request

### User Flow
1. User completes phone registration (STORY-009) and is prompted to begin KYC
2. User selects "Upload National ID" from the KYC screen
3. App presents options: "Take Photo" (camera) or "Choose from Gallery"
4. If camera: app opens camera with document framing guide overlay
5. User captures or selects the ID image
6. App validates locally: file format (JPEG/PNG), file size (<=10MB), minimum resolution (640x480)
7. App displays preview with option to retake or confirm
8. On confirm, app initiates gRPC streaming upload to `KYCService.UploadDocument`
9. Server receives the stream, validates the image server-side
10. Server encrypts the file using AES-256 with a key reference from HSM
11. Server stores the encrypted file on disk at the configured storage path
12. Server creates a record in `kyc_documents` with status `uploaded`
13. Server returns `UploadDocumentResponse` with document ID and status
14. App displays success confirmation and prompts user to proceed to selfie capture

---

## Acceptance Criteria

- [ ] User can capture a national ID image using the device camera
- [ ] User can upload a national ID image from the device gallery
- [ ] System validates image format is JPEG or PNG; rejects other formats with a clear error message
- [ ] System enforces a maximum file size of 10MB; rejects larger files with a clear error message
- [ ] Document is transmitted to the server via gRPC client streaming
- [ ] Document is stored encrypted at rest using AES-256
- [ ] Encryption key is managed via HSM reference (not stored alongside the document)
- [ ] A KYC record is created in the `kyc_documents` table with status `uploaded`
- [ ] Document type is validated against the tenant's configured allowed document types
- [ ] Upload returns a confirmation with the document ID and current status
- [ ] Duplicate upload for the same account replaces the previous document (soft delete old record)
- [ ] Upload fails gracefully on network interruption with a retry-friendly error
- [ ] All upload attempts are logged in the audit trail

---

## Technical Notes

### Components
- **KYCModule** (`src/Modules/KYC/`): Core module handling KYC document management
  - `KYCService.cs`: gRPC service implementation
  - `DocumentEncryptionService.cs`: AES-256 encryption/decryption logic
  - `DocumentStorageService.cs`: File system storage with tenant-partitioned paths
  - `KYCDocumentRepository.cs`: Data access for `kyc_documents` table
- **SharedKernel** (`src/SharedKernel/`): Encryption abstractions, HSM key reference interfaces
- **ApiGateway** (`src/ApiGateway/`): gRPC gateway routing for KYC endpoints

### API / gRPC Endpoints

**Service:** `KYCService`

```protobuf
service KYCService {
  // Client streaming: upload document in chunks
  rpc UploadDocument(stream UploadDocumentRequest) returns (UploadDocumentResponse);
}

message UploadDocumentRequest {
  oneof payload {
    DocumentMetadata metadata = 1;   // First message in stream
    bytes chunk = 2;                  // Subsequent messages: file data chunks
  }
}

message DocumentMetadata {
  string account_id = 1;
  string document_type = 2;          // e.g., "NATIONAL_ID", "NRC", "BI"
  string file_name = 3;
  string content_type = 4;           // "image/jpeg" or "image/png"
  int64 file_size = 5;               // Total size in bytes for validation
}

message UploadDocumentResponse {
  string document_id = 1;
  string status = 2;                 // "uploaded"
  string message = 3;
  google.protobuf.Timestamp uploaded_at = 4;
}
```

**Upload Flow:**
1. First message contains `DocumentMetadata` (validated before accepting chunks)
2. Subsequent messages contain `bytes chunk` (recommended chunk size: 64KB)
3. Server assembles chunks into a temporary file, validates, encrypts, then moves to final storage

### Database Changes

**Table:** `kyc_documents` (schema: `{tenant_schema}`)

```sql
CREATE TABLE kyc_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    document_type VARCHAR(50) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(50) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    encryption_key_ref VARCHAR(255) NOT NULL,
    encryption_algorithm VARCHAR(50) NOT NULL DEFAULT 'AES-256-GCM',
    status VARCHAR(30) NOT NULL DEFAULT 'uploaded',
    checksum_sha256 VARCHAR(64) NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT fk_kyc_documents_account FOREIGN KEY (account_id) REFERENCES accounts(id)
);

CREATE INDEX idx_kyc_documents_account_id ON kyc_documents(account_id);
CREATE INDEX idx_kyc_documents_status ON kyc_documents(status);
```

**Status Values:** `uploaded`, `processing`, `verified`, `rejected`, `expired`

### Security Considerations
- **Encryption at Rest:** All document files encrypted using AES-256-GCM before writing to disk. Encryption key references stored in the database; actual keys managed by HSM (Hardware Security Module) or a key management service.
- **Transport Security:** gRPC streams over TLS 1.3. Mutual TLS between internal services.
- **Access Control:** Only the owning user and authorized KYC reviewers can access the document. Role-based access enforced at the gRPC interceptor level.
- **Data Retention:** Documents retained per regulatory requirements (varies by country/tenant). Automated purge after configurable retention period.
- **File Validation:** Server-side validation of image magic bytes (not just extension) to prevent malicious file uploads. Image re-encoding to strip EXIF metadata containing GPS or device information.
- **Storage Isolation:** Files stored in tenant-partitioned directory structure: `{storage_root}/{tenant_id}/kyc/{account_id}/{document_id}`.
- **Audit Trail:** Every upload attempt (success or failure) logged with account_id, IP address, device_id, timestamp, and outcome.

### Edge Cases
- **Network interruption during streaming upload:** Server should detect incomplete streams and clean up partial files. Client should be able to retry the entire upload.
- **Duplicate uploads:** If a user uploads a new document when one already exists, soft-delete the old record and file, create a new one. Only one active document per type per account.
- **Corrupted image:** Validate image integrity server-side by attempting to decode the image header. Reject corrupted files with an informative error.
- **Maximum concurrent uploads:** Rate limit to 1 concurrent upload per account to prevent abuse.
- **Storage full:** Monitor disk usage; alert when storage reaches 80% capacity. Return a service unavailable error if storage is critically low.
- **Very slow connections:** Set a reasonable streaming timeout (e.g., 5 minutes) to prevent indefinite resource holding.
- **File exactly at 10MB limit:** Accept files up to and including 10MB (10,485,760 bytes).

---

## Dependencies

**Prerequisite Stories:**
- STORY-009: User Registration (account must exist before KYC upload)

**Blocked Stories:**
- STORY-012: KYC - Selfie Capture & Photo Match (requires uploaded ID document)
- STORY-013: Account Activation on KYC Approval (requires completed KYC)

**External Dependencies:**
- HSM or Key Management Service for encryption key management
- Sufficient disk storage provisioned for document storage
- TLS certificates for gRPC transport security

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] gRPC streaming upload verified end-to-end with test images
- [ ] Encryption verified: stored files are not readable without decryption
- [ ] File validation tested with invalid formats, oversized files, and corrupted images
- [ ] Tenant isolation verified: documents stored in correct tenant-partitioned paths

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
