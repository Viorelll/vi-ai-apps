FROM node:22.18.0-bookworm-slim AS build
WORKDIR /web
COPY src/buildbudgetai-web/package*.json ./
RUN --mount=type=secret,id=ca,required=false if [ -f /run/secrets/ca ]; then export NODE_EXTRA_CA_CERTS=/run/secrets/ca; fi; npm ci
COPY src/buildbudgetai-web/ ./
RUN npm run build
FROM nginx:1.28.0-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist /usr/share/nginx/html
