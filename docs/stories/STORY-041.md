# STORY-041: ISO 20022 Adapter

**Epic:** EPIC-008 National Network Switching
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **system**
I want to **format and parse ISO 20022 messages**
So that **transactions can be routed to modern national switches that use the ISO 20022 XML/JSON standard**

---

## Description

### Background

While ISO 8583 dominates legacy infrastructure, several national payment switches in the Southern African region are migrating to ISO 20022, the modern XML-based financial messaging standard. ISO 20022 offers richer data, better interoperability, and is the direction all major payment networks are heading (SWIFT has mandated full migration by 2025). Some national instant payment schemes already operate exclusively on ISO 20022.

GoldBank must support both protocols simultaneously because different switches and different transaction types may use different standards. The ISO 20022 adapter encapsulates all XML message construction, parsing, namespace management, and schema validation behind the same `ISwitchAdapter` interface used by the ISO 8583 adapter. This ensures the Message Router (STORY-042) can route to either protocol transparently.

The connectivity model for ISO 20022 switches is typically REST API or message queue (MQ) based, rather than raw TCP sockets. The adapter must support both connectivity modes, configurable per switch endpoint.

**Functional Requirements:** FR-064 (National Switch ISO 20022 Integration)

### Scope

**In scope:**
- ISO 20022 XML message builder for supported message types
- ISO 20022 XML message parser
- Mapping service between GoldBank canonical format and ISO 20022 message structures
- Message types: pacs.008 (FI to FI Customer Credit Transfer), pacs.002 (Payment Status Report), camt.053 (Bank to Customer Statement)
- REST API client connectivity for API-based switches
- MQ client connectivity for message queue-based switches (configurable per switch)
- Authentication: mTLS and API key support (configurable per switch)
- XML schema validation against ISO 20022 XSD definitions
- Full message logging (raw XML and parsed canonical) to the switching schema
- Implementation of `ISwitchAdapter` interface for adapter pattern compliance
- JSON serialization support for switches that accept ISO 20022 in JSON format

**Out of scope:**
- ISO 8583 message handling (STORY-040)
- Message routing logic (STORY-042)
- Settlement file generation (STORY-045)
- MQ broker provisioning (infrastructure concern)
- Full ISO 20022 message catalogue (only pacs.008, pacs.002, camt.053 for MVP)

### User Flow

This is a system-to-system adapter. The primary interaction flows are:

**Outbound Flow (GoldBank to National Switch via API):**
1. Message Router calls `ISwitchAdapter.SendAsync(CanonicalMessage)` on the ISO 20022 adapter
2. Adapter maps canonical message fields to ISO 20022 XML elements using `CanonicalToISO20022Mapper`
3. Adapter constructs the full XML document with correct namespaces, headers, and message body
4. Adapter validates the XML against the ISO 20022 XSD schema
5. Adapter sends the XML via HTTPS POST (REST) or publishes to MQ (based on switch configuration)
6. For REST: awaits HTTP response, parses response XML/JSON
7. For MQ: awaits response on reply queue with correlation ID
8. Response is parsed back to canonical format using `ISO20022ToCanonicalMapper`
9. Full message (request and response) logged to `switch_messages` table

**Outbound Flow (GoldBank to National Switch via MQ):**
1. Steps 1-4 same as API flow
2. Adapter publishes the XML message to the outbound MQ queue with a unique correlation ID
3. Adapter subscribes to the reply queue, filtering by correlation ID
4. Await response with configurable timeout (default 30 seconds)
5. Steps 8-9 same as API flow

**Inbound Flow (National Switch to GoldBank):**
1. API endpoint receives an HTTP POST with ISO 20022 XML, or MQ consumer receives a message
2. Adapter validates the XML against the XSD schema
3. Adapter parses the XML and maps to canonical format using `ISO20022ToCanonicalMapper`
4. Adapter passes the canonical message to the Message Router for processing
5. After processing, the adapter formats the response as ISO 20022 XML and returns it
6. Full message logged to `switch_messages` table

---

## Acceptance Criteria

