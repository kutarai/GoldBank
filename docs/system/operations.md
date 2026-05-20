# Operations

How to run the system locally, where the moving parts live, and how to
troubleshoot when something goes wrong.

## Quick start

```powershell
# 1. Start the container backbone (Postgres, Redis, gateway, switch, admin, etc.)
cd c:\Users\wmapu\Projects\GoldBank
python -m podman_compose --profile core up -d

# 2. Apply migrations + seed demo data (first time only)
cd server\GoldBank.Migrator
$env:GOLDBANK_ConnectionStrings__DefaultConnection = `
  "Host=localhost;Port=5432;Database=goldbank;Username=goldbank;Password=goldbank_dev_password"
dotnet run -- --demo
cd ..\..

# 3. Start the React dev servers (in two separate terminals)
cd bank-client; $env:OPENSSL_CONF = ''; npm run dev      # :5173
cd bank-teller; $env:OPENSSL_CONF = ''; npm run dev      # :5174

# 4. Build + install the mobile APK (emulator must already be running)
cd mobile
.\gradlew :androidApp:assembleDebug
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r androidApp\build\outputs\apk\debug\androidApp-debug.apk
```

## Container layout

`docker-compose.yml` defines all the long-running services. Three
profiles select which ones come up:

| Profile | Brings up |
| --- | --- |
| `core` *(default for dev)* | postgres, redis, gateway, switch, admin, hsm, notifications, server-migrator, switch-migrator |
| `ai` | ollama (multi-GB image; start separately when AI features needed) |
| `monitoring` | prometheus, grafana, elasticsearch, kibana |

To bring up a single service:
```powershell
python -m podman_compose --profile core up -d gateway
```

## Port map

| Service | Container port | Host port | Protocol |
| --- | --- | --- | --- |
| postgres | 5432 | 5432 | PostgreSQL |
| redis | 6379 | 6379 | Redis |
| gateway (gRPC) | 1111 | **5000** | gRPC h2c (plaintext HTTP/2) |
| gateway (REST) | 1112 | **5001** | HTTP/1.1 |
| switch (gRPC) | 3333 | 3333 | gRPC h2c |
| switch (web admin) | 8080 | 8080 | HTTP |
| admin (Blazor) | 5010 | 5010 | HTTP |
| hsm | 5005 | 5005 | HTTP |
| notifications | 5007 | 5007 | HTTP |
| ollama | 11434 | 11434 | HTTP (OpenAI-compatible) |
| **bank-client** (Vite dev) | — | **5173** | host process |
| **bank-teller** (Vite dev) | — | **5174** | host process |
| mobile (Android emulator) | — | uses `10.0.2.2:5000` | host loopback |

## Environment variables

The compose file reads from `.env` (not committed) and falls back to
defaults. The `.env.example` at the repo root lists every variable.
Common overrides:

| Variable | Default | Purpose |
| --- | --- | --- |
| `POSTGRES_USER` | `goldbank` | DB user |
| `POSTGRES_PASSWORD` | `goldbank_dev_password` | DB password |
| `POSTGRES_DB` | `goldbank` | DB name |
| `ASPNETCORE_ENVIRONMENT` | `Development` | .NET env switch |
| `GATEWAY_GRPC_PORT` | `5000` | host gRPC port |
| `GATEWAY_HTTP_PORT` | `5001` | host REST port |
| `JWT_SECRET` | `dev-secret-key-change-in-production-min-32-chars!!` | HMAC key. **Rotate for prod.** |
| `JWT_ISSUER` | `goldbank-dev` | JWT issuer claim |
| `JWT_AUDIENCE` | `goldbank-api` | JWT audience claim |

## Seeding

The `GoldBank.Migrator` console app is the migration runner **and** the
demo seeder. CLI flags:

```
dotnet run                                  apply all migrations (PublicDb + GoldBankDb)
dotnet run -- --context PublicDb            PublicDbContext only
dotnet run -- --context GoldBankDb          GoldBankDbContext only
dotnet run -- --demo                        migrations + seed demo data
dotnet run -- --apply-ekub-fees             debit the Ekub monthly fee against active groups
dotnet run -- --apply-ekub-fees --period 2026-05    apply fee for a specific period
```

The seed is **idempotent** — re-running it does nothing if rows already
exist. To wipe + reseed, drop the DB:

```powershell
podman exec goldbank-postgres psql -U goldbank -d postgres -c `
  "DROP DATABASE IF EXISTS goldbank; CREATE DATABASE goldbank;"
cd server\GoldBank.Migrator
dotnet run -- --demo
```

