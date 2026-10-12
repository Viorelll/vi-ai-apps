FROM nginx:1.29-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/meetingai-web/dist/ /usr/share/nginx/html/
EXPOSE 80
