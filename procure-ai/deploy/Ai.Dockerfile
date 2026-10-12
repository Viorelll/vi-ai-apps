FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ProcureAI.slnx ./
COPY src/ ./src/
RUN dotnet restore src/ProcureAI.Ai/ProcureAI.Ai.csproj --locked-mode
RUN dotnet publish src/ProcureAI.Ai/ProcureAI.Ai.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProcureAI.Ai.dll"]
