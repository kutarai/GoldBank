# SynergySwitch

`switch/` is a satellite service that bridges card-acceptance hardware
(POS terminals, ATMs) and national payment networks (Zimswitch) to the
GoldBank gateway. It runs as a separate container (`goldbank-switch`) but
shares the same Postgres instance (different DB: `synergy_switch`).

```
                            ┌──────────────────────────────────────────────┐
   Smart POS terminals      │            SYNERGY SWITCH                    │
   speaking ISO 20022 ─gRPC►│                                              │
                            │  Inbound:                                    │
   Legacy POS / ATMs        │   • gRPC server on :3333  (ISO 20022 + admin)│
   speaking ISO 8583 ─TCP──►│   • TCP listener on :3334 (ISO 8583)         │
                            │                                              │
   Zimswitch                │  Outbound:                                   │
   national network ─TCP───►│   • TCP pool to Zimswitch   (ISO 8583)       │
                            │   • gRPC to gateway:1111    (on-us auth)     │
                            │                                              │
   Operations team   ──HTTP►│   • Web admin on :8080      (Razor Pages)    │
                            └────────────────────┬─────────────────────────┘
                                                 │
                                                 ▼ gRPC CardTransactionService
                                       ┌────────────────────────┐
                                       │   GOLDBANK GATEWAY     │
                                       │   Modules/CardTransac. │
                                       └────────────────────────┘
```

## Why it exists

A core-banking gateway shouldn't know about ISO 8583 wire frames or
terminal-vendor quirks. The switch:

1. **Normalises wire protocols.** ISO 8583 (TCP, MTI + bitmap, BCD-encoded
   PAN) and ISO 20022 (JSON over gRPC) both become a single
   `CardTransactionAuthorisation` request on the gateway side.
2. **Routes on-us vs off-us.** A transaction where both cardholder *and*
   merchant are GoldBank customers stays local (gateway gRPC). One where
   the cardholder is on a foreign bank (Stanbic, Steward, NMB, …) gets
   shipped out over ISO 8583 to Zimswitch.
3. **Connection pooling.** ISO 8583 partner links are persistent TCP
   sockets. The switch holds the pool — the gateway never sees it.
4. **Settlement.** End-of-day batches are reconciled against Zimswitch
   files inside the switch's own DB; the gateway only consumes the
   summary.

## On-us / off-us routing

`Routing/BinResolver.cs` does longest-prefix BIN matching:

```
Configured BIN ranges:
  6275 00 ……  → on-us   (GoldBank issuer ID)
  6275 12 ……  → off-us  (Stanbic — same first 4 but different range)
  4000 00 ……  → off-us  (Visa)
  5100 00 ……  → off-us  (Mastercard)

A transaction's PAN:
  6275 0012 3456 7890   ← on-us  (matches 6275 00 longer than any other)
  6275 1234 5678 9012   ← off-us
  4000 1111 2222 3333   ← off-us
```

The card BIN prefix list is seeded in `system_configs` under
`card.bin_prefix` (a JSON-stringified `"6275"` default) and extended at
runtime by ops staff via the switch's web admin. On-us transactions
short-circuit national-network egress, which has two benefits:

- **Lower interchange.** Zimswitch charges per-transaction; on-us avoids
  it.
- **Faster auth.** Skip a TCP round-trip + network ISO parse.

## Ports

| Port | Protocol | Direction | Used by |
| --- | --- | --- | --- |
| 3333 | gRPC (h2c) | inbound | smart POS terminals, gateway admin operations |
| 3334 | TCP (ISO 8583) | inbound | legacy POS / ATMs / national link |
| 8080 | HTTP (Razor Pages) | inbound | switch operations dashboard |
| 1111 | gRPC (h2c) | **outbound** | calls the gateway's `CardTransactionService` |

In `docker-compose.yml` these map to host ports as
`SWITCH_GRPC_PORT=3333`, `SWITCH_WEB_PORT=8080`. The ISO 8583 port isn't
exposed to the host by default — it's intra-container only.

## Database

The switch owns the `synergy_switch` PostgreSQL database (separate from
the bank's `goldbank` DB; same instance). Tables of note:

- `terminals` — registered POS / ATM hardware (terminal ID, merchant,
  certificate fingerprint).
- `bin_ranges` — runtime-editable list of card BIN prefixes and their
  destinations.
- `transactions` — the switch's own transaction log; cross-references the
  gateway's `card_transactions` table via `external_id`.
- `iso8583_messages` — raw wire frames (for forensic / dispute use).
- `settlement_batches` — daily files received from Zimswitch.

Migrations are applied by the `goldbank-switch-migrator` container that
runs once at compose-up time.

## Configuration

| Env var | Default | Purpose |
| --- | --- | --- |
| `BankConnection__Host` | `gateway` | gRPC target for the gateway |
| `BankConnection__Port` | `1111` | gRPC port for the gateway |
| `BankConnection__OfflineMode` | `false` | Buffer authorisations locally when the gateway is down |
| `Kestrel__Endpoints__Grpc__Url` | `http://0.0.0.0:3333` | Inbound gRPC bind |
| `Kestrel__Endpoints__Web__Url` | `http://0.0.0.0:8080` | Admin web bind |
| `ConnectionStrings__SwitchDb` | `Host=postgres;...;Database=synergy_switch;...` | Postgres |

`OfflineMode` is the EFT-grade resilience knob: when `true`, the switch
will accept transactions up to a configurable floor limit, cache them
locally, and replay them when the gateway is back. Off by default for
dev.

## Talking to the switch as a developer

You generally don't. The two ways you might:

1. **Simulate a card auth** — `scripts/iso8583-simulator.py` (if present)
   or any external ISO 8583 tool pointed at `localhost:3334`.
2. **Operations web** — open `http://localhost:8080` to see live
   transaction log, gateway health, BIN range editor, settlement files.

The mobile / bank-client apps don't talk to the switch — they only ever
talk to the gateway. The switch is purely a card-network plane.

## Status

The on-disk `switch/` folder has been seen empty in some checkouts — the
service is described in `docker-compose.yml`, in
`docs/epics/EPIC-016-synergy-switch-integration.md`, and built via
`switch/Containerfile` (the build context expects a full project tree
that's not always synced with the repo HEAD). If you find an empty
`switch/`, that's the EPIC-016 implementation pending merge in a topic
branch.

## Failure modes

| Symptom | Likely cause |
| --- | --- |
| Gateway returns `Unavailable` from `CardTransactionService` | Switch container is up but gateway is down. Check `podman ps`. |
| Switch logs `ResolveBin: no match` | New BIN range needs to be added in switch admin. |
| ISO 8583 connections timing out | Zimswitch keep-alive failing. Restart the switch container to reset the TCP pool. |
| `on-us` transactions still go off-us | `card.bin_prefix` in `system_configs` doesn't match the issued card PANs. Check `account.card_pan` first digits. |
| Settlement files not appearing | The `goldbank-switch` container lacks volume mount for `/settlement/`. See `docker-compose.yml`. |
