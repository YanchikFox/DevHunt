---
sidebar_position: 2
title: Frontend Code Map
description: Куда смотреть во фронте, чтобы понять архитектуру, данные и точки расширения.
---

# Frontend Code Map

## Стек и базовые команды
- Next.js 14 (app router), TypeScript, shadcn/ui, Tailwind, React Query (через кастомные хуки), SignalR.
- Запуск dev: `cd frontend && npm install && npm run dev`.
- Проверки: `npm run lint`, `npm run typecheck`, `npm run test` (если настроены), `npm run build`.
- Документация по коду: `/docs/frontend/api/README` (TypeDoc Markdown, генерится `npm run prepare:docs` из `documentation`).

## Навигация и страницы (app dir)
- Маршруты: `frontend/src/app/**`.
  - Публичные страницы: `src/app/[locale]/(public)/**`.
  - Auth: `src/app/[locale]/(auth)/**`.
  - Кабинет: `src/app/[locale]/dashboard/**`.
  - API-прокси/next routes: `src/app/api/**`.
- Лейауты и метаданные: `src/app/[locale]/layout.tsx`, `src/app/layout.tsx`, `metadata` в файлах маршрутов.

## UI-слой
- Компоненты: `src/components/**`.
  - UI-библиотека (shadcn): `src/components/ui/*` — базовые блоки.
  - Фичи/страницы: `src/components/features/*`, `src/components/projects/*`, `src/components/profile/*`, `src/components/chat/*`.
  - Обертки: `src/components/layout/*`, `src/components/providers/*`, `src/components/theme-toggle/*`.
- Стили: Tailwind + локальные стили внутри компонентов; глобальные темы/шрифты в `src/app/globals.css` и `src/lib/fonts`.

## Данные и API
- Клиент: `src/lib/api/client.ts` (`apiClient`, `authClient`), адаптеры в `src/lib/api/adapters/**`.
- Запросы/хуки: `src/lib/api/queries/**` (разделы `auth`, `chat`, `profile`, `projects`, `tasks`, `teams`, и т.д.).
- Схемы DTO: `src/lib/api/schema/**`.
- Proxy: `src/app/api/proxy-core/[...path]/route.ts` и `proxy.ts` для серверного проксирования к backend.
- Environment: публичные переменные `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_AUTH_URL`, `NEXT_PUBLIC_WS_URL`, `NEXT_PUBLIC_ML_SERVICE_URL`.

## Состояние и бизнес-логика
- Store: `src/lib/store/chatStore.ts` (zustand или аналог) — чат, непрерывное состояние.
- Hooks: `src/hooks/**` — обёртки над API/состоянием (`useChat`, `use-toast`, `use-current-user`, `use-notifications` и др.).
- Feature flags / конфиг: `src/lib/feature-flags.ts`, `src/lib/auth/constants.ts`.
- Утилиты: `src/lib/utils.ts` (например, `cn`), прочие функции — по каталогам.

## Реалтайм и уведомления
- SignalR клиент и подписки: `src/lib/signalr.ts` + `src/lib/realtime/**`.
- WebSocket URL задаётся `NEXT_PUBLIC_WS_URL`; методы подписки/отписки и ивенты описаны в SignalR-обёртке.

## Аутентификация
- NextAuth конфиг: `src/auth.ts` и `src/auth.config.ts` (роуты в `src/app/api/auth/[...nextauth]/route.ts`).
- Перехватчики/refresh-токены — см. адаптеры и запросы в `src/lib/api/**`.

## Локализация
- Маршруты с `[locale]`; хелперы в `src/i18n/routing.ts` и `src/i18n/request/**`.

## Документация кода
- TypeDoc Markdown: `documentation/docs/frontend/api/**` (не редактируется вручную, генерится).
- Сгенерированный сайт: `/docs/frontend/api/README` внутри портала.

## Что читать новичку во фронте
1) `src/app/[locale]/layout.tsx` и базовые страницы в `(public)/(auth)/dashboard` — понять роутинг и каркас.
2) `src/lib/api/client.ts` + `src/lib/api/queries/**` — как ходим в API.
3) `src/lib/signalr.ts` — реалтайм.
4) `src/lib/store/chatStore.ts` — пример стора.
5) `src/components/ui/*` + основные фичи (`components/chat`, `features/*`) — UI-слой.
