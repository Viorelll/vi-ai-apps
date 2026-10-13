FROM node:22.22.0-bookworm-slim AS build
WORKDIR /src
COPY src/moderateai-web/package*.json ./
RUN npm ci
COPY src/moderateai-web/ .
RUN npm run build
FROM nginx:1.28-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginx:nginx /src/dist /usr/share/nginx/html
