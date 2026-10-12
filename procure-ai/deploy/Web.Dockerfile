FROM node:22-alpine AS build
WORKDIR /web
COPY src/procureai-web/package*.json ./
RUN npm ci --no-audit --no-fund
COPY src/procureai-web/ ./
RUN npm run build
FROM nginx:stable-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginx:nginx /web/dist/ /usr/share/nginx/html/
EXPOSE 80
