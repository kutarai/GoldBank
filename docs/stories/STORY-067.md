# STORY-067: Exportable Reports

**Epic:** EPIC-012 Reporting & Analytics
**Priority:** Should Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an admin
I want to export reports as CSV and PDF
So that I can share data offline with stakeholders and maintain records

---

## Description

### Background
While the admin portal provides rich interactive reports, finance and operations staff frequently need to share report data with external stakeholders (auditors, regulators, management) who may not have admin portal access. Reports also need to be archived for regulatory compliance and record-keeping. This story adds export functionality across all report types, supporting both CSV (for data manipulation in spreadsheets) and PDF (for formal distribution). Large exports are handled via gRPC server streaming to avoid timeout issues and memory pressure.

The export function is designed as a cross-cutting capability that integrates with every report page built in STORY-062 through STORY-066, rather than being a standalone page.

### Scope
**In scope:**
- CSV export for all report types
- PDF export for all report types
- Export includes: report title, generation timestamp, applied filters, date range, data rows, summary totals
- gRPC server streaming for large exports (chunked delivery of 1,000 rows)
- Browser download initiation from Blazor UI
- Export file naming convention: `{report_type}_{tenant}_{date_range}.{format}`
- Export queue for large reports with notification on completion

**Out of scope:**
- Scheduled/recurring report exports (e.g., email every Monday)
- Custom report builder
- Excel (XLSX) format (CSV covers spreadsheet use case)
- Report template customisation
- Archival/retention management of exported files

### User Flow
1. Admin views any report page (Dashboard, User Growth, Merchant Performance, Revenue, Reconciliation)
2. Admin configures the report filters (date range, tenant, etc.) as desired
3. Admin clicks the "Export" button in the report header
4. A dropdown appears with format options: CSV, PDF
5. Admin selects the desired format
6. For small reports (< 5,000 rows): export generates immediately and browser download starts
7. For large reports (>= 5,000 rows): export is queued and a notification appears "Your export is being generated. You will be notified when it is ready."
8. When the large export completes, a notification appears with a download link
9. The downloaded file includes:
   - Report title and type
   - Generation timestamp
   - Applied filters and date range
   - Column headers
   - Data rows
   - Summary totals (if applicable)
10. File is named following the convention: `{report_type}_{tenant}_{date_range}.{format}`

---

## Acceptance Criteria

