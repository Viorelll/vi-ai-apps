FROM nginx:1.27-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=nginx:nginx src/inspectai-web/dist /usr/share/nginx/html
EXPOSE 8080
