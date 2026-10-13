# Official Microsoft SDK archive; checksum from Microsoft's 10.0 releases.json.
# Avoids reliance on registry CDN hosts blocked by some cloud sandboxes.
FROM debian:bookworm-slim AS runtime-base
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu72 libssl3 zlib1g libstdc++6 && rm -rf /var/lib/apt/lists/*
FROM runtime-base AS build
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN --mount=type=secret,id=ca,required=false if [ -f /run/secrets/ca ]; then export CURL_CA_BUNDLE=/run/secrets/ca; fi; curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o /tmp/sdk.tar.gz && echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  /tmp/sdk.tar.gz" | sha512sum -c - && mkdir /dotnet && tar -xzf /tmp/sdk.tar.gz -C /dotnet && rm /tmp/sdk.tar.gz
ENV PATH=/dotnet:$PATH DOTNET_ROOT=/dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1
ARG PROJECT=Api
WORKDIR /source
COPY Directory.Build.props global.json BuildBudgetAI.slnx ./
COPY src/BuildBudgetAI.Domain/ src/BuildBudgetAI.Domain/
COPY src/BuildBudgetAI.Database/ src/BuildBudgetAI.Database/
COPY src/BuildBudgetAI.Services/ src/BuildBudgetAI.Services/
COPY src/BuildBudgetAI.Infrastructure/ src/BuildBudgetAI.Infrastructure/
COPY src/BuildBudgetAI.Api/ src/BuildBudgetAI.Api/
COPY src/BuildBudgetAI.Ai/ src/BuildBudgetAI.Ai/
RUN --mount=type=secret,id=ca,required=false if [ -f /run/secrets/ca ]; then export SSL_CERT_FILE=/run/secrets/ca; fi; dotnet restore src/BuildBudgetAI.${PROJECT}/BuildBudgetAI.${PROJECT}.csproj --locked-mode && dotnet publish src/BuildBudgetAI.${PROJECT}/BuildBudgetAI.${PROJECT}.csproj -c Release --no-restore -o /app && chmod -R a+rX /app
FROM runtime-base
ARG PROJECT=Api
ENV APP_PROJECT=$PROJECT DOTNET_ROOT=/dotnet PATH=/dotnet:$PATH ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /dotnet/dotnet /dotnet/dotnet
COPY --from=build /dotnet/host /dotnet/host
COPY --from=build /dotnet/shared /dotnet/shared
WORKDIR /app
COPY --from=build /app .
USER 1000:1000
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet BuildBudgetAI.${APP_PROJECT}.dll \"$@\"", "--"]
