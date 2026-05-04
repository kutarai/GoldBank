# STORY-001: Solution Scaffolding & Project Structure

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want a properly structured .NET 10 solution with all projects scaffolded,
So that the team can begin parallel development immediately.

---

## Description

### Background
GoldBank is a white-label banking platform targeting the unbanked population in Southern Africa. Before any feature development can begin, the team needs a well-organized solution structure that reflects the Modular Monolith with Satellite Services architecture. This story establishes the foundational codebase that every subsequent story depends on.

The architecture follows a Modular Monolith pattern for the Core Banking domain (accounts, payments, transfers, agents, bill pay, merchants) with Satellite Services for specialized concerns (switching/ISO 8583, terminal management, HSM/cryptography). The API Gateway serves as the single entry point for all gRPC traffic, and shared concerns live in a SharedKernel project.

### Scope

**In scope:**
- Creation of `GoldBank.sln` solution file with all project references
- Scaffolding of all 11 projects with correct project types and dependencies
- `global.json` pinning .NET 10 SDK version
- `Directory.Build.props` for shared MSBuild properties (versioning, nullable, implicit usings)
- `.editorconfig` for consistent code formatting across the team
- NuGet package references for all required dependencies
- Folder structure within each project following Clean Architecture / DDD patterns
- Basic `README.md` at solution root with build instructions
- `.gitignore` for .NET projects

**Out of scope:**
- Actual implementation of any business logic
- Database migrations or schema creation (STORY-003)
- Docker configuration (STORY-002)
- CI/CD pipeline (STORY-006)
- Proto file definitions (STORY-004)

### User Flow
1. Developer clones the repository
2. Developer opens `GoldBank.sln` in IDE (Visual Studio, Rider, or VS Code)
3. Developer runs `dotnet restore` to pull NuGet packages
4. Developer runs `dotnet build` and all projects compile successfully
5. Developer can navigate the project structure and understand the architecture
6. Developer begins implementing their assigned story in the correct project

---

## Acceptance Criteria

- [ ] `GoldBank.sln` exists at the repository root and contains references to all 11 projects
- [ ] All projects are created with correct project types:
  - `GoldBank.Gateway` - ASP.NET Core gRPC host (`web` template)
  - `GoldBank.Core` - Class library with modular folder structure
  - `GoldBank.Switching` - ASP.NET Core worker service
  - `GoldBank.TerminalManager` - ASP.NET Core worker service
  - `GoldBank.HSM` - ASP.NET Core worker service
  - `GoldBank.Admin` - Blazor Server application
  - `GoldBank.Reporting` - Class library
  - `GoldBank.Notifications` - ASP.NET Core worker service
  - `GoldBank.Protos` - Class library for protobuf contracts
  - `GoldBank.SharedKernel` - Class library
  - `GoldBank.Tests` - xUnit test project
- [ ] `global.json` pins .NET 10 SDK (version `10.0.100` or latest preview)
- [ ] `Directory.Build.props` configures: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, shared version properties
- [ ] `.editorconfig` enforces C# coding conventions (indentation, naming, etc.)
- [ ] NuGet packages are referenced in correct projects (see Technical Notes)
- [ ] `dotnet restore` completes without errors
- [ ] `dotnet build` completes without errors or warnings
- [ ] Solution README documents the project structure and build steps
- [ ] `.gitignore` excludes bin/, obj/, .vs/, .idea/, *.user files

---

## Technical Notes

### Components

**Solution Root Structure:**
```
GoldBank/
  GoldBank.sln
  global.json
  Directory.Build.props
  .editorconfig
  .gitignore
  README.md
  src/
    GoldBank.Gateway/
    GoldBank.Core/
    GoldBank.Switching/
    GoldBank.TerminalManager/
    GoldBank.HSM/
    GoldBank.Admin/
    GoldBank.Reporting/
    GoldBank.Notifications/
    GoldBank.Protos/
    GoldBank.SharedKernel/
  tests/
    GoldBank.Tests/
    GoldBank.IntegrationTests/
```

**GoldBank.Gateway Project Structure:**
```
GoldBank.Gateway/
  Program.cs
  appsettings.json
  appsettings.Development.json
  Interceptors/
    AuthInterceptor.cs
    TenantInterceptor.cs
    RateLimitInterceptor.cs
    LoggingInterceptor.cs
  Services/
  Configuration/
```

**GoldBank.Core Project Structure (Modular Monolith):**
```
GoldBank.Core/
  Modules/
    Accounts/
      Domain/
        Entities/
        ValueObjects/
        Events/
      Application/
        Commands/
        Queries/
        Handlers/
        Validators/
      Infrastructure/
        Persistence/
        Services/
      Grpc/
        AccountGrpcService.cs
    Payments/
      Domain/ Application/ Infrastructure/ Grpc/
    Transfers/
      Domain/ Application/ Infrastructure/ Grpc/
    Agents/
      Domain/ Application/ Infrastructure/ Grpc/
    BillPay/
      Domain/ Application/ Infrastructure/ Grpc/
    Merchants/
      Domain/ Application/ Infrastructure/ Grpc/
  Common/
    Persistence/
      TenantDbContext.cs
      PublicDbContext.cs
    Middleware/
    Extensions/
```

