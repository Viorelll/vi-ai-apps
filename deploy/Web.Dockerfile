FROM node:22-alpine AS build
WORKDIR /web
COPY src/docuai-web/package*.json ./
RUN npm ci
COPY src/docuai-web/ ./
RUN npm run build
FROM nginx:stable-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/ /usr/share/nginx/html/
EXPOSE 80
