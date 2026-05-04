# GoldBank.Migrator - EF Core migration runner
# Build context: repository root
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["GoldBank.slnx", "."]
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]

COPY ["server/GoldBank.Migrator/GoldBank.Migrator.csproj", "server/GoldBank.Migrator/"]
COPY ["server/GoldBank.Core/GoldBank.Core.csproj", "server/GoldBank.Core/"]
COPY ["server/GoldBank.Protos/GoldBank.Protos.csproj", "server/GoldBank.Protos/"]
COPY ["server/GoldBank.SharedKernel/GoldBank.SharedKernel.csproj", "server/GoldBank.SharedKernel/"]

RUN dotnet restore "server/GoldBank.Migrator/GoldBank.Migrator.csproj"

COPY . .

WORKDIR /src/server/GoldBank.Migrator
RUN dotnet publish "GoldBank.Migrator.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GoldBank.Migrator.dll"]
