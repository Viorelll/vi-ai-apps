# syntax=docker/dockerfile:1
FROM node:24.13.0-bookworm-slim AS build
WORKDIR /source
COPY src/videosearchai-web/package*.json ./
RUN --mount=type=secret,id=egress_ca if [ -f /run/secrets/egress_ca ]; then export NODE_EXTRA_CA_CERTS=/run/secrets/egress_ca; fi; npm ci
COPY src/videosearchai-web/ ./
RUN npm run build
FROM nginx:1.28.0-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/dist /usr/share/nginx/html
EXPOSE 8080
