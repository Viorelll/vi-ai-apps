FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet restore src/DocuAI.Api/DocuAI.Api.csproj
RUN dotnet publish src/DocuAI.Api/DocuAI.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","DocuAI.Api.dll"]
