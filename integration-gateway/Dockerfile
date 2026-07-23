FROM node:20-alpine

WORKDIR /app/integration-gateway

# Install dependencies
COPY integration-gateway/package*.json ./
RUN npm install --omit=dev

# Copy shared packages so ../../../packages resolves to /app/packages
COPY packages /app/packages

# Copy application code
COPY integration-gateway/. .

# Expose port
EXPOSE 5002

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD node -e "require('http').get('http://localhost:5002/health', (r) => {process.exit(r.statusCode === 200 ? 0 : 1)})"

# Run application
USER node
CMD ["node", "src/index.js"]
