# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY global.json Directory.Build.props EvalAI.slnx ./
COPY src/ src/
# Cloud override provides the public CA bundle; never disable TLS verification.
RUN --mount=type=secret,id=egress_ca,target=/run/egress-ca.pem,required=false if [ -f /run/egress-ca.pem ]; then cp /run/egress-ca.pem /usr/local/share/ca-certificates/egress.crt; update-ca-certificates; fi
RUN dotnet restore src/EvalAI.Ai/EvalAI.Ai.csproj --locked-mode
RUN dotnet publish src/EvalAI.Ai/EvalAI.Ai.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9
WORKDIR /app
COPY --from=build /out/ .
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","EvalAI.Ai.dll"]
