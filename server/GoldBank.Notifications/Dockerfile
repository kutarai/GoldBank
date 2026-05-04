# ============================================
# GoldBank.Notifications - Worker Service
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
COPY ["server/GoldBank.Notifications/GoldBank.Notifications.csproj", "server/GoldBank.Notifications/"]
COPY ["server/GoldBank.Protos/GoldBank.Protos.csproj", "server/GoldBank.Protos/"]
COPY ["server/GoldBank.SharedKernel/GoldBank.SharedKernel.csproj", "server/GoldBank.SharedKernel/"]

# Restore dependencies
RUN dotnet restore "server/GoldBank.Notifications/GoldBank.Notifications.csproj"

# Copy all source files
COPY . .

# Publish the application
WORKDIR /src/server/GoldBank.Notifications
RUN dotnet publish "GoldBank.Notifications.csproj" -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

EXPOSE 5007

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:5007/health || exit 1

ENTRYPOINT ["dotnet", "GoldBank.Notifications.dll"]
