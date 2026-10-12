FROM debian:bookworm-slim AS filesystem
RUN mkdir -p /data /tmp/minio && chown 10001:10001 /data /tmp/minio
COPY .build/minio /usr/local/bin/minio
COPY .build/minio-healthcheck /usr/local/bin/minio-healthcheck
COPY .build/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt

FROM scratch
COPY --from=filesystem /usr/local/bin/minio /usr/local/bin/minio
COPY --from=filesystem /usr/local/bin/minio-healthcheck /usr/local/bin/minio-healthcheck
COPY --from=filesystem /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=filesystem /data /data
COPY --from=filesystem /tmp/minio /tmp/minio
USER 10001:10001
EXPOSE 9000 9001
ENTRYPOINT ["/usr/local/bin/minio"]
CMD ["server", "/data", "--console-address", ":9001"]
