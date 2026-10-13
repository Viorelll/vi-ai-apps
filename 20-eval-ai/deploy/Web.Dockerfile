# syntax=docker/dockerfile:1
FROM node:22.21.1-alpine AS build
WORKDIR /web
COPY src/evalai-web/package*.json ./
RUN --mount=type=secret,id=egress_ca,target=/run/egress-ca.pem,required=false if [ -f /run/egress-ca.pem ]; then NODE_EXTRA_CA_CERTS=/run/egress-ca.pem npm ci; else npm ci; fi
COPY src/evalai-web/ ./
RUN npm run build
FROM nginx:1.28.0-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/ /usr/share/nginx/html/
EXPOSE 80
