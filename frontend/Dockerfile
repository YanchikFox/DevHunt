# ============================================
# STAGE 1: Dependencies - установка зависимостей
# ============================================
FROM node:20-alpine AS deps
WORKDIR /app

# Check https://github.com/nodejs/docker-node/tree/b4117f9333da4138b03a546ec926ef50a31506c3#nodealpine to understand why libc6-compat might be needed.
RUN apk add --no-cache libc6-compat

# Устанавливаем только зависимости, необходимые для сборки
COPY package.json package-lock.json ./
# Устанавливаем все зависимости (включая dev для сборки)
# Используем --mount=type=cache для ускорения последующих сборок
# --legacy-peer-deps нужен для обхода конфликтов зависимостей (Storybook и др.)
RUN --mount=type=cache,target=/root/.npm \
    npm ci --legacy-peer-deps

# ============================================
# STAGE 2: Builder - сборка приложения
# ============================================
FROM node:20-alpine AS builder
WORKDIR /app

# Install libc6-compat for compatibility
RUN apk add --no-cache libc6-compat

# Копируем зависимости из предыдущего stage
COPY --from=deps /app/node_modules ./node_modules
# Копируем исходный код
COPY . .

# Устанавливаем переменные окружения для оптимизации сборки
ENV NEXT_TELEMETRY_DISABLED=1
ENV NODE_ENV=production

# Build-time env variables for Docker environment
# These are embedded into the built JS bundles
ENV DOCKER_ENV=true
ENV NEXT_PUBLIC_USE_PROXY=true
ENV NEXT_PUBLIC_API_URL=/api/proxy-core
ENV NEXT_PUBLIC_AUTH_URL=/api/proxy-auth
ENV NEXT_PUBLIC_AUTH_API_URL=/api/proxy-auth
ENV NEXT_PUBLIC_USE_MOCKS=false
ENV NEXT_PUBLIC_USE_API=true
# WebSocket URL for SignalR hubs — empty = relative path through nginx proxy
ENV NEXT_PUBLIC_WS_URL=

# Собираем приложение
RUN npm run build

# ============================================
# STAGE 3: Runner - финальный образ только с runtime
# ============================================
FROM node:20-alpine AS runner
WORKDIR /app

# SECURITY: Create non-root user (SEC-015)
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nodejs -u 1001

# Устанавливаем только production зависимости для runtime
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# Копируем только необходимые файлы для production
# В standalone режиме Next.js создает минимальный набор файлов
# Структура: .next/standalone/ содержит server.js, node_modules и public (если есть)
# Копируем standalone (включает все необходимое, включая public)
COPY --from=builder /app/.next/standalone ./
# Копируем статические файлы
COPY --from=builder /app/.next/static ./.next/static

# Создаем директорию для кэша и устанавливаем права
RUN mkdir -p /app/.next/cache && \
    chown -R nodejs:nodejs /app

# SECURITY: Run as non-root user
USER nodejs

# Expose port
EXPOSE 3000

# Set hostname to 0.0.0.0 so Next.js listens on all interfaces (required for Docker healthcheck)
ENV HOSTNAME=0.0.0.0
ENV PORT=3000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget -q --spider http://localhost:3000 || exit 1

# Run application in production mode
CMD ["node", "server.js"]

