# syntax=docker/dockerfile:1
FROM node:24.19.0-bookworm-slim AS build
WORKDIR /app
COPY src/imageai-web/package*.json ./
RUN --mount=type=secret,id=proxy_ca,target=/tmp/proxy-ca.crt,required=false \
    if [ -f /tmp/proxy-ca.crt ]; then npm config set cafile /tmp/proxy-ca.crt; fi && npm ci --no-audit --no-fund
COPY src/imageai-web ./
RUN npm run build
FROM nginx:1.28-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
