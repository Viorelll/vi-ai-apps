# syntax=docker/dockerfile:1
# Verified Microsoft SDK archive avoids MCR blob restrictions on cloud runners.
FROM debian:bookworm-slim AS build
RUN --mount=type=secret,id=proxy_ca,target=/usr/local/share/ca-certificates/cloud-proxy.crt,required=false \
    apt-get update && apt-get install -y --no-install-recommends ca-certificates curl libicu72 libssl3 zlib1g libstdc++6 && update-ca-certificates && rm -rf /var/lib/apt/lists/*
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o /tmp/sdk.tar.gz && \
    echo '51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  /tmp/sdk.tar.gz' | sha512sum -c - && \
    mkdir -p /usr/share/dotnet && tar -xzf /tmp/sdk.tar.gz -C /usr/share/dotnet && rm /tmp/sdk.tar.gz
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/ImageAI.Domain src/ImageAI.Domain
COPY src/ImageAI.Contracts src/ImageAI.Contracts
COPY src/ImageAI.Database src/ImageAI.Database
COPY src/ImageAI.Infrastructure src/ImageAI.Infrastructure
COPY src/ImageAI.Services src/ImageAI.Services
COPY src/ImageAI.Api src/ImageAI.Api
COPY src/ImageAI.Ai src/ImageAI.Ai
ARG PROJECT=Api
RUN dotnet restore src/ImageAI.${PROJECT}/ImageAI.${PROJECT}.csproj --locked-mode && \
    dotnet publish src/ImageAI.${PROJECT}/ImageAI.${PROJECT}.csproj -c Release --no-restore -o /out && \
    printf '#!/bin/sh\nexec dotnet ImageAI.%s.dll "$@"\n' "$PROJECT" > /out/entrypoint.sh && chmod +x /out/entrypoint.sh
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu72 libssl3 zlib1g libstdc++6 curl && rm -rf /var/lib/apt/lists/*
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /usr/share/dotnet/dotnet /usr/share/dotnet/dotnet
COPY --from=build /usr/share/dotnet/host /usr/share/dotnet/host
COPY --from=build /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
WORKDIR /app
COPY --from=build --chown=10001:10001 /out .
USER 10001:10001
ENTRYPOINT ["/app/entrypoint.sh"]
