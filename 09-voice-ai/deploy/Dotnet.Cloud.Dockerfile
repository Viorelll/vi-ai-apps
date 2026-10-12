# syntax=docker/dockerfile:1
# Official Microsoft SDK archive fallback for cloud runners where MCR layer blobs return 403.
FROM debian:bookworm-slim AS build
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl libicu72 libssl3 zlib1g libstdc++6 && rm -rf /var/lib/apt/lists/*
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN --mount=type=secret,id=egress_ca \
    mkdir -p /etc/ssl/certs && if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi && \
    curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o /tmp/sdk.tar.gz && \
    echo '51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  /tmp/sdk.tar.gz' | sha512sum -c - && \
    mkdir -p /usr/share/dotnet && tar -xzf /tmp/sdk.tar.gz -C /usr/share/dotnet && rm /tmp/sdk.tar.gz
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src ./src
ARG PROJECT=Api
RUN --mount=type=secret,id=egress_ca \
    if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi && \
    dotnet restore src/VoiceAI.${PROJECT}/VoiceAI.${PROJECT}.csproj --locked-mode && \
    dotnet publish src/VoiceAI.${PROJECT}/VoiceAI.${PROJECT}.csproj -c Release --no-restore -o /out
FROM debian:bookworm-slim
ARG PROJECT=Api
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH ASPNETCORE_HTTP_PORTS=8080
RUN --mount=type=secret,id=egress_ca \
    mkdir -p /etc/ssl/certs && if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi && \
    apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu72 libssl3 zlib1g libstdc++6 \
    && if [ "$PROJECT" = Api ]; then apt-get install -y --no-install-recommends curl; else apt-get install -y --no-install-recommends ffmpeg; fi \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /usr/share/dotnet/dotnet /usr/share/dotnet/dotnet
COPY --from=build /usr/share/dotnet/host /usr/share/dotnet/host
COPY --from=build /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
WORKDIR /app
COPY --from=build --chown=10001:10001 /out .
USER 10001:10001
ENTRYPOINT ["dotnet", "VoiceAI.Api.dll"]
