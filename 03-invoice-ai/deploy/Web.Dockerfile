FROM node:24.19.0-bookworm-slim AS build
WORKDIR /web
COPY src/invoiceai-web/package*.json ./
RUN npm ci
COPY src/invoiceai-web/ ./
RUN npm run build
FROM nginx:1.28.0-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/ /usr/share/nginx/html/
EXPOSE 80