- [ ] Export button is present on all report pages: Dashboard, User Growth, Merchant Performance, Revenue, Reconciliation
- [ ] Export format options include CSV and PDF
- [ ] CSV export produces a valid CSV file with proper escaping of special characters
- [ ] CSV export includes column headers as the first row
- [ ] PDF export produces a properly formatted document with headers, data table, and page numbers
- [ ] Exported files include: report title, generation timestamp (in admin's timezone), applied filters, date range
- [ ] Exported files include summary totals where applicable (e.g., total revenue, total transactions)
- [ ] File naming follows convention: `{report_type}_{tenant}_{date_range}.{format}` (e.g., `revenue_tenantA_2026-01-01_2026-01-31.csv`)
- [ ] Small exports (< 5,000 rows) generate and download immediately (within 5 seconds)
- [ ] Large exports (>= 5,000 rows) are queued with a notification when ready
- [ ] Large exports use gRPC server streaming to deliver data in chunks of 1,000 rows
- [ ] Download link for queued exports remains available for 24 hours
- [ ] Export respects the current filters and date range applied to the report
- [ ] Export respects tenant scoping (`tenant_admin` can only export their own tenant's data)
- [ ] All export actions are audit-logged with admin_user_id, report_type, filters, format, timestamp

---

## Technical Notes

### Components
- **Blazor Components:**
  - `Components/Reports/ExportButton.razor` -- reusable export button with format dropdown, placed on all report pages
  - `Components/Reports/ExportProgressNotification.razor` -- notification component for queued export status
  - `Components/Reports/ExportDownloadLink.razor` -- download link component for completed exports
- **Services:**
  - `Services/ReportExportService.cs` -- orchestrates export generation for all report types
  - `Services/CsvExportGenerator.cs` -- generates CSV output using CsvHelper library
  - `Services/PdfExportGenerator.cs` -- generates PDF output using QuestPDF library
  - `Services/ExportQueueService.cs` -- manages async export queue for large reports
  - `Services/ExportStorageService.cs` -- stores generated files temporarily (local filesystem or blob storage)
- **Wolverine Handlers:**
  - `Handlers/GenerateExportHandler.cs` -- background handler for queued large exports
  - `Messages/ExportRequested.cs` -- message published when large export is queued
  - `Messages/ExportCompleted.cs` -- message published when export is ready for download

### API / gRPC Endpoints
```protobuf
service ReportingService {
  // Synchronous export for small datasets
  rpc ExportReport (ExportReportRequest) returns (stream ExportChunk);

  // Async export for large datasets
  rpc QueueExport (QueueExportRequest) returns (QueueExportResponse);
  rpc GetExportStatus (GetExportStatusRequest) returns (ExportStatusResponse);
  rpc DownloadExport (DownloadExportRequest) returns (stream ExportChunk);
}

message ExportReportRequest {
  string report_type = 1;          // dashboard, user_growth, merchant_performance, revenue, reconciliation
  string format = 2;               // csv, pdf
  string tenant_id = 3;            // auto-set for tenant_admin
  google.protobuf.Timestamp date_from = 4;
  google.protobuf.Timestamp date_to = 5;
  string granularity = 6;          // daily, weekly, monthly (where applicable)
  map<string, string> filters = 7; // additional report-specific filters
  string admin_user_id = 8;
  string timezone = 9;             // admin's timezone for timestamp formatting
}

message ExportChunk {
  bytes data = 1;                  // chunk of file data (up to 64KB)
  int32 chunk_number = 2;
  int32 total_chunks = 3;         // 0 if unknown (streaming)
  bool is_last = 4;
  string content_type = 5;        // text/csv or application/pdf
  string filename = 6;            // only on first chunk
}

message QueueExportRequest {
  string report_type = 1;
  string format = 2;
  string tenant_id = 3;
  google.protobuf.Timestamp date_from = 4;
  google.protobuf.Timestamp date_to = 5;
  string granularity = 6;
  map<string, string> filters = 7;
  string admin_user_id = 8;
  string timezone = 9;
}

message QueueExportResponse {
  string export_id = 1;
  string status = 2;               // queued
  string message = 3;
  int32 estimated_seconds = 4;     // estimated time to complete
}

message GetExportStatusRequest {
  string export_id = 1;
}

message ExportStatusResponse {
  string export_id = 1;
  string status = 2;               // queued, processing, completed, failed
  double progress = 3;             // 0.0 to 1.0
  string download_url = 4;         // populated when completed
  google.protobuf.Timestamp expires_at = 5;  // download link expiry
  string error_message = 6;        // populated when failed
}

message DownloadExportRequest {
  string export_id = 1;
}
```

### Database Changes
```sql
-- Export job tracking table (in admin schema)
CREATE TABLE admin.export_jobs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES admin.admin_users(id),
    report_type     VARCHAR(50) NOT NULL,
    format          VARCHAR(10) NOT NULL CHECK (format IN ('csv', 'pdf')),
    tenant_id       UUID,
    date_from       TIMESTAMPTZ NOT NULL,
    date_to         TIMESTAMPTZ NOT NULL,
    filters         JSONB,
    status          VARCHAR(20) NOT NULL DEFAULT 'queued'
                        CHECK (status IN ('queued', 'processing', 'completed', 'failed')),
    progress        FLOAT NOT NULL DEFAULT 0.0,
    file_path       VARCHAR(500),              -- path to generated file
    file_size_bytes BIGINT,
    filename        VARCHAR(255),              -- download filename
    error_message   TEXT,
    row_count       INT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ                -- 24 hours after completion
);

CREATE INDEX idx_export_jobs_admin ON admin.export_jobs(admin_user_id);
CREATE INDEX idx_export_jobs_status ON admin.export_jobs(status) WHERE status IN ('queued', 'processing');
CREATE INDEX idx_export_jobs_expires ON admin.export_jobs(expires_at) WHERE status = 'completed';
```

### CSV Generation Details
Using the **CsvHelper** library:
```csharp
// CSV generation pattern
public async Task<Stream> GenerateCsvAsync<T>(
    IAsyncEnumerable<T> data,
    ExportMetadata metadata,
    CancellationToken ct)
{
    var stream = new MemoryStream();
    await using var writer = new StreamWriter(stream, leaveOpen: true);
    await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    // Write metadata header as comments
    await writer.WriteLineAsync($"# Report: {metadata.ReportTitle}");
    await writer.WriteLineAsync($"# Generated: {metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
    await writer.WriteLineAsync($"# Date Range: {metadata.DateFrom:yyyy-MM-dd} to {metadata.DateTo:yyyy-MM-dd}");
    await writer.WriteLineAsync($"# Filters: {metadata.FiltersDescription}");
    await writer.WriteLineAsync("#");

    // Write headers and data
    csv.WriteHeader<T>();
    await csv.NextRecordAsync();

    await foreach (var record in data.WithCancellation(ct))
    {
        csv.WriteRecord(record);
        await csv.NextRecordAsync();
    }

    // Write summary footer
    await writer.WriteLineAsync();
    await writer.WriteLineAsync($"# Total Rows: {metadata.RowCount}");
    foreach (var summary in metadata.SummaryTotals)
    {
        await writer.WriteLineAsync($"# {summary.Key}: {summary.Value}");
    }

    stream.Position = 0;
    return stream;
}
```

### PDF Generation Details
Using the **QuestPDF** library:
```csharp
// PDF generation pattern
public byte[] GeneratePdf<T>(
    IReadOnlyList<T> data,
    ExportMetadata metadata,
    IReadOnlyList<ColumnDefinition> columns)
{
    return Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);

            // Header
            page.Header().Element(header =>
            {
                header.Row(row =>
                {
                    row.RelativeItem().Text(metadata.ReportTitle)
                        .FontSize(16).Bold();
                    row.ConstantItem(200).AlignRight()
                        .Text($"Generated: {metadata.GeneratedAt:yyyy-MM-dd HH:mm}")
                        .FontSize(8);
                });
            });

            // Content: data table
            page.Content().Element(content =>
            {
                content.Table(table =>
                {
                    // Define columns
                    foreach (var col in columns)
                        table.ColumnsDefinition(cd => cd.RelativeColumn());

                    // Header row
                    foreach (var col in columns)
                        table.Cell().Background("#2c3e50")
                            .Padding(4).Text(col.Header)
                            .FontColor("#ffffff").FontSize(9).Bold();

                    // Data rows
                    foreach (var row in data)
                    {
                        foreach (var col in columns)
                        {
                            var value = col.ValueSelector(row);
                            table.Cell().BorderBottom(0.5f)
                                .Padding(3).Text(value).FontSize(8);
                        }
                    }
                });
            });

            // Footer: page numbers and summary
            page.Footer().Element(footer =>
            {
                footer.Row(row =>
                {
                    row.RelativeItem().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    }).FontSize(8);
                    row.RelativeItem().AlignRight()
                        .Text($"Total rows: {metadata.RowCount}")
                        .FontSize(8);
                });
            });
        });
    }).GeneratePdf();
}
```

### Security Considerations
- All admin roles can export reports they have access to (export inherits report-level RBAC)
- Export audit log captures: who exported, what report, what filters, what format
- Generated export files are stored with randomised filenames to prevent URL guessing
- Download links expire after 24 hours; expired files are deleted by a cleanup background job
- Export files are stored outside the web root to prevent direct URL access
- gRPC streaming validates authentication on each chunk (not just initial request)
- Rate limiting: maximum 5 export requests per admin per hour to prevent abuse
- Large export queue has a global concurrency limit (e.g., 3 concurrent exports) to prevent resource exhaustion

### Edge Cases
- Export with zero rows: generate file with headers and metadata only; display "No data matches the selected criteria"
- Very large export (> 100,000 rows): enforce maximum row limit; suggest narrower date range or additional filters
- Export timeout: for streaming exports, implement 10-minute timeout; queued exports have 30-minute timeout
- Concurrent exports by same admin: allow up to 2 concurrent queued exports; reject additional with "Maximum concurrent exports reached"
- PDF with very wide tables: use landscape orientation and auto-scale font size; very wide tables may need column selection
- CSV with special characters: ensure proper escaping of commas, quotes, and newlines within cell values (CsvHelper handles this)
- Export during report data refresh: use snapshot isolation to ensure consistent data throughout the export
- Browser download failure (network interruption): queued exports can be re-downloaded via the exports list; streaming exports must be retried
- File storage full: monitor disk usage; alert when export storage exceeds 80% capacity; auto-cleanup expired files
- Unicode in CSV: use UTF-8 with BOM to ensure Excel compatibility

---

## Dependencies

**Prerequisite Stories:**
- STORY-062 (Real-Time Transaction Dashboard)
- STORY-063 (User Growth & Registration Reports)
- STORY-064 (Merchant/Agent Performance Reports)
- STORY-065 (Revenue & Fee Reports)
- STORY-066 (Reconciliation Reports)

**Blocked Stories:** None
**External Dependencies:**
- `CsvHelper` NuGet package for CSV generation
- `QuestPDF` NuGet package for PDF generation (free Community license for revenue < $1M)
- Temporary file storage (local filesystem or blob storage for generated exports)
- Wolverine for async export job processing

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
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
