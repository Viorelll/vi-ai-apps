FROM golang:1.25.1-bookworm AS build
ARG MINIO_VERSION=RELEASE.2025-10-15T17-29-55Z
ENV CGO_ENABLED=0 GOBIN=/out GOMAXPROCS=4
RUN go install github.com/minio/minio@${MINIO_VERSION}
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/minio /usr/local/bin/minio
RUN mkdir /data && chown 10001:10001 /data
USER 10001:10001
EXPOSE 9000 9001
ENTRYPOINT ["minio"]
CMD ["server","/data","--console-address",":9001"]