**GoldBank.SharedKernel Project Structure:**
```
GoldBank.SharedKernel/
  Domain/
    BaseEntity.cs
    AggregateRoot.cs
    IAuditableEntity.cs
    ISoftDeletable.cs
    ValueObject.cs
  Events/
    DomainEvent.cs
    IDomainEventHandler.cs
    AccountCreated.cs
    TransactionCompleted.cs
    TransactionFailed.cs
    KYCApproved.cs
    KYCRejected.cs
    FraudAlertRaised.cs
    LowFloatAlert.cs
    TerminalStatusChanged.cs
  MultiTenancy/
    ITenantProvider.cs
    TenantInfo.cs
  Results/
    Result.cs
    Error.cs
  Constants/
    StatusCodes.cs
    ErrorMessages.cs
```

### NuGet Package References

**GoldBank.Gateway:**
- `Grpc.AspNetCore` (latest)
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `StackExchange.Redis`
- `Serilog.AspNetCore`
- `Serilog.Sinks.Elasticsearch`
- `prometheus-net.AspNetCore`

**GoldBank.Core:**
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Wolverine`
- `Wolverine.EntityFrameworkCore`
- `FluentValidation`
- `Grpc.AspNetCore`
- `StackExchange.Redis`
- `Serilog.AspNetCore`

**GoldBank.Switching:**
- `Grpc.AspNetCore`
- `Serilog.AspNetCore`

**GoldBank.TerminalManager:**
- `MQTTnet` (v4+)
- `Grpc.AspNetCore`
- `Serilog.AspNetCore`

**GoldBank.HSM:**
- `Grpc.AspNetCore`
- `Serilog.AspNetCore`
- (PKCS#11 interop will be added when HSM stories are implemented)

**GoldBank.Admin:**
- `Microsoft.AspNetCore.Components.Web`
- `MudBlazor` or `Radzen.Blazor`
- `Grpc.Net.Client`
- `Serilog.AspNetCore`

**GoldBank.Protos:**
- `Grpc.Tools`
- `Google.Protobuf`

**GoldBank.SharedKernel:**
- No external dependencies (pure domain)

**GoldBank.Tests:**
- `xunit`
- `xunit.runner.visualstudio`
- `Moq` or `NSubstitute`
- `FluentAssertions`
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`

### API / gRPC Endpoints
Not applicable for this story. Proto definitions are handled in STORY-004.

### Database Changes
Not applicable for this story. Database setup is handled in STORY-003.

### Security Considerations
- Ensure `.gitignore` excludes any files that might contain secrets (`appsettings.*.json` should have placeholders, not real values)
- `appsettings.Development.json` should use environment variables or user-secrets for sensitive configuration
- Enable `<TreatWarningsAsErrors>` to catch potential security-related warnings early

### Edge Cases
- Developers on different OS platforms (Windows, macOS, Linux) must be able to build successfully
- `global.json` must handle SDK version rollforward policy correctly (`"rollForward": "latestFeature"`)
- If .NET 10 is still in preview at project start, document the specific preview version required
- Ensure project references do not create circular dependencies

### Implementation Guidance

**global.json:**
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

**Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <VersionPrefix>0.1.0</VersionPrefix>
    <Authors>GoldBank Team</Authors>
    <Company>GoldBank</Company>
  </PropertyGroup>
</Project>
```

**Project Dependency Graph:**
```
GoldBank.Gateway        --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Core           --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Switching      --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.TerminalManager --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.HSM            --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Admin          --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Reporting      --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Notifications  --> GoldBank.Protos, GoldBank.SharedKernel
GoldBank.Protos         --> (none)
GoldBank.SharedKernel   --> (none)
GoldBank.Tests          --> all src projects
```

---

## Dependencies

**Prerequisite Stories:**
- None (this is the first story)

**Blocked Stories:**
- STORY-002: Docker Compose Development Environment
- STORY-004: gRPC Proto Definitions & Shared Contracts
- STORY-006: CI/CD Pipeline Setup
- STORY-007: Wolverine Messaging & MQTT Broker Configuration
- All subsequent stories depend on this foundational structure

**External Dependencies:**
- .NET 10 SDK must be available (preview or GA)
- NuGet.org must be accessible for package restore

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) -- for this story, a basic build verification test
- [ ] Integration tests passing -- N/A for scaffolding
- [ ] Code reviewed and approved
- [ ] Documentation updated (README with build instructions)
- [ ] Acceptance criteria validated (solution builds, all projects present)
- [ ] Deployed to staging -- N/A for scaffolding, but CI pipeline should pass

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
