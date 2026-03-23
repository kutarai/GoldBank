# ============================================
# UniBank.Gateway - ASP.NET Web API + gRPC
# Build context: repository root
# ============================================

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and build configuration
COPY ["UniBank.slnx", "."]
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]

# Copy csproj files for restore
COPY ["server/UniBank.Gateway/UniBank.Gateway.csproj", "server/UniBank.Gateway/"]
COPY ["server/UniBank.Core/UniBank.Core.csproj", "server/UniBank.Core/"]
COPY ["server/UniBank.Protos/UniBank.Protos.csproj", "server/UniBank.Protos/"]
COPY ["server/UniBank.SharedKernel/UniBank.SharedKernel.csproj", "server/UniBank.SharedKernel/"]
COPY ["server/UniBank.Reporting/UniBank.Reporting.csproj", "server/UniBank.Reporting/"]
COPY ["server/UniBank.Notifications/UniBank.Notifications.csproj", "server/UniBank.Notifications/"]

# Restore dependencies
RUN dotnet restore "server/UniBank.Gateway/UniBank.Gateway.csproj"

# Copy all source files
COPY . .

# Publish the application
WORKDIR /src/server/UniBank.Gateway
RUN dotnet publish "UniBank.Gateway.csproj" -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

EXPOSE 5000

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "UniBank.Gateway.dll"]