- [ ] ISO 20022 XML message builder correctly constructs valid XML documents for pacs.008 (Customer Credit Transfer)
- [ ] ISO 20022 XML message builder correctly constructs valid XML documents for pacs.002 (Payment Status Report)
- [ ] ISO 20022 XML message builder correctly constructs valid XML documents for camt.053 (Bank to Customer Statement)
- [ ] ISO 20022 XML parser correctly decodes all three supported message types into structured data
- [ ] Mapping between canonical message format and ISO 20022 is bidirectional and lossless for all supported fields
- [ ] XML output validates against official ISO 20022 XSD schemas
- [ ] REST API client sends messages via HTTPS POST and correctly handles responses (2xx success, 4xx client error, 5xx server error)
- [ ] MQ client publishes to outbound queue and consumes from reply queue with correlation ID matching
- [ ] mTLS authentication is supported for REST connections (client certificate configured per switch)
- [ ] API key authentication is supported for REST connections (configurable header name and value per switch)
- [ ] Full message logging captures: direction, message type, raw XML, parsed canonical JSON, and timestamp in the `switch_messages` table
- [ ] JSON serialization mode is available as an alternative to XML for switches that accept ISO 20022 JSON
- [ ] Invalid inbound XML that fails schema validation is rejected with appropriate error response and logged

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `GoldBank.Switching` | `src/Satellites/GoldBank.Switching/` | Satellite service project |
| `ISO20022Adapter.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/` | `ISwitchAdapter` implementation for ISO 20022 |
| `ISO20022MessageBuilder.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/` | Constructs XML documents from canonical messages |
| `ISO20022MessageParser.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/` | Parses XML documents into structured data |
| `CanonicalToISO20022Mapper.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/Mapping/` | Maps canonical format to ISO 20022 elements |
| `ISO20022ToCanonicalMapper.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/Mapping/` | Maps ISO 20022 elements to canonical format |
| `XsdValidator.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO20022/Validation/` | Validates XML against ISO 20022 XSD schemas |
| `RestSwitchClient.cs` | `src/Satellites/GoldBank.Switching/Network/` | HTTP client for REST API-based switches |
| `MqSwitchClient.cs` | `src/Satellites/GoldBank.Switching/Network/` | MQ client for message queue-based switches |
| `SwitchMessageLogger.cs` | `src/Satellites/GoldBank.Switching/Logging/` | Shared logger (same as ISO 8583) |
| `ISwitchAdapter.cs` | `src/Satellites/GoldBank.Switching/Adapters/` | Shared interface |

### API / gRPC Endpoints

The ISO 20022 adapter implements the same `ISwitchAdapter` interface as the ISO 8583 adapter (see STORY-040).

**Inbound Webhook Endpoint** (for switches that push messages to GoldBank):

```
POST /api/v1/switch/iso20022/inbound
Content-Type: application/xml
Authorization: Bearer {api-key} or mTLS

Body: ISO 20022 XML document

Response: ISO 20022 XML document (pacs.002 status report)
```

**ISO 20022 Message Type Mappings:**

**pacs.008.001.10 — FI to FI Customer Credit Transfer:**
```xml
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:pacs.008.001.10">
  <FIToFICstmrCdtTrf>
    <GrpHdr>
      <MsgId>{CanonicalMessage.TransactionReference}</MsgId>
      <CreDtTm>{CanonicalMessage.Timestamp:ISO 8601}</CreDtTm>
      <NbOfTxs>1</NbOfTxs>
      <SttlmInf>
        <SttlmMtd>CLRG</SttlmMtd>
      </SttlmInf>
    </GrpHdr>
    <CdtTrfTxInf>
      <PmtId>
        <InstrId>{InstructionId}</InstrId>
        <EndToEndId>{CanonicalMessage.TransactionReference}</EndToEndId>
      </PmtId>
      <IntrBkSttlmAmt Ccy="{CanonicalMessage.Currency}">
        {CanonicalMessage.Amount}
      </IntrBkSttlmAmt>
      <DbtrAgt>
        <FinInstnId>
          <BICFI>{CanonicalMessage.SourceInstitution BIC}</BICFI>
        </FinInstnId>
      </DbtrAgt>
      <CdtrAgt>
        <FinInstnId>
          <BICFI>{CanonicalMessage.DestinationInstitution BIC}</BICFI>
        </FinInstnId>
      </CdtrAgt>
      <Dbtr>
        <Nm>{DebtorName}</Nm>
      </Dbtr>
      <DbtrAcct>
        <Id><Othr><Id>{CanonicalMessage.SourceAccount}</Id></Othr></Id>
      </DbtrAcct>
      <Cdtr>
        <Nm>{CreditorName}</Nm>
      </Cdtr>
      <CdtrAcct>
        <Id><Othr><Id>{CanonicalMessage.DestinationAccount}</Id></Othr></Id>
      </CdtrAcct>
    </CdtTrfTxInf>
  </FIToFICstmrCdtTrf>
</Document>
```

