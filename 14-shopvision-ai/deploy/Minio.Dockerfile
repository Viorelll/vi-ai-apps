# MinIO community binary distributed by Bitnami, pinned to its verified image digest.
FROM bitnamilegacy/minio:2025.7.23@sha256:8935e75fa5d11295c17171e4aa49efe390a1193cd7f12e4d21b92af9ffef09d7
USER 0
RUN mkdir -p /data && chown 1001:0 /data
USER 1001
ENTRYPOINT ["/opt/bitnami/minio/bin/minio"]
CMD ["server", "/data", "--console-address", ":9001"]
