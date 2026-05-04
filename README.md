# GoldBank - White-Label Banking Platform

A white-label banking platform targeting the unbanked population in Southern Africa, built with .NET 10 using a Modular Monolith with Satellite Services architecture.

## Architecture

- **Modular Monolith** (Core Banking): Accounts, Payments, Transfers, Agents, BillPay, Merchants
- **Satellite Services**: Switching (ISO 8583/20022), Terminal Manager (MQTT), HSM (PKCS#11)
- **Communication**: gRPC with Protocol Buffers for all client-server and server-server communication
- **Messaging**: Wolverine + MQTT embedded broker
- **Database**: PostgreSQL 18 with schema-per-tenant multi-tenancy
- **Cache**: Redis

## Project Structure

```
GoldBank.slnx                          # .NET 10 XML solution file
├── server/                           # Core server-side projects
│   ├── GoldBank.Gateway/              # API Gateway (gRPC entry point)
│   ├── GoldBank.Core/                 # Modular Monolith (business logic)
│   ├── GoldBank.SharedKernel/         # Shared domain primitives
│   ├── GoldBank.Protos/               # Protobuf contracts
│   ├── GoldBank.Reporting/            # Reporting module
│   └── GoldBank.Notifications/        # Push & SMS notifications
├── switch/                           # Payment switching satellite
│   └── GoldBank.Switching/            # ISO 8583/20022 adapters
├── terminal/                         # Terminal management satellite
│   └── GoldBank.TerminalManager/      # MQTT-based POS management
├── hsm/                              # Hardware Security Module satellite
│   └── GoldBank.HSM/                  # PKCS#11 cryptographic operations
├── admin/                            # Admin portal
│   └── GoldBank.Admin/                # Blazor Server with MudBlazor
├── mobile/                           # Mobile client (future KMP)
└── tests/                            # Test projects
    ├── GoldBank.Tests/                # Unit tests (xUnit)
    └── GoldBank.IntegrationTests/     # Integration tests (TestContainers)
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) (10.0.100+)
- Docker & Docker Compose (for development services)
- PostgreSQL 18 (via Docker or local)
- Redis (via Docker or local)

## Quick Start

```bash
# Restore NuGet packages
dotnet restore GoldBank.slnx

# Build the solution
dotnet build GoldBank.slnx

# Run unit tests
dotnet test tests/GoldBank.Tests/

# Run the API Gateway
dotnet run --project server/GoldBank.Gateway/
```

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 10 / C# |
| API | gRPC + Protocol Buffers |
| Database | PostgreSQL 18 |
| ORM | Entity Framework Core 10 |
| Cache | Redis (StackExchange.Redis) |
| Messaging | Wolverine + MQTT |
| Admin UI | Blazor Server + MudBlazor |
| Testing | xUnit, NSubstitute, FluentAssertions, TestContainers |
| Logging | Serilog + Elasticsearch |
| Metrics | Prometheus + Grafana |
| HSM | PKCS#11 interop |
| Switching | ISO 8583 (TCP/IP), ISO 20022 (MQ/API) |

## Multi-Tenancy

GoldBank uses a schema-per-tenant model in PostgreSQL. Each tenant (bank) gets its own database schema, ensuring complete data isolation while sharing the application runtime.

## License

Proprietary - All rights reserved.