**pacs.002.001.12 — Payment Status Report:**
```xml
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:pacs.002.001.12">
  <FIToFIPmtStsRpt>
    <GrpHdr>
      <MsgId>{ResponseMessageId}</MsgId>
      <CreDtTm>{Timestamp}</CreDtTm>
    </GrpHdr>
    <TxInfAndSts>
      <OrgnlEndToEndId>{OriginalTransactionReference}</OrgnlEndToEndId>
      <TxSts>ACCP|RJCT|PDNG</TxSts>
      <StsRsnInf>
        <Rsn><Cd>{ReasonCode}</Cd></Rsn>
      </StsRsnInf>
    </TxInfAndSts>
  </FIToFIPmtStsRpt>
</Document>
```

**camt.053.001.10 — Bank to Customer Statement (for reconciliation):**
```xml
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.10">
  <BkToCstmrStmt>
    <Stmt>
      <Id>{StatementId}</Id>
      <CreDtTm>{Timestamp}</CreDtTm>
      <Acct>
        <Id><Othr><Id>{AccountId}</Id></Othr></Id>
      </Acct>
      <Bal>
        <Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp>
        <Amt Ccy="{Currency}">{OpeningBalance}</Amt>
      </Bal>
      <Ntry>
        <!-- One entry per transaction -->
        <Amt Ccy="{Currency}">{Amount}</Amt>
        <CdtDbtInd>CRDT|DBIT</CdtDbtInd>
        <Sts><Cd>BOOK</Cd></Sts>
        <NtryRef>{TransactionReference}</NtryRef>
      </Ntry>
    </Stmt>
  </BkToCstmrStmt>
</Document>
```

### Database Changes

Uses the same `switching.switch_messages` table defined in STORY-040. The `protocol` column will be set to `'ISO20022'` for messages handled by this adapter. Additional fields used:

- `mti` column stores the ISO 20022 message type (e.g., `pacs.008`, `pacs.002`, `camt.053`) instead of the ISO 8583 MTI
- `raw_hex` column stores the raw XML string (not hex-encoded, since ISO 20022 is text-based)
- `parsed_json` column stores the canonicalized JSON representation

**switch_endpoint_config table** (stores per-switch connectivity configuration):

```sql
CREATE TABLE switching.switch_endpoint_config (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id  VARCHAR(20) NOT NULL,
    adapter_type    VARCHAR(20) NOT NULL CHECK (adapter_type IN ('ISO8583', 'ISO20022')),
    protocol        VARCHAR(10) NOT NULL CHECK (protocol IN ('TCP', 'REST', 'MQ')),
    endpoint_url    VARCHAR(500) NOT NULL,
    auth_type       VARCHAR(10) NOT NULL CHECK (auth_type IN ('MTLS', 'API_KEY', 'NONE')),
    api_key_header  VARCHAR(100),
    api_key_value   VARCHAR(500),  -- encrypted at rest
    client_cert_thumbprint VARCHAR(64),
    mq_outbound_queue VARCHAR(200),
    mq_reply_queue    VARCHAR(200),
    timeout_seconds INT NOT NULL DEFAULT 30,
    is_active       BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (institution_id, adapter_type)
);
```

### Security Considerations

- **mTLS for REST Connections:** When connecting to switches via REST API, the adapter must support mutual TLS. The client certificate is loaded from the certificate store or file path configured per switch endpoint. The server certificate is validated against a pinned CA or thumbprint.
- **API Key Security:** API keys stored in `switch_endpoint_config.api_key_value` must be encrypted at rest using the data protection API or a dedicated encryption key. API keys are sent in the configured HTTP header (e.g., `X-API-Key`, `Authorization: Bearer`).
- **XML Security:** Protect against XML-specific attacks:
  - **XXE (XML External Entity):** Disable external entity resolution in the XML parser (`XmlReaderSettings.DtdProcessing = DtdProcessing.Prohibit`)
  - **XML Bomb:** Set `XmlReaderSettings.MaxCharactersFromEntities` to a reasonable limit
  - **XPath Injection:** Use parameterized XPath queries, never string concatenation
