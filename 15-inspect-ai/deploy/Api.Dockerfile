# syntax=docker/dockerfile:1
FROM node:24.19.0 AS native-libraries

FROM node:24.19.0-slim AS runtime
WORKDIR /app
RUN mkdir -p /opt/inspectai/native
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libssl.so.3 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libcrypto.so.3 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libz.so.1 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libzstd.so.1 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libgssapi_krb5.so.2 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkrb5.so.3 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libk5crypto.so.3 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libcom_err.so.2 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkrb5support.so.0 /opt/inspectai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkeyutils.so.1 /opt/inspectai/native/
COPY --from=native-libraries /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
ENV LD_LIBRARY_PATH=/opt/inspectai/native
COPY --chown=node:node artifacts/publish/api/ ./
RUN --mount=type=secret,id=egress_ca if [ -s /run/secrets/egress_ca ]; then cp /run/secrets/egress_ca /etc/ssl/certs/ca-certificates.crt; fi
USER node
EXPOSE 8080
ENTRYPOINT ["/app/InspectAI.Api"]