### Demo accounts

| Role | Username / phone | Password / PIN |
| --- | --- | --- |
| Back-office admin (bank-client) | `admin` | `Admin@1234` |
| KYC officer | `kyc` | `Kyc@1234` |
| Fraud analyst | `fraud` | `Fraud@1234` |
| Customer support | `support` | `Support@1234` |
| Loan officer | `loans` | `Loans@1234` |
| Compliance | `compliance` | `Compliance@1234` |
| Branch manager | `branch` | `Branch@1234` |
| Branch teller (bank-teller) | `teller` | `teller` |
| Branch manager (bank-teller) | `branch` | `branch` |
| Customer (chairman) | `+263770003287` | `1234` |
| Customer (treasurer) | `+263775304489` | `1234` |
| Customer (secretary) | `+263771882741` | `1234` |
| Customer (member) | `+263774538185` | `1234` |
| Customer (member, has gold coins) | `+263771000001` | `1234` |

## Auxiliary seeds (SQL scripts)

The demo seeder runs in C# but a few demo bits live in raw SQL under
`scripts/`. These are one-off — run them via:

```powershell
Get-Content scripts\<name>.sql | podman exec -i goldbank-postgres `
  psql -U goldbank -d goldbank
```

| Script | What it does |
| --- | --- |
| `seed-gold-coins.sql` | Inserts 1 deposit house + 5 gold-coin assets for John Moyo (`+263771000001`) |
| `seed-asset-valuations.sql` | Inserts 10 prior valuations (initial + revalued) for the 5 coins so the History tab shows % change |
| `age-asset-valuations.sql` | Backdates `last_valuation_date` on 2 assets so the Valuation Queue has overdue items |
| `check-ekub-mig.sql` | Diagnostic dump of Ekub group state (memberships, roles, contributions, pot) |

## Scheduled jobs

### Ekub monthly fee

The bank's per-product fee on active Ekub groups. Idempotent: at most
one `ekub_fees` row per `(group, period)`.

**Manual run**:
```powershell
cd server\GoldBank.Migrator
dotnet run -- --apply-ekub-fees
```

**Scheduled run** (Windows): register a Task Scheduler entry to run on
the 1st of every month at 02:00 local time:

```powershell
.\scripts\register-ekub-fees-task.ps1            # register
.\scripts\register-ekub-fees-task.ps1 -RunNow    # register + trigger immediately
.\scripts\register-ekub-fees-task.ps1 -Unregister # remove
```

The task invokes `dotnet run --project server/GoldBank.Migrator --
--apply-ekub-fees` against the local DB. Because it has to reach
`localhost:5432`, this can't be a cloud-hosted scheduled agent; it has
to run on the host that has the DB.

## Diagnostics

### Container status

```powershell
podman ps                                          # running
podman ps -a --format "{{.Names}} | {{.Status}}"   # all
podman logs goldbank-gateway --tail 50             # gateway logs
podman logs goldbank-postgres --tail 20            # DB logs
```

### Health endpoints

| Service | Health |
| --- | --- |
| Gateway | container has a `HEALTHCHECK` running `dotnet --info`. Status visible in `podman ps`. |
| Postgres | `podman exec goldbank-postgres pg_isready -U goldbank` |
| Redis | `podman exec goldbank-redis redis-cli ping` |

### Common failure modes

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| `Cannot connect to Podman socket: 127.0.0.1:5346` | Podman machine VM is down even though `podman machine list` says running | `wsl --shutdown` then `podman machine start` |
| `bind: address already in use` on 6379 (or other) | Orphaned `wslrelay.exe` holding the port | `Get-NetTCPConnection -LocalPort 6379` → find PID → `Stop-Process -Id <pid> -Force` |
| `HTTP_1_1_REQUIRED` / `Received Goaway` from mobile or web | Gateway container has exited; clients hit nothing on :5000/:5001 | Check `podman ps`; restart container |
| Mobile build fails on `classes.jar in use by another process` | VS Code Kotlin LSP (`fwcd.kotlin`) holds the file | `Get-CimInstance Win32_Process -Filter "Name='java.exe'"` → find one with `kotlin*Server` → kill it |
| `Vite` won't start, `Could not determine Node.js install directory` | Bash shell quirk on Windows | Use `pwsh` instead, or `$env:OPENSSL_CONF=''` before `npm run dev` |
| `BC8D0000:error:07000065:configuration file routines:def_load_bio:missing equal sign` | A broken system-wide `OPENSSL_CONF` env var points at a non-OpenSSL file | `$env:OPENSSL_CONF = ''` in the shell before any node command |
| Bank-client gets 403 on a customer's data | The customer's `tenant_id` doesn't match the staff JWT tenant | `UPDATE bank.customers SET tenant_id='goldbank' WHERE phone='+263…'` (also for accounts) |
| `Insufficient pot balance` when applying for an Ekub loan | Server-side pre-flight rejection (correct) | Check `GetGroupDetail.potBalance` vs principal; deduct in-flight pending loans |
| Confirm-and-disburse silently fails on the mobile group detail screen | Error not surfaced; status response code was 9 (FailedPrecondition) | Look at logcat; fixed in mobile by surfacing `state.error` as a banner |

### Logs

The gateway uses Serilog with structured logs to stdout. To follow:

```powershell
podman logs goldbank-gateway --follow
```

Adjust verbosity with `Logging__LogLevel__Default` env var (default
`Information`). For SQL traces add
`Logging__LogLevel__Microsoft.EntityFrameworkCore=Information`.

## Backups (operational, not configured for dev)

Postgres data lives in a podman volume:

```powershell
podman volume inspect GoldBank_postgres-data
```

For prod the recommended pattern is `pg_dump` on a schedule + S3
upload. The repo doesn't currently include a backup container.

## Building images

```powershell
# Gateway only (most common)
cd c:\Users\wmapu\Projects\GoldBank
podman build -f server\GoldBank.Gateway\Dockerfile -t localhost/goldbank_gateway:latest .

