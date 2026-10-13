FROM debian:trixie-slim
COPY artifacts/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY artifacts/speech/ /
WORKDIR /app
COPY artifacts/ai/ ./
COPY artifacts/native/ ./native/
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_EnableDiagnostics=0 LD_LIBRARY_PATH=/app/native:/usr/lib/x86_64-linux-gnu/pulseaudio ESPEAK_DATA_PATH=/usr/lib/x86_64-linux-gnu/espeak-ng-data
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["./AccessReadAI.Ai"]
