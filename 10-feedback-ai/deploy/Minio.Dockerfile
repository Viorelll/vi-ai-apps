FROM golang:1.25-bookworm AS build
ARG MINIO_VERSION=RELEASE.2025-10-15T17-29-55Z
ENV CGO_ENABLED=0 GOFLAGS=-p=2 GOPROXY="https://proxy.golang.org|direct"
RUN --mount=type=secret,id=proxy_ca,target=/usr/local/share/ca-certificates/cloud-proxy.crt,required=false update-ca-certificates && go mod download github.com/minio/minio@${MINIO_VERSION} && cd /go/pkg/mod/github.com/minio/minio@* && go build -o /out/minio .
COPY deploy/healthprobe.go /probe.go
RUN go build -o /out/healthprobe /probe.go
FROM debian:bookworm-slim
COPY --from=build --chown=10001:10001 /out/ /usr/local/bin/
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
RUN mkdir /data && chown 10001:10001 /data
USER 10001:10001
EXPOSE 9000
ENTRYPOINT ["minio"]
CMD ["server","/data","--console-address",":9001"]
