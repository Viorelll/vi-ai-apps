FROM nginx:stable-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/accessreadai-web/dist/ /usr/share/nginx/html/
EXPOSE 80
