# UniBank.Migrator - EF Core migration runner
# Build context: repository root
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["UniBank.slnx", "."]
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]

COPY ["server/UniBank.Migrator/UniBank.Migrator.csproj", "server/UniBank.Migrator/"]
COPY ["server/UniBank.Core/UniBank.Core.csproj", "server/UniBank.Core/"]
COPY ["server/UniBank.Protos/UniBank.Protos.csproj", "server/UniBank.Protos/"]
COPY ["server/UniBank.SharedKernel/UniBank.SharedKernel.csproj", "server/UniBank.SharedKernel/"]

RUN dotnet restore "server/UniBank.Migrator/UniBank.Migrator.csproj"

COPY . .

WORKDIR /src/server/UniBank.Migrator
RUN dotnet publish "UniBank.Migrator.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UniBank.Migrator.dll"]