# Switch
podman build -f switch\Containerfile -t localhost/goldbank_switch:latest ./switch

# Admin (Blazor)
podman build -f admin\GoldBank.Admin\Dockerfile -t localhost/goldbank_admin:latest .

# Notifications
podman build -f server\GoldBank.Notifications\Dockerfile -t localhost/goldbank_notifications:latest .
```

After rebuilding an image, restart the container so it picks up the
new image:

```powershell
podman stop goldbank-gateway
podman rm goldbank-gateway
python -m podman_compose --profile core up -d gateway
```

For frontend changes (bank-client, bank-teller), Vite hot-reloads —
no rebuild needed.

For mobile changes:

```powershell
cd mobile
.\gradlew :androidApp:assembleDebug
# install + relaunch
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r androidApp\build\outputs\apk\debug\androidApp-debug.apk
& $adb shell am force-stop com.goldbank.app
& $adb shell monkey -p com.goldbank.app -c android.intent.category.LAUNCHER 1
```

## CI

A `Jenkinsfile` and a `.gitlab-ci.yml` exist at the root but neither is
wired to a runner in this checkout. Both target a standard
build-test-package pipeline against .NET 10. CI work is pending.

## Production deployment

Not in scope of this document — the repo is dev-tuned. To deploy:

1. Replace `JWT_SECRET` with a real 32+ char secret.
2. Set `Postgres` credentials to non-default values; rotate.
3. Terminate TLS at a load balancer; expose gateway gRPC over TLS or
   private network only.
4. Replace bank-client's `SEED_ACCOUNTS` stub with a real login flow.
5. Add real authentication to `AdminApiController` (`[Authorize]` +
   admin JWT issuer).
6. Configure Ollama with a production-class model and GPU.
7. Replace the in-DB KYC document storage with S3/MinIO.
8. Wire CI to build images + push to a registry.

These are tracked as standing offers in the system docs but none are
done in the repo as of `4be4dd7`.
