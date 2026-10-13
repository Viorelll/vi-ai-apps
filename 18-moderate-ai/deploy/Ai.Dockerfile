FROM golang:1.25-bookworm AS probe
WORKDIR /src
COPY deploy/healthprobe.go .
RUN CGO_ENABLED=0 go build -o /healthprobe healthprobe.go
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ModerateAI.Ai/ModerateAI.Ai.csproj --locked-mode && dotnet publish src/ModerateAI.Ai/ModerateAI.Ai.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
COPY --from=build --chown=1654:1654 /out .
COPY --from=probe /healthprobe /usr/local/bin/healthprobe
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet","ModerateAI.Ai.dll"]
