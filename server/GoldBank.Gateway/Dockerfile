# ============================================
# GoldBank.Gateway - ASP.NET Web API + gRPC
# Build context: repository root
# ============================================

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and build configuration
COPY ["GoldBank.slnx", "."]
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]

# Copy csproj files for restore
COPY ["server/GoldBank.Gateway/GoldBank.Gateway.csproj", "server/GoldBank.Gateway/"]
COPY ["server/GoldBank.Core/GoldBank.Core.csproj", "server/GoldBank.Core/"]
COPY ["server/GoldBank.Protos/GoldBank.Protos.csproj", "server/GoldBank.Protos/"]
COPY ["server/GoldBank.SharedKernel/GoldBank.SharedKernel.csproj", "server/GoldBank.SharedKernel/"]
COPY ["server/GoldBank.Reporting/GoldBank.Reporting.csproj", "server/GoldBank.Reporting/"]
COPY ["server/GoldBank.Notifications/GoldBank.Notifications.csproj", "server/GoldBank.Notifications/"]

# Restore dependencies
RUN dotnet restore "server/GoldBank.Gateway/GoldBank.Gateway.csproj"

# Copy all source files
COPY . .

# Publish the application
WORKDIR /src/server/GoldBank.Gateway
RUN dotnet publish "GoldBank.Gateway.csproj" -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

EXPOSE 5000

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "GoldBank.Gateway.dll"]
