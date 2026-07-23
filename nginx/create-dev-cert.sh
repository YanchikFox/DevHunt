#!/bin/bash
# Create self-signed certificate for development

mkdir -p ssl

openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout ssl/key.pem \
    -out ssl/cert.pem \
    -subj "/C=US/ST=State/L=City/O=DevHunt/CN=localhost"

chmod 644 ssl/cert.pem
chmod 600 ssl/key.pem

echo "Self-signed certificate created in nginx/ssl/"