- **Schema Validation:** All inbound XML must be validated against the official ISO 20022 XSD schemas before processing. Invalid XML is rejected at the adapter boundary.
- **Sensitive Data in XML:** Account numbers and names in ISO 20022 messages are logged in full in the raw XML (needed for reconciliation), but the `switch_messages` table must have restricted access. For the parsed JSON, mask account numbers to show only last 4 digits.
- **Message Signing:** Some ISO 20022 switches require XML Digital Signatures (XMLDSig). The adapter should support optional message signing using the HSM Interface Service for key storage, though this is not required for MVP.

### Edge Cases

- **REST API Timeout:** If the switch REST API does not respond within the configured timeout, return a decline status to the caller. Implement retry with exponential backoff for 5xx errors (max 3 retries, 1s/2s/4s delays).
- **MQ Connection Loss:** If the MQ broker connection drops, implement automatic reconnection with backoff. Pending correlation entries should be timed out after the configured timeout.
- **MQ Reply Timeout:** If no reply arrives on the MQ reply queue within the timeout, return a decline status. Log the timeout for reconciliation purposes.
- **Invalid XML Response:** If the switch returns XML that does not conform to the expected ISO 20022 schema, log the raw response, attempt best-effort parsing, and flag the message for manual review.
- **Character Encoding:** ISO 20022 XML uses UTF-8. Ensure all XML processing uses UTF-8 encoding. Handle BOM (Byte Order Mark) if present in responses.
- **Large camt.053 Statements:** Bank-to-customer statements can contain thousands of entries. Implement streaming XML parsing (`XmlReader`) rather than loading the entire document into memory (`XDocument`).
- **Namespace Versioning:** Different switches may use different versions of ISO 20022 schemas (e.g., pacs.008.001.08 vs pacs.008.001.10). The adapter must handle version negotiation or be configured per switch.
- **HTTP 429 Rate Limiting:** If the switch returns HTTP 429 (Too Many Requests), respect the `Retry-After` header and queue subsequent requests.
- **Duplicate Message Detection:** ISO 20022 messages include a `MsgId` (Message Identification) that must be unique. The adapter must generate unique IDs and detect duplicate inbound messages by checking `MsgId` against the `switch_messages` table.

---

## Dependencies

**Prerequisite Stories:**
- STORY-004: gRPC Proto Definitions — proto files for Switching Service internal API

**Blocked Stories:**
- STORY-042: Message Router & Canonical Format — the router depends on this adapter being available
- STORY-043: Outbound Transaction Routing — outbound routing uses this adapter for ISO 20022 destinations
- STORY-044: Inbound Transaction Processing — inbound processing uses this adapter to parse ISO 20022 messages
- STORY-045: Daily Reconciliation — reconciliation may use camt.053 messages from this adapter

**External Dependencies:**
- National switch sandbox environment for integration testing (REST API endpoint or MQ broker, credentials, test data)
- ISO 20022 XSD schema files for pacs.008, pacs.002, camt.053
- Switch-specific API documentation (endpoint URLs, authentication methods, message variants)
- MQ broker (RabbitMQ or IBM MQ) for MQ-based switch connectivity testing
- mTLS certificates for switch connections

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — XML builder/parser tested against known ISO 20022 sample messages
- [ ] Integration tests passing with mock REST API switch and mock MQ broker
- [ ] All three message types (pacs.008, pacs.002, camt.053) correctly built and parsed
- [ ] XML output validates against official ISO 20022 XSD schemas
- [ ] Canonical-to-ISO 20022 mapping tested for round-trip fidelity
- [ ] mTLS and API key authentication tested
- [ ] Message logging verified — all messages captured in `switch_messages` with correct raw XML and parsed JSON
- [ ] XXE and XML bomb protections verified
- [ ] Code reviewed and approved
- [ ] Documentation updated (adapter usage, message type mapping, switch configuration)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
