# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src ./src
RUN --mount=type=secret,id=egress_ca if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi; dotnet restore src/ContractAI.Api/ContractAI.Api.csproj --locked-mode
RUN dotnet publish src/ContractAI.Api/ContractAI.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build --chown=${APP_UID}:${APP_UID} /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "ContractAI.Api.dll"]
