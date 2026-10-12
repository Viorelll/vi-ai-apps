FROM node:24.19.0-bookworm-slim@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df AS build
ADD --checksum=sha256:137268c8ad939c064ff1ee2a6fdf0899d8725377114ea012fbd1ad5fa2550418 https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz /tmp/sdk.tar.gz
RUN mkdir -p /usr/share/dotnet && tar -xzf /tmp/sdk.tar.gz -C /usr/share/dotnet && rm /tmp/sdk.tar.gz
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src
COPY . .
RUN dotnet restore src/VideoStudioAI.Api/VideoStudioAI.Api.csproj --locked-mode
RUN dotnet publish src/VideoStudioAI.Api/VideoStudioAI.Api.csproj -c Release --no-restore -o /out
FROM node:24.19.0-bookworm-slim@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df
ARG HTTPS_PROXY
RUN sed -i 's|http://deb.debian.org|https://deb.debian.org|g' /etc/apt/sources.list.d/debian.sources \
 && apt-get -o Acquire::https::Proxy="$HTTPS_PROXY" update \
 && apt-get -o Acquire::https::Proxy="$HTTPS_PROXY" install -y --no-install-recommends ffmpeg curl \
 && rm -rf /var/lib/apt/lists/* \
 && useradd --uid 10001 --create-home app
COPY --from=build /usr/share/dotnet/dotnet /usr/share/dotnet/dotnet
COPY --from=build /usr/share/dotnet/host /usr/share/dotnet/host
COPY --from=build /usr/share/dotnet/shared /usr/share/dotnet/shared
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /out .
USER app
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s CMD curl -fsS http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "VideoStudioAI.Api.dll"]
