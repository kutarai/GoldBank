# ============================================
# GoldBank.HSM - Worker Service (Hardware Security Module)
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
COPY ["hsm/GoldBank.HSM/GoldBank.HSM.csproj", "hsm/GoldBank.HSM/"]
COPY ["server/GoldBank.Protos/GoldBank.Protos.csproj", "server/GoldBank.Protos/"]
COPY ["server/GoldBank.SharedKernel/GoldBank.SharedKernel.csproj", "server/GoldBank.SharedKernel/"]

# Restore dependencies
RUN dotnet restore "hsm/GoldBank.HSM/GoldBank.HSM.csproj"

# Copy all source files
COPY . .

# Publish the application
WORKDIR /src/hsm/GoldBank.HSM
RUN dotnet publish "GoldBank.HSM.csproj" -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

EXPOSE 5005

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:5005/health || exit 1

ENTRYPOINT ["dotnet", "GoldBank.HSM.dll"]
