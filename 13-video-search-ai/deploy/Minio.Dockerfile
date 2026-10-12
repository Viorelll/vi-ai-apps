# MinIO server distribution, fixed to an immutable digest for local development.
FROM bitnamilegacy/minio:2025.7.23-debian-12-r3@sha256:953d489a81cc4de7975f90e07202189c4325da39b0b92470b6a13c7ea99e36cd
USER 0
RUN mkdir -p /data && chown 1001:1001 /data
USER 1001
ENTRYPOINT ["/opt/bitnami/minio/bin/minio"]
CMD ["server","/data","--console-address",":9001"]
