FROM nginx:alpine

# Local/dev: HTTP-only. Production uses Dockerfile.prod (TLS + nginx.conf).
COPY rate-limit-zones.conf /etc/nginx/conf.d/00-rate-limit-zones.conf
COPY edge-security.inc /etc/nginx/conf.d/edge-security.inc
COPY nginx-http-only.conf /etc/nginx/conf.d/default.conf

# Create directory for SSL certificates
RUN mkdir -p /etc/nginx/ssl

RUN apk add --no-cache openssl && \
    mkdir -p /etc/nginx/ssl && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/nginx/ssl/key.pem \
    -out /etc/nginx/ssl/cert.pem \
    -subj "/C=US/ST=State/L=City/O=DevHunt/CN=localhost" && \
    chmod 644 /etc/nginx/ssl/cert.pem && \
    chmod 600 /etc/nginx/ssl/key.pem

# Create directory for Let's Encrypt
RUN mkdir -p /var/www/certbot

# Fix permissions for non-root user
RUN chown -R nginx:nginx /etc/nginx/ssl && \
    chown -R nginx:nginx /var/www/certbot && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d && \
    touch /var/run/nginx.pid && \
    chown nginx:nginx /var/run/nginx.pid

USER nginx

EXPOSE 8080 8443

CMD ["nginx", "-g", "daemon off;"]

