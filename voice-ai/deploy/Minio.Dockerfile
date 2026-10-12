# syntax=docker/dockerfile:1
FROM golang:1.25-bookworm AS build
ARG MINIO_VERSION=RELEASE.2025-10-15T17-29-55Z
ARG MINIO_COMMIT=9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a
ENV CGO_ENABLED=0 GOFLAGS=-p=2 GOTOOLCHAIN=local
RUN --mount=type=secret,id=egress_ca \
    if [ -f /run/secrets/egress_ca ]; then export GIT_SSL_CAINFO=/run/secrets/egress_ca; fi; \
    git clone --depth 1 --branch ${MINIO_VERSION} https://github.com/minio/minio.git /source && \
    test "$(git -C /source rev-parse HEAD)" = "${MINIO_COMMIT}"
WORKDIR /source
RUN --mount=type=cache,target=/go/pkg/mod --mount=type=cache,target=/root/.cache/go-build --mount=type=secret,id=egress_ca \
    if [ -f /run/secrets/egress_ca ]; then export SSL_CERT_FILE=/run/secrets/egress_ca; fi; \
    go build -mod=readonly -o /out/minio . && go mod verify && git diff --exit-code -- go.mod go.sum
FROM debian:bookworm-slim
RUN --mount=type=secret,id=egress_ca \
    mkdir -p /etc/ssl/certs; if [ -f /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi; \
    apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/minio /usr/local/bin/minio
RUN mkdir /data && chown 10001:10001 /data
USER 10001:10001
ENTRYPOINT ["minio"]
CMD ["server", "/data", "--console-address", ":9001"]
