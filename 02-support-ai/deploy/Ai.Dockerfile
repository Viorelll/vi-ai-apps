FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/ ./src/
RUN --mount=type=secret,id=proxy_ca,target=/usr/local/share/ca-certificates/cloud-proxy.crt,required=false update-ca-certificates && dotnet restore src/SupportAI.Ai/SupportAI.Ai.csproj --locked-mode && dotnet publish src/SupportAI.Ai/SupportAI.Ai.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","SupportAI.Ai.dll"]
