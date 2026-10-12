FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/InvoiceAI.Api/InvoiceAI.Api.csproj --locked-mode
RUN dotnet publish src/InvoiceAI.Api/InvoiceAI.Api.csproj -c Release --no-restore -o /out
RUN chmod -R a+rX /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
COPY --from=build /out/ .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","InvoiceAI.Api.dll"]
