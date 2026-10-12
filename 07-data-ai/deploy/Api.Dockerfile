FROM node:24.19.0 AS native-libraries

FROM node:24.19.0-slim AS runtime
WORKDIR /app
RUN mkdir -p /opt/dataai/native
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libssl.so.3 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libcrypto.so.3 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libz.so.1 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libzstd.so.1 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libgssapi_krb5.so.2 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkrb5.so.3 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libk5crypto.so.3 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libcom_err.so.2 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkrb5support.so.0 /opt/dataai/native/
COPY --from=native-libraries /usr/lib/x86_64-linux-gnu/libkeyutils.so.1 /opt/dataai/native/
COPY --from=native-libraries /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
ENV LD_LIBRARY_PATH=/opt/dataai/native
COPY artifacts/publish/api/ ./
EXPOSE 8080
ENTRYPOINT ["/app/DataAI.Api"]
