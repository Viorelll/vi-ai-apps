# syntax=docker/dockerfile:1
# Use Microsoft's official SDK archive with its SHA-512 from release-metadata/10.0/releases.json.
FROM ubuntu:24.04 AS build
ARG SDK_SHA512=f78dbac30c9af2230d67ff5c224de3a5dbf63f8a78d1c206594dedb80e6909d2cc8a9d865d5105c72c2fd2aa266fc0c6c77dedac60408cbccf272b116bd11b07
RUN --mount=type=secret,id=egress_ca \
 apt_options=""; mkdir -p /etc/ssl/certs; if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; chmod 644 /etc/ssl/certs/ca-certificates.crt; apt_options="-o Acquire::https::CaInfo=/etc/ssl/certs/ca-certificates.crt"; fi; \
 find /etc/apt/sources.list.d -type f -exec sed -i 's|http://|https://|g' {} + && apt-get $apt_options update && apt-get $apt_options install -y --no-install-recommends ca-certificates curl libicu74 libssl3t64 libstdc++6 zlib1g && rm -rf /var/lib/apt/lists/*
RUN --mount=type=secret,id=egress_ca \
 if [ -f /run/secrets/egress_ca ]; then export CURL_CA_BUNDLE=/run/secrets/egress_ca; fi; \
 curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-linux-x64.tar.gz -o /tmp/sdk.tar.gz && \
 echo "$SDK_SHA512  /tmp/sdk.tar.gz" | sha512sum -c - && mkdir -p /opt/dotnet && tar -xzf /tmp/sdk.tar.gz -C /opt/dotnet && rm /tmp/sdk.tar.gz
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source
COPY . .
RUN --mount=type=secret,id=egress_ca if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /usr/local/share/ca-certificates/egress.crt; update-ca-certificates; fi; \
 dotnet restore src/VideoSearchAI.Ai/VideoSearchAI.Ai.csproj --locked-mode && dotnet publish src/VideoSearchAI.Ai/VideoSearchAI.Ai.csproj -c Release --no-restore -o /out
FROM ubuntu:24.04
RUN --mount=type=secret,id=egress_ca \
 apt_options=""; mkdir -p /etc/ssl/certs; if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; chmod 644 /etc/ssl/certs/ca-certificates.crt; apt_options="-o Acquire::https::CaInfo=/etc/ssl/certs/ca-certificates.crt"; fi; \
 find /etc/apt/sources.list.d -type f -exec sed -i 's|http://|https://|g' {} + && apt-get $apt_options update && apt-get $apt_options install -y --no-install-recommends ca-certificates curl libicu74 libssl3t64 libstdc++6 zlib1g && rm -rf /var/lib/apt/lists/*
COPY --from=build /opt/dotnet/dotnet /opt/dotnet/dotnet
COPY --from=build /opt/dotnet/host /opt/dotnet/host
COPY --from=build /opt/dotnet/shared /opt/dotnet/shared
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH ASPNETCORE_HTTP_PORTS=8080 DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
COPY --from=build /out .
RUN chmod a+r /app/appsettings.json
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["dotnet","VideoSearchAI.Ai.dll"]
