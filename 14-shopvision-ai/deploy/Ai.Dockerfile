# syntax=docker/dockerfile:1
# Official Microsoft SDK archive; SHA-512 from Microsoft's 10.0 release metadata.
# This avoids MCR CDN dependencies in restricted cloud build environments.
FROM debian:bookworm-slim AS sdk
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl libicu72 libssl3 && rm -rf /var/lib/apt/lists/*
RUN --mount=type=secret,id=proxy_ca,required=false if [ -f /run/secrets/proxy_ca ]; then cp /run/secrets/proxy_ca /usr/local/share/ca-certificates/build-proxy.crt; update-ca-certificates; fi
RUN curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o /tmp/sdk.tar.gz && echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  /tmp/sdk.tar.gz" | sha512sum -c - && mkdir /dotnet && tar -xzf /tmp/sdk.tar.gz -C /dotnet && rm /tmp/sdk.tar.gz
ENV DOTNET_ROOT=/dotnet PATH=/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source
COPY . .
RUN dotnet restore src/ShopVisionAI.Ai/ShopVisionAI.Ai.csproj --locked-mode -r linux-x64 && dotnet publish src/ShopVisionAI.Ai/ShopVisionAI.Ai.csproj -c Release --no-restore -r linux-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -o /app
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu72 libssl3 && rm -rf /var/lib/apt/lists/* && useradd -u 10001 -m app
WORKDIR /app
COPY --from=sdk /app .
RUN chmod -R a=rX /app
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["./ShopVisionAI.Ai"]
