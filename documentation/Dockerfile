# Multi-stage Dockerfile for DevHunt Documentation Portal
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg python3 python3-pip python3-venv \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /repo

# Cache the heavy package installs before copying the full source tree.
COPY ./documentation/package*.json ./documentation/
COPY ./frontend/package*.json ./frontend/
COPY ./ml-service/requirements.txt ./ml-service/requirements.txt

RUN cd documentation && npm ci
RUN cd frontend && (npm ci || npm install --package-lock=false)
RUN python3 -m venv /opt/ml-docs-venv \
    && /opt/ml-docs-venv/bin/pip install --upgrade pip \
    && /opt/ml-docs-venv/bin/pip install -r ml-service/requirements.txt

COPY . .

ENV PATH="/opt/ml-docs-venv/bin:${PATH}"
ENV ALLOW_DOCS_FALLBACK=true

RUN dotnet tool restore

WORKDIR /repo/documentation

RUN npm run prepare:docs
RUN npm run build

# Production stage
FROM nginx:alpine

RUN apk add --no-cache apache2-utils curl

# Copy built documentation
COPY --from=builder /repo/documentation/build /usr/share/nginx/html

# Copy nginx configuration
COPY ./documentation/nginx.conf /etc/nginx/conf.d/default.conf
COPY ./documentation/docker-entrypoint.d/30-docs-auth.sh /docker-entrypoint.d/30-docs-auth.sh
COPY ./documentation/docker-healthcheck.sh /usr/local/bin/docs-healthcheck

RUN chmod +x /docker-entrypoint.d/30-docs-auth.sh /usr/local/bin/docs-healthcheck

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD ["docs-healthcheck"]

CMD ["nginx", "-g", "daemon off;"]
