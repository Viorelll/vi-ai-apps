FROM node:22-alpine AS build
WORKDIR /web
COPY src/supportai-web/package*.json ./
RUN --mount=type=secret,id=proxy_ca,target=/tmp/proxy-ca.crt,required=false if [ -s /tmp/proxy-ca.crt ]; then NODE_EXTRA_CA_CERTS=/tmp/proxy-ca.crt npm ci; else npm ci; fi
COPY src/supportai-web/ ./
RUN npm run build
FROM nginx:stable-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/ /usr/share/nginx/html/
EXPOSE 80
