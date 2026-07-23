# DevHunt Documentation Portal

Портал документаций для платформы DevHunt - системы коллаборативной разработки.

## 🚀 Быстрый запуск

```bash
# Из корня проекта
docker compose up -d documentation

# Документация будет доступна по адресу:
# http://localhost:9000
```

## 📚 Структура портала

### 🏠 Главная страница
- Портал с навигацией по разделам документации
- Быстрые ссылки на ключевые разделы

### ⚙️ Backend Documentation
- C# API Reference (DocFX)
- Database Models и Migrations
- Services Architecture
- REST API Endpoints

### 🎨 Frontend Documentation
- React Components (TypeDoc)
- TypeScript Types
- Hooks и Utilities
- Architecture Overview

### 📚 Additional Sections
- Getting Started Guides
- Architecture Overview
- API Interactive Documentation
- Swagger/OpenAPI Specs

## 🛠️ Локальная разработка

```bash
# Установка зависимостей
cd documentation
npm install

# Разработка
npm start

# Сборка
npm run build

# Подготовка API документации
npm run prepare:docs
```

## 📋 API Документация

Документация генерируется автоматически из:
- C# XML комментариев (DocFX)
- TypeScript исходного кода (TypeDoc)
- OpenAPI спецификаций (Swagger)

## 🔗 Полезные ссылки

- [Главная страница портала](/)
- [Backend Documentation](/docs/backend/intro)
- [Frontend Documentation](/docs/frontend/intro)
- [API Interactive](/docs/api/interactive)
- [Full API Reference](/docfx/index.html)