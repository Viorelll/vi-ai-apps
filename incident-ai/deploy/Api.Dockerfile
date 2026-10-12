FROM debian:trixie-slim
WORKDIR /app
COPY artifacts/api/ ./
COPY artifacts/native/ ./native/
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_EnableDiagnostics=0 LD_LIBRARY_PATH=/app/native
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["./IncidentAI.Api"]
