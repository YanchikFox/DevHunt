# DevHunt Frontend — Dokumentacja Techniczna

> **Wersja**: 1.0  
> **Data aktualizacji**: 11 lutego 2026  
> **Technologia**: Next.js 16, React 19, TypeScript 5.3, TailwindCSS 3.4  
> **Port domyślny**: 3000

---

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i uruchomienie](#5-konfiguracja-i-uruchomienie)
6. [Routing i strony](#6-routing-i-strony)
   - 6.1 [Grupy tras (Route Groups)](#61-grupy-tras-route-groups)
   - 6.2 [Strony publiczne](#62-strony-publiczne)
   - 6.3 [Strony uwierzytelniania](#63-strony-uwierzytelniania)
   - 6.4 [Dashboard (chroniony)](#64-dashboard-chroniony)
   - 6.5 [Trasy API (proxy)](#65-trasy-api-proxy)
7. [Uwierzytelnianie (NextAuth.js v5)](#7-uwierzytelnianie-nextauthjs-v5)
   - 7.1 [Przepływ logowania](#71-przepływ-logowania)
   - 7.2 [OAuth (GitHub, Google)](#72-oauth-github-google)
   - 7.3 [Auto-refresh tokenów](#73-auto-refresh-tokenów)
   - 7.4 [Middleware ochrony tras](#74-middleware-ochrony-tras)
   - 7.5 [CSRF (Double Submit Cookie)](#75-csrf-double-submit-cookie)
8. [Warstwa API](#8-warstwa-api)
   - 8.1 [Klient Axios (apiClient / authClient)](#81-klient-axios-apiclient--authclient)
   - 8.2 [Klient serwerowy (server-client)](#82-klient-serwerowy-server-client)
   - 8.3 [Interceptory (PascalCase ↔ camelCase)](#83-interceptory-pascalcase--camelcase)
   - 8.4 [Moduły zapytań (queries)](#84-moduły-zapytań-queries)
   - 8.5 [Schematy Zod (schema.ts)](#85-schematy-zod-schemats)
   - 8.6 [Magazyn tokenów (access-token-store)](#86-magazyn-tokenów-access-token-store)
9. [Stan aplikacji](#9-stan-aplikacji)
   - 9.1 [Zustand (chatStore)](#91-zustand-chatstore)
   - 9.2 [TanStack React Query](#92-tanstack-react-query)
   - 9.3 [In-memory token store](#93-in-memory-token-store)
10. [Real-time (SignalR)](#10-real-time-signalr)
    - 10.1 [Chat Hub](#101-chat-hub)
    - 10.2 [Notification Hub](#102-notification-hub)
11. [Internacjonalizacja (i18n)](#11-internacjonalizacja-i18n)
12. [System motywów (ciemny/jasny)](#12-system-motywów-ciemnyjasny)
13. [Komponenty](#13-komponenty)
    - 13.1 [Komponenty UI (shadcn/ui)](#131-komponenty-ui-shadcnui)
    - 13.2 [Komponenty funkcjonalne (features)](#132-komponenty-funkcjonalne-features)
    - 13.3 [Providery](#133-providery)
    - 13.4 [Layout (Header, Footer)](#134-layout-header-footer)
    - 13.5 [ErrorBoundary](#135-errorboundary)
14. [Hooki](#14-hooki)
15. [SEO i metatagi](#15-seo-i-metatagi)
16. [Testy](#16-testy)
17. [Storybook](#17-storybook)
18. [Docker](#18-docker)
19. [Zmienne środowiskowe](#19-zmienne-środowiskowe)

---

## 1. Przegląd systemu

**Frontend** to aplikacja kliencka platformy DevHunt zbudowana na **Next.js 16** z **React 19**. Pełni rolę interfejsu użytkownika dla wszystkich funkcji platformy:

- **Landing page** — strona główna z opisem platformy, statystykami, CTA
- **Uwierzytelnianie** — logowanie, rejestracja, OAuth (GitHub/Google), weryfikacja e-mail, reset hasła
- **Dashboard** — panel użytkownika z projektami, zadaniami, czatem, powiadomieniami, profilem
- **Projekty** — przeglądanie, tworzenie, zarządzanie zadaniami (tablica Kanban), zespołami, dokumentami
- **Czat** — komunikacja w czasie rzeczywistym (SignalR) z indykatorami pisania i potwierdzeń odczytu
- **Powiadomienia** — push w czasie rzeczywistym (SignalR) z kategoriami i deep linkami
- **Profil** — edycja profilu, umiejętności, prywatność, publiczny profil
- **Administracja** — panel admin z moderacją, zarządzaniem użytkownikami

Aplikacja jest **dwujęzyczna** (rosyjski 🇷🇺 / angielski 🇬🇧) i wspiera **ciemny/jasny motyw**.

---

## 2. Architektura

```
┌─────────────────────────────────────────────────────────────┐
│                     Przeglądarka                            │
│  ┌──────────────┐  ┌────────────┐  ┌─────────────────┐     │
│  │ React 19 SPA │  │ SignalR WS │  │ NextAuth cookies│     │
│  └──────┬───────┘  └─────┬──────┘  └────────┬────────┘     │
└─────────┼────────────────┼───────────────────┼──────────────┘
          │ HTTP           │ WebSocket         │ Cookie
┌─────────▼────────────────▼───────────────────▼──────────────┐
│              Next.js 16 Server (Node.js 20) :3000           │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                    Middleware                         │   │
│  │  NextAuth (JWT session) + next-intl (locale routing) │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │               App Router (Pages)                      │   │
│  │  ┌─────────┐ ┌───────────┐ ┌──────────┐ ┌─────────┐ │   │
│  │  │ (auth)  │ │ (public)  │ │dashboard │ │ api/    │ │   │
│  │  │ login   │ │ projects  │ │ projects │ │ proxy-  │ │   │
│  │  │ register│ │ community │ │ chats    │ │ core/   │ │   │
│  │  │ forgot  │ │ users     │ │ profile  │ │ proxy-  │ │   │
│  │  │ reset   │ │ showcase  │ │ teams    │ │ auth/   │ │   │
│  │  │ verify  │ │ internship│ │ work     │ │         │ │   │
│  │  └─────────┘ └───────────┘ └──────────┘ └─────────┘ │   │
│  └──────────────────────────────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │                Rewrites (Proxy)                       │   │
│  │  /api/proxy-core/* → Core API :7002                   │   │
│  │  /api/proxy-auth/* → Auth Service :7001               │   │
│  │  /chatHub/*        → Core API SignalR                 │   │
│  │  /notificationHub/*→ Core API SignalR                 │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────┬──────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
   ┌──────▼─────┐  ┌──────▼──────┐  ┌──────▼──────┐
   │ Core API   │  │ Auth Service│  │  ML Service │
   │ :7002      │  │ :7001       │  │  :8000      │
   └────────────┘  └─────────────┘  └─────────────┘
```

### Architektura kliencka

```
                     Providers
                        │
          ┌─────────────┼─────────────┐
          │             │             │
   SessionProvider  QueryClient  ThemeProvider
          │             │             │
   SessionTokenSync     │             │
   (→ in-memory store)  │             │
          │             │             │
          └─────────┬───┘             │
                    │                 │
              ┌─────▼──────┐         │
              │ apiClient  │         │
              │ authClient │         │
              │  (Axios)   │         │
              │ + CSRF     │         │
              │ + PascalCase│        │
              └─────┬──────┘         │
                    │                │
              React Components ──────┘
              + Custom Hooks
              + Zustand Store
              + SignalR Connections
```

---

## 3. Stos technologiczny

### Runtime (34 zależności)

| Kategoria | Technologia | Wersja |
|-----------|-------------|--------|
| Framework | Next.js | 16.0.1 |
| UI | React | 19.2.0 |
| Język | TypeScript | 5.3 |
| Stylowanie | TailwindCSS | 3.4.18 |
| Animacje CSS | tailwindcss-animate | 1.0.7 |
| Utility CSS | clsx + tailwind-merge | 2.1 / 3.4 |
| CVA | class-variance-authority | 0.7.1 |
| Komponenty UI | Radix UI (13 pakietów) | — |
| Ikony | lucide-react | 0.553 |
| Uwierzytelnianie | next-auth (v5 beta) | 5.0.0-beta.19 |
| Stan globalny | Zustand | 5.0.8 |
| Data fetching | TanStack React Query | 5.90 |
| Formularze | react-hook-form | 7.66 |
| Walidacja | Zod | 4.1 |
| HTTP | Axios | 1.13 |
| i18n | next-intl | 4.5.3 |
| Motywy | next-themes | 0.4.6 |
| Real-time | @microsoft/signalr | 9.0.6 |
| Drag & Drop | @dnd-kit/core + sortable | 6.1 / 10.0 |
| Daty | date-fns | 4.1 |
| Markdown | react-markdown | 10.1 |
| Diagramy | mermaid | 11.4 |
| Query strings | qs | 6.14 |

### Dev (27 zależności)

| Kategoria | Technologia | Wersja |
|-----------|-------------|--------|
| Testy jednostkowe | Vitest | 4.0 |
| Testy E2E | Playwright | 1.41 |
| Testy komponentów | Testing Library (react) | 16.3 |
| Testy a11y | jest-axe + @axe-core/react | 8.0 / 4.8 |
| Visual regression | Storybook + Chromatic | 8.6 / 13.3 |
| Pokrycie kodu | @vitest/coverage-v8 | 4.0 |
| Linting | ESLint | 9.39 |
| Formatting | Prettier + prettier-plugin-tailwindcss | 3.1 / 0.7 |
| Git hooks | Husky + lint-staged | 9.1 / 16.2 |
| Dokumentacja kodu | TypeDoc | 0.28 |
| DOM (test env) | jsdom | 27.3 |

---

## 4. Struktura projektu

```
frontend/
├── next.config.mjs              # Konfiguracja Next.js (proxy, standalone, i18n)
├── tailwind.config.ts           # Konfiguracja TailwindCSS (design tokens)
├── tsconfig.json                # TypeScript strict, path alias @/*
├── package.json                 # 34 runtime + 27 dev zależności
├── Dockerfile                   # Multi-stage: deps → build → runner
├── vitest.config.ts             # Vitest: jsdom, coverage v8
├── playwright.config.ts         # Playwright: Chromium, auto-start
├── eslint.config.mjs            # Flat config: next/core-web-vitals
├── postcss.config.cjs           # PostCSS: TailwindCSS + autoprefixer
├── .env.local                   # Zmienne środowiskowe (lokalne)
│
├── messages/                    # Pliki i18n
│   ├── pl.json                  # Polski 
│   └── en.json                  # Angielski
│
├── .storybook/                  # Konfiguracja Storybook
│   ├── main.ts                  # React-Vite, addons: a11y, viewport
│   └── preview.ts               # Globalne style, a11y config
│
├── tests/                       # Testy
│   ├── unit/                    # Vitest: button.test.tsx, config.test.ts
│   ├── a11y/                    # Vitest + jest-axe: a11y-audit.test.tsx
│   ├── e2e/                     # Playwright: auth.spec.ts
│   └── e2e-full/                # Playwright suite (18+ spec files)
│       ├── auth/                # Testy logowania, rejestracji
│       ├── projects/            # Testy projektów
│       ├── tasks/               # Testy zadań
│       ├── admin/               # Testy panelu admin
│       ├── realtime/            # Testy WebSocket
│       ├── social/              # Testy społecznościowe
│       ├── edge-cases/          # Testy brzegowe
│       ├── page-objects/        # POM (Page Object Model)
│       └── fixtures/            # Test fixtures
│
└── src/
    ├── middleware.ts             # NextAuth + next-intl middleware
    ├── auth.ts                  # NextAuth konfiguracja i callbacki
    ├── auth.config.ts           # NextAuth: sesja, cookies, ochrona tras
    │
    ├── app/                     # App Router (Next.js)
    │   ├── layout.tsx           # Root layout (html, body)
    │   ├── globals.css          # CSS variables, design tokens
    │   ├── robots.ts            # robots.txt generator
    │   ├── sitemap.ts           # sitemap.xml generator
    │   ├── opengraph-image.tsx  # OG image generator
    │   ├── api/                 # API Routes (NextAuth handlers)
    │   └── [locale]/            # Routing z locale (pl/en)
    │       ├── layout.tsx       # Locale layout (NextIntlClientProvider)
    │       ├── page.tsx         # Strona główna (landing)
    │       ├── oauth-callback/  # OAuth callback handler
    │       ├── (auth)/          # Grupa: logowanie, rejestracja
    │       ├── (public)/        # Grupa: publiczne strony
    │       ├── (protected)/     # Grupa: admin, debug
    │       └── dashboard/       # Chroniony panel użytkownika
    │
    ├── components/              # React komponenty
    │   ├── ui/                  # shadcn/ui (30 komponentów)
    │   ├── features/            # Złożone komponenty (9 szt.)
    │   ├── layout/              # Header, Footer
    │   ├── admin/               # Panel administracyjny (9 szt.)
    │   ├── chat/                # Czat (20 komponentów)
    │   ├── profile/             # Profil (17 komponentów)
    │   ├── projects/            # Projekty (22+ komponentów)
    │   ├── news/                # Aktualności (4 szt.)
    │   ├── showcase/            # Showcase (3 szt.)
    │   ├── ai-planner/          # AI planowanie
    │   ├── providers.tsx        # Root providers
    │   ├── ErrorBoundary.tsx    # Obsługa błędów
    │   ├── theme-toggle.tsx     # Przełącznik motywu
    │   └── language-switcher.tsx # Przełącznik języka
    │
    ├── hooks/                   # Custom React hooks (15 szt.)
    │   ├── use-current-user.ts
    │   ├── use-debounced-value.ts
    │   ├── use-notifications.ts
    │   ├── use-project-task-board.ts
    │   ├── use-project-tasks.ts
    │   ├── use-project-team.ts
    │   ├── use-project-dialogs.ts
    │   ├── use-project-activity.ts
    │   ├── use-project-status-actions.ts
    │   ├── use-project-invitations.ts
    │   ├── use-project-task-handlers.ts
    │   ├── use-project-gallery.ts
    │   ├── use-gallery-lightbox.ts
    │   ├── use-toast.ts
    │   └── useChat.ts
    │
    ├── lib/                     # Biblioteki i narzędzia
    │   ├── api/                 # Warstwa API
    │   │   ├── client.ts        # Axios (apiClient + authClient)
    │   │   ├── server-client.ts # Server-side fetch klient
    │   │   ├── schema.ts        # Schematy Zod (~30 typów)
    │   │   ├── csrf.ts          # CSRF Double Submit Cookie
    │   │   ├── access-token-store.ts # In-memory token store
    │   │   ├── adapters/        # Mapery backend ↔ frontend
    │   │   └── queries/         # TanStack Query hooks (25 modułów)
    │   ├── auth/                # Stałe auth
    │   ├── store/               # Zustand stores (chatStore)
    │   ├── realtime/            # SignalR notification client
    │   ├── validation/          # Walidacja hasła (Zod + stałe)
    │   ├── signalr.ts           # SignalR chat client
    │   ├── jwt.ts               # Dekodowanie JWT (client-side)
    │   ├── utils.ts             # cn(), toCamelCase, toPascalCase
    │   ├── feature-flags.ts     # Flagi funkcji
    │   └── fonts.ts             # Konfiguracja czcionek
    │
    ├── i18n/                    # Internacjonalizacja
    │   ├── routing.ts           # Locale routing (pl, en)
    │   └── request.ts           # Server-side locale resolution
    │
    ├── mocks/                   # Mock data (offline dev)
    └── types/                   # Rozszerzenia typów TypeScript
        ├── next-auth.d.ts       # Rozszerzenie sesji NextAuth
        └── vitest-matchers.d.ts # Matchery testowe
```

---

## 5. Konfiguracja i uruchomienie

### 5.1 next.config.mjs

Kluczowe ustawienia:

| Opcja | Wartość | Cel |
|-------|---------|-----|
| `output` | `"standalone"` | Zoptymalizowany obraz Docker |
| `reactStrictMode` | `true` | Wykrywanie problemów React |
| `productionBrowserSourceMaps` | `false` | Bezpieczeństwo: ukrycie kodu źródłowego |
| `optimizePackageImports` | `["lucide-react", "@radix-ui/react-dialog"]` | Tree-shaking |
| `typescript.ignoreBuildErrors` | `false` | Wymuszenie poprawności typów |

**Proxy (rewrites):**

```
/api/proxy-core/*      → http://core-api:8080/api/*         (Core API)
/api/proxy-auth/*      → http://auth-service:8080/api/*     (Auth Service)
/:locale/chatHub/*     → http://core-api:8080/chatHub/*     (SignalR Chat)
/:locale/notificationHub/* → http://core-api:8080/notificationHub/* (SignalR Notifications)
```

Proxy rozróżnia środowisko Docker (`DOCKER_ENV=true` lub `NEXT_PUBLIC_USE_PROXY=true`) od lokalnego.

**Dozwolone ródła obrazów:**

| Pattern | Opis |
|---------|------|
| `**.devhunt.io` (HTTPS) | Produkcja |
| `api.dicebear.com` (HTTPS) | Generowane avatary |
| `localhost:8333` (HTTP) | Lokalny SeaweedFS |
| `object-storage:8333` (HTTP) | Docker SeaweedFS |

### 5.2 Uruchomienie lokalne

```bash
cd frontend
npm install --legacy-peer-deps

# Development (hot reload)
npm run dev
# → http://localhost:3000

# Lub przez task VS Code:
# "Run Frontend (Hot Reload)"
```

### 5.3 Uruchomienie w Docker

```bash
docker-compose up -d --build frontend
# → http://localhost:3000
```

### 5.4 Skrypty npm

| Skrypt | Polecenie | Opis |
|--------|-----------|------|
| `dev` | `next dev` | Dev server z hot reload |
| `build` | `next build` | Build produkcyjny |
| `start` | `next start` | Serwer produkcyjny |
| `lint` | `eslint .` | Sprawdzenie ESLint |
| `type-check` | `tsc --noEmit` | Sprawdzenie typów TypeScript |
| `test` | `vitest` | Testy jednostkowe (watch) |
| `test:unit` | `vitest run tests/unit` | Testy jednostkowe (single run) |
| `test:a11y` | `vitest run tests/a11y` | Testy dostępności |
| `test:e2e` | `playwright test` | Testy E2E (szybkie) |
| `test:e2e:full` | `playwright test -c playwright.full.config.ts` | Testy E2E (pełna suite) |
| `test:contract` | `vitest run tests/contract` | Testy kontraktowe |
| `storybook` | `storybook dev -p 6006` | Storybook dev server |
| `build-storybook` | `storybook build` | Build Storybook |
| `chromatic` | `chromatic --exit-zero-on-changes` | Visual regression (CI) |
| `docs` | `typedoc` | Generowanie dokumentacji kodu |

---

## 6. Routing i strony

### 6.1 Grupy tras (Route Groups)

Next.js App Router z **route groups** do logicznej separacji:

```
/[locale]/
├── (auth)/              # Logowanie, rejestracja — bez nawigacji
│   └── layout.tsx       # Layout: wycentrowana karta
├── (public)/            # Publicznie dostępne strony
│   └── layout.tsx       # Layout: Header + Footer
├── (protected)/         # Admin, debug — pełna autoryzacja
│   ├── admin/
│   └── debug/
├── dashboard/           # Panel użytkownika — sidebar + header
│   └── layout.tsx       # Layout: chroniony, z nawigacją
└── oauth-callback/      # Callback po OAuth
```

**Nawiasy `()` nie wpływają na URL** — to organizacja logiczna.

### 6.2 Strony publiczne

| Trasa | Komponent | Typ | Opis |
|-------|-----------|-----|------|
| `/` | `page.tsx` | Server | Landing page: hero, cechy, statystyki, CTA |
| `/projects` | `projects/page.tsx` | — | Lista publicznych projektów |
| `/users/[userId]` | `users/[userId]/page.tsx` | — | Publiczny profil użytkownika |
| `/community` | `community/page.tsx` | — | Społeczność deweloperów |
| `/showcase` | `showcase/page.tsx` | — | Galeria opublikowanych projektów |
| `/internships` | `internships/page.tsx` | — | Staże i hackathony |

### 6.3 Strony uwierzytelniania

| Trasa | Komponent | Typ | Opis |
|-------|-----------|-----|------|
| `/login` | `login/page.tsx` | Client | Logowanie (e-mail + OAuth) |
| `/register` | `register/page.tsx` | Client | Rejestracja z walidacją hasła na żywo |
| `/forgot-password` | `forgot-password/page.tsx` | Client | Żądanie resetu hasła |
| `/reset-password` | `reset-password/page.tsx` | Client | Formularz nowego hasła |
| `/verify-email` | `verify-email/page.tsx` | Client | Wprowadzenie kodu weryfikacyjnego |
| `/oauth-callback` | `oauth-callback/page.tsx` | Client | Obsługa callbacka OAuth |

### 6.4 Dashboard (chroniony)

| Trasa | Opis |
|-------|------|
| `/dashboard` | Strona główna: powitanie, statystyki, feed, projekty, sugerowane profile |
| `/dashboard/projects` | Lista projektów użytkownika |
| `/dashboard/projects/[id]` | Szczegóły projektu (tablica zadań, zespół, dokumenty, galeria, integracje) |
| `/dashboard/chats` | Czat w czasie rzeczywistym (SignalR) |
| `/dashboard/profile` | Profil użytkownika |
| `/dashboard/profile/edit` | Edycja profilu |
| `/dashboard/profile/privacy` | Ustawienia prywatności |
| `/dashboard/profile/[userId]` | Profil innego użytkownika |
| `/dashboard/teams` | Zarządzanie zespołami |
| `/dashboard/notifications` | Centrum powiadomień |
| `/dashboard/invitations` | Zaproszenia do projektów |
| `/dashboard/work` | Zadania przypisane użytkownikowi |
| `/dashboard/support` | Zgłoszenia wsparcia |
| `/dashboard/support/new` | Nowe zgłoszenie |
| `/dashboard/support/[ticketId]` | Szczegóły zgłoszenia |
| `/dashboard/messages` | Wiadomości |
| `/dashboard/showcase-preview` | Podgląd showcase |
| `/dashboard/internships` | Staże |

### 6.5 Trasy API (proxy)

| Trasa | Cel | Backend |
|-------|-----|---------|
| `/api/auth/*` | NextAuth handlers | Wewnętrzny |
| `/api/client-error/*` | Raportowanie błędów klienta | Wewnętrzny |
| `/api/proxy-core/*` | Core API proxy | `core-api:8080` |
| `/api/proxy-auth/*` | Auth Service proxy | `auth-service:8080` |

---

## 7. Uwierzytelnianie (NextAuth.js v5)

Frontend korzysta z **NextAuth v5 (beta)** z providerem `Credentials` do integracji z Auth Service.

### 7.1 Przepływ logowania

```
      Użytkownik (formularz login)
             │
             │ email + password
             │
      ┌──────▼──────────────┐
      │   signIn("credentials",│
      │   { email, password,   │
      │     redirect: false }) │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  NextAuth authorize() │
      │  → fetch POST         │
      │  Auth Service /login  │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  Auth Service        │
      │  zwraca:             │
      │  - accessToken       │
      │  - refreshToken      │
      │  - userId, email     │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  jwt callback        │
      │  Zapisuje w tokenie: │
      │  - accessToken       │
      │  - refreshToken      │
      │  - accessTokenExpires│
      │    (25 min od teraz) │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  session callback    │
      │  Przenosi do sesji:  │
      │  - user.id           │
      │  - accessToken       │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  SessionTokenSync    │
      │  (komponent React)   │
      │  → setAccessToken()  │
      │  (in-memory store)   │
      └──────┬──────────────┘
             │
      ┌──────▼──────────────┐
      │  Axios interceptor   │
      │  Dodaje nagłówek:    │
      │  Authorization:      │
      │  Bearer <token>      │
      └─────────────────────┘
```

**Konfiguracja sesji:**

| Parametr | Wartość | Opis |
|----------|---------|------|
| `strategy` | `"jwt"` | Sesja przechowywana w JWT (stateless) |
| `maxAge` | 7 dni | Dopasowane do refresh token |
| `updateAge` | 5 minut | Odświeżanie cookie sesji przy aktywności |
| Cookie name | `authjs.session-token` | HttpOnly, SameSite=Lax |
| `refetchInterval` | 4 minuty | Automatyczne odświeżanie sesji (providers) |

### 7.2 OAuth (GitHub, Google)

**Przepływ OAuth na frontendzie:**

```
1. Użytkownik klika "Zaloguj przez GitHub"
2. Frontend buduje URL:
   {AUTH_API}/api/auth/login/github?redirectUri={origin}/{locale}/oauth-callback
3. Redirect → Auth Service → GitHub → Auth Service callback
4. Auth Service redirect → /oauth-callback?accessToken=...&refreshToken=...

5. Strona oauth-callback:
   - Pobiera tokeny z URL query params
   - Wywołuje signIn("credentials", { accessToken, refreshToken, userId })
   - NextAuth zapisuje tokeny w sesji
   - Redirect → /dashboard
```

### 7.3 Auto-refresh tokenów

Tokeny JWT wygasają po **30 minutach**. NextAuth automatycznie odświeża je **5 minut przed wygaśnięciem** (25 min):

```typescript
// jwt callback
if (Date.now() < token.accessTokenExpires) {
  return token; // Token ważny — nie odświeżaj
}

// Token wygasł — odśwież
const response = await fetch(authService + "/api/auth/refresh", {
  method: "POST",
  body: JSON.stringify({ refreshToken: token.refreshToken })
});

if (response.ok) {
  const data = await response.json();
  return {
    ...token,
    accessToken: data.accessToken,
    refreshToken: data.refreshToken ?? token.refreshToken,
    accessTokenExpires: Date.now() + 25 * 60 * 1000 // 25 min
  };
} else {
  // Refresh failed → wymuszenie ponownego logowania
  return { ...token, error: "RefreshTokenError" };
}
```

**Obsługa `RefreshTokenError` na kliencie (`SessionTokenSync`):**

```typescript
if (session?.error === "RefreshTokenError") {
  setAccessToken(null);
  signOut({ redirect: true, callbackUrl: "/login" });
}
```

### 7.4 Middleware ochrony tras

Middleware Next.js łączy **NextAuth** z **next-intl**:

```typescript
export default auth((req) => {
  return intlMiddleware(req);
});

export const config = {
  matcher: ["/((?!api|_next|.*chatHub.*|.*notificationHub.*|.*\\..*).*)"],
};
```

**Trasy publiczne (bez autoryzacji):**

```typescript
const PUBLIC_ROUTES = [
  "/login", "/register", "/forgot-password",
  "/reset-password", "/verify-email", "/oauth-callback"
];
```

Każda trasa jest sprawdzana z uwzględnieniem locale prefix (`/pl/login`, `/en/login`). Strona główna (`/`) i publiczne strony są dostępne bez logowania.

### 7.5 CSRF (Double Submit Cookie)

**Implementacja w `csrf.ts`:**

| Funkcja | Opis |
|---------|------|
| `getCsrfTokenFromCookie()` | Czyta cookie `XSRF-REQUEST-TOKEN` |
| `fetchCsrfToken(baseUrl)` | Pobiera token z `/Auth/csrf-token` (z deduplicacją) |
| `getCsrfToken()` | Cache → cookie → null |
| `clearCsrfToken()` | Czyści cache + usuwa cookie |
| `refreshCsrfToken()` | Odświeża po login/logout |
| `requiresCsrfToken(method)` | `true` dla POST/PUT/DELETE/PATCH |

**Integracja z Axios interceptors:**

Każdy state-changing request (POST/PUT/DELETE/PATCH) automatycznie dodaje nagłówek `X-CSRF-TOKEN`. Wyjątki: `/auth/login`, `/auth/register`, `/auth/refresh`, `/auth/forgot-password`, `/auth/reset-password`.

Jeśli backend zwróci 403 z `CSRF_USER_MISMATCH` — interceptor automatycznie odświeża token i ponawia request raz.

---

## 8. Warstwa API

### 8.1 Klient Axios (apiClient / authClient)

Dwa oddzielne klienty Axios:

| Klient | Cel | Base URL (dev) | Base URL (Docker) |
|--------|-----|----------------|-------------------|
| `apiClient` | Core API | `http://localhost:7002/api` | `/api/proxy-core` |
| `authClient` | Auth Service | `http://localhost:7001/api` | `/api/proxy-auth` |

**Wspólna konfiguracja:**

```typescript
{
  timeout: 30_000,             // 30s timeout
  withCredentials: true,       // Cookies (R7 httpOnly)
  paramsSerializer: (params) =>
    qs.stringify(params, { arrayFormat: "repeat" })
}
```

### 8.2 Klient serwerowy (server-client)

Dla Server Components — natywny `fetch` (bez Axios):

```typescript
serverApiClient.get<T>(path, options?)    // ISR cache: 5 min
serverApiClient.post<T>(path, data?)
serverApiClient.put<T>(path, data?)
serverApiClient.patch<T>(path, data?)
serverApiClient.delete<T>(path)
```

Brak konwersji case, brak CSRF, brak interceptorów — prosty wrapper nad `fetch`.

### 8.3 Interceptory (PascalCase ↔ camelCase)

Backend .NET zwraca JSON z kluczami PascalCase (`FullName`, `CreatedAt`). Frontend używa camelCase (`fullName`, `createdAt`).

**Request interceptor:** `toPascalCase(data)` — konwertuje klucze body przed wysłaniem (pomija `FormData`).

**Response interceptor:** `toCamelCase(data)` — konwertuje klucze odpowiedzi.

**Implementacja (`utils.ts`):**

```typescript
export function toCamelCase(obj: unknown): unknown {
  if (Array.isArray(obj)) return obj.map(toCamelCase);
  if (obj && typeof obj === "object") {
    return Object.fromEntries(
      Object.entries(obj).map(([k, v]) => [
        k.charAt(0).toLowerCase() + k.slice(1), // PascalCase → camelCase
        toCamelCase(v)
      ])
    );
  }
  return obj;
}
```

### 8.4 Moduły zapytań (queries)

25 modułów TanStack React Query w `lib/api/queries/`:

| Moduł | Opis |
|-------|------|
| `auth.ts` | `useRegister()`, `useLogin()`, `useRefresh()`, `useForgotPassword()`, `useResetPassword()`, `useVerifyEmail()`, `useResendVerification()` |
| `projects.ts` | `useProjectsList()`, `useProject()`, `useCreateProject()`, `useUpdateProject()`, `useDeleteProject()`, `useFeatureProject()` |
| `tasks.ts` | `useProjectTasks()`, `useCreateTask()`, `useUpdateTask()`, `useDeleteTask()`, `useMoveTask()`, `useTaskLinks()` |
| `teams.ts` | `useTeamMembers()`, `useJoinProject()`, `useLeaveProject()`, `useUpdateMemberRole()`, `useRemoveMember()` |
| `chat.ts` | `useConversations()`, `useMessages()`, `useCreateConversation()`, `useSendMessage()` |
| `notifications.ts` | `useNotifications()`, `useMarkNotificationAsRead()`, `useMarkAllAsRead()` |
| `profile.ts` | `useAuth()`, `useProfile()`, `useUpdateProfile()`, `useUserStats()`, `usePublicProfile()` |
| `skills.ts` | `useSkills()`, `useAddSkill()`, `useRemoveSkill()` |
| `columns.ts` | `useColumns()`, `useCreateColumn()`, `useUpdateColumn()`, `useDeleteColumn()`, `useReorderColumns()` |
| `integrations.ts` | `useGitHubIntegration()`, `useConnectGitHub()`, `useSyncGitHub()` |
| `admin.ts` | `useAdminUsers()`, `useAdminProjects()`, `useUpdateUserRole()`, `useBanUser()` |
| `news.ts` | `useProjectNews()`, `useCreateNews()`, `useUpdateNews()`, `useDeleteNews()` |
| `showcase.ts` | `useShowcase()`, `usePublishToShowcase()` |
| `badges.ts` | `useBadges()`, `useUserBadges()` |
| `documents.ts` | `useProjectDocuments()`, `useCreateDocument()`, `useUpdateDocument()` |
| `files.ts` | `useProjectFiles()`, `useUploadFile()`, `useDeleteFile()`, `useProjectGallery()` |
| `ai-plans.ts` | `useAiPlan()`, `useGenerateAiPlan()`, `useApplyAiPlan()` |
| `board-settings.ts` | `useBoardSettings()`, `useUpdateBoardSettings()` |
| `internships.ts` | `useInternships()`, `useCreateInternship()` |
| `moderation.ts` | `useModerationReports()`, `useCreateReport()`, `useResolveReport()` |
| `project-artifacts.ts` | `useProjectArtifacts()`, `useCreateArtifact()` |
| `project-context.ts` | `useProjectContext()`, `useUpdateContext()` |
| `settings.ts` | `useSettings()`, `useUpdateSettings()` |
| `superadmin.ts` | `useSuperAdminStats()`, `useSystemHealth()` |
| `support.ts` | `useSupportTickets()`, `useCreateTicket()`, `useTicketDetail()` |

### 8.5 Schematy Zod (schema.ts)

~30 schematów Zod definiujących model danych:

| Schema | Kluczowe pola |
|--------|---------------|
| `UserSchema` | `id, email, name, role, avatar, bio, skills[], experienceLevel, timezone, language` |
| `ProjectSchema` | `id, title, description, technologies[], requiredRoles[], status, visibility, ownerId, teamSize, maxTeamSize, rating` |
| `TaskSchema` | `id, projectId, title, status, priority, assigneeId, dueDate, columnId, positionInColumn, linkCount, attachmentCount, tags[]` |
| `TeamMemberSchema` | `id, userId, projectId, role, canPublishNews, canManageTasks, canManageFiles` |
| `InvitationSchema` | `id, projectId, inviteeId, inviterId, type, role, message, status` |
| `NotificationSchema` | `id, userId, type, title, message, read, metadata` |
| `InternshipSchema` | `id, title, type, requirements[], duration, isRemote, location, deadline` |
| `LoginRequestSchema` | `email, password` (min 8, regex) |
| `RegisterRequestSchema` | `email, password, fullName` (min 2) |
| `AuthResponseSchema` | `accessToken, refreshToken, email, userId` |

**Statusy:**

| Encja | Statusy |
|-------|---------|
| `Project` | `draft, active, recruiting, in_progress, completed, cancelled, archived` |
| `Task` | `todo, doing, review, done, archived, cancelled, in_progress` |
| `Invitation` | `pending, accepted, declined, cancelled` |
| `User.Role` | `participant, company, curator, admin, superadmin` |
| `TeamMember.Role` | `owner, lead, developer, designer, devops, qa` |
| `Task.Priority` | `low, medium, high, urgent` |

### 8.6 Magazyn tokenów (access-token-store)

Bezpieczne przechowywanie access tokena **wyłącznie w pamięci** (nie localStorage, nie sessionStorage):

```typescript
let accessToken: string | null = null;
const listeners = new Set<() => void>();

export function setAccessToken(token?: string | null) {
  accessToken = token ?? null;
  listeners.forEach(fn => fn());
}

export function getAccessToken(): string | null {
  return accessToken;
}

// React hook z useSyncExternalStore
export function useAccessToken(): string | null {
  return useSyncExternalStore(subscribe, getSnapshot);
}
```

**Dlaczego in-memory?**
- Odporny na XSS (JavaScript nie ma dostępu do localStorage/sessionStorage tokena)
- Synchronizowany z sesją NextAuth przez `SessionTokenSync`
- Automatycznie czyszczony przy wylogowaniu/błędzie

---

## 9. Stan aplikacji

### 9.1 Zustand (chatStore)

Globalny stan widgetu czatu:

```typescript
interface ChatStore {
  isOpen: boolean;
  activeConversationId: string | null;
  recipientUser: RecipientUser | null;

  openWidget(): void;
  closeWidget(): void;
  toggleWidget(): void;
  openConversation(id: string): void;
  openNewChat(user: RecipientUser): void;
  resetActiveChat(): void;
}
```

### 9.2 TanStack React Query

Konfiguracja globalna (w `Providers`):

```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,         // 1 minuta cache
      refetchOnWindowFocus: false,
    },
  },
});
```

Każdy moduł w `queries/` eksportuje hooks używające `useQuery`, `useMutation`, `useInfiniteQuery` z odpowiednimi kluczami cache i invalidacją.

### 9.3 In-memory token store

Patrz sekcja 8.6 — token w pamięci, zsynchronizowany z NextAuth session.

---

## 10. Real-time (SignalR)

### 10.1 Chat Hub

**Endpoint:** `/chatHub` (proxy przez Next.js → Core API)

**Transporty:** WebSockets (preferowany) + LongPolling (fallback)

**Auto-reconnect:** `[0ms, 2000ms, 10000ms, 30000ms]`

**Zdarzenia serwera:**

| Zdarzenie | Dane | Opis |
|-----------|------|------|
| `ReceiveMessage` | `ChatMessage` | Nowa wiadomość |
| `MessageReceived` | `ChatMessage` | Alternatywna nazwa |
| `MessageRead` | `MessageReadEvent` | Potwierdzenie odczytu |
| `UserTyping` | `UserTypingEvent` | Indykator pisania |
| `UserJoined` | `UserJoinedEvent` | Użytkownik dołączył |
| `UserLeft` | `userId` | Użytkownik opuścił |

**Metody klienta:**

| Metoda | Opis |
|--------|------|
| `joinChat(conversationId)` | Dołączenie do pokoju |
| `leaveChat(conversationId)` | Opuszczenie pokoju |
| `sendMessage(conversationId, content)` | Wysłanie wiadomości |
| `markAsRead(conversationId)` | Potwierdzenie odczytu |
| `sendTyping(conversationId, isTyping)` | Indykator pisania |

**Interfejs `ChatMessage`:**

```typescript
interface ChatMessage {
  id: string;
  senderId?: string;
  sender?: { id: string; fullName?: string; avatarUrl?: string };
  content: string;
  createdAt: string;
  conversationId?: string;
  isEdited?: boolean;
  replyToId?: string;
  replyTo?: { id: string; content?: string; fullName?: string };
}
```

### 10.2 Notification Hub

**Endpoint:** `/notificationHub`

**Implementacja:** Wzorzec Factory z dwoma klientami:

| Klasa | Warunek | Zachowanie |
|-------|---------|------------|
| `SignalRNotificationClient` | `USE_MOCKS === false` | Prawdziwe połączenie SignalR |
| `FakeNotificationClient` | `USE_MOCKS === true` | Mock: symuluje powiadomienie `"invitation"` co 30 sekund |

**Reconnection (SignalR):** Exponential backoff: 0s → 2s → 10s → 30s

**Interfejs:**

```typescript
interface NotificationClient {
  connect(userId: string, token: string): Promise<void>;
  disconnect(): void;
  onMessage(cb: (notification: Notification) => void): void;
  onError(cb: (error: Error) => void): void;
}
```

---

## 11. Internacjonalizacja (i18n)

### Konfiguracja

| Parametr | Wartość |
|----------|---------|
| Biblioteka | `next-intl` v4.5 |
| Domyślny locale | `en` |
| Obsługiwane locale | `en`, `pl` |
| Pliki tłumaczeń | `messages/pl.json`, `messages/en.json` |
| Routing | `[locale]` segment w URL |

### Routing (`i18n/routing.ts`)

```typescript
export const routing = defineRouting({
  locales: ["pl", "en"],
  defaultLocale: "en",
});

export const { Link, redirect, usePathname, useRouter } = createNavigation(routing);
```

### Użycie

**Server Components:**

```typescript
const t = await getTranslations("home");
return <h1>{t("title")}</h1>;
```

**Client Components:**

```typescript
const t = useTranslations("dashboard");
return <p>{t("welcome", { name: user.name })}</p>;
```

### Przełącznik języka

Komponent `LanguageSwitcher` — dropdown z flagami:
- 🇷🇺 Русский
- 🇬🇧 English

Używa `useRouter().replace()` z `next-intl/navigation` do zmiany locale bez przeładowania strony.

---

## 12. System motywów (ciemny/jasny)

### Implementacja

- **Biblioteka:** `next-themes` z `attribute="class"` (klasa CSS `.dark`)
- **Domyślny motyw:** `system` (automatycznie z ustawień systemowych)
- **Komponent:** `ThemeToggle` — przyciski Sun ☀️ / Moon 🌙

### Design Tokens (CSS Variables)

Wszystkie kolory definiowane w HSL w `globals.css`:

| Token | Jasny | Ciemny | Użycie |
|-------|-------|--------|--------|
| `--background` | `0 0% 100%` (biały) | `217 33% 5%` (granatowy) | Tło strony |
| `--foreground` | `222.2 84% 4.9%` (czarny) | `210 40% 98%` (jasny) | Tekst |
| `--primary` | `221.2 83.2% 53.3%` (niebieski) | `217.2 91.2% 59.8%` (jasny niebieski) | Przycisk, link |
| `--destructive` | `0 84.2% 60.2%` (czerwony) | `0 62.8% 30.6%` (ciemny czerw.) | Błędy, usuwanie |
| `--muted` | `210 40% 96.1%` (jasny szary) | `217.2 32.6% 17.5%` (ciemny szary) | Tło drugorzędne |
| `--border` | `214.3 31.8% 91.4%` | `217.2 32.6% 17.5%` | Ramki |
| `--radius` | `0.5rem` | `0.5rem` | Zaokrąglenia |

**Statusy projektów:**

| Status | Token | Kolor |
|--------|-------|-------|
| Active | `--status-active` | Zielony |
| Recruiting | `--status-recruiting` | Niebieski |
| Completed | `--status-completed` | Szary |
| Draft | `--status-draft` | Żółty |

**Customowe gradienty:**

| Nazwa | Kolory |
|-------|--------|
| `gradient-card-blue` | Niebieski → Fioletowy |
| `gradient-card-green` | Zielony → Niebieski |
| `gradient-card-orange` | Pomarańczowy → Czerwony |

---

## 13. Komponenty

### 13.1 Komponenty UI (shadcn/ui)

30 komponentów bazowych opartych na **Radix UI** + **TailwindCSS** + **CVA**:

| Komponent | Radix UI | Opis |
|-----------|----------|------|
| `AlertDialog` | ✅ | Dialogi potwierdzenia |
| `Avatar` | ✅ | Awatar z fallbackiem |
| `Badge` | — | Etykieta statusu |
| `Button` | — | Przycisk (warianty: default, destructive, outline, secondary, ghost, link) |
| `Card` | — | Karta z Content, Header, Footer |
| `Checkbox` | ✅ | Pole wyboru |
| `Dialog` | ✅ | Dialog modalny |
| `DropdownMenu` | ✅ | Menu rozwijane |
| `Form` | — | Integracja react-hook-form + Zod |
| `Input` | — | Pole tekstowe |
| `Label` | ✅ | Etykieta pola |
| `Loading` | — | Spinner ładowania |
| `MermaidDiagram` | — | Renderer diagramów Mermaid |
| `ScrollArea` | ✅ | Przewijany obszar |
| `Select` | ✅ | Pole wyboru (dropdown) |
| `Separator` | ✅ | Separator wizualny |
| `Skeleton` | — | Placeholder ładowania |
| `StatsCard` | — | Karta ze statystykami |
| `Switch` | ✅ | Przełącznik |
| `Table` | — | Tabela danych |
| `Tabs` | ✅ | Zakładki |
| `Textarea` | — | Pole tekstowe wieloliniowe |
| `Toast` | ✅ | Powiadomienia toast |
| `Toaster` | — | Kontener toast |
| `Tooltip` | ✅ | Podpowiedź |
| `UserAvatarCard` | — | Karta użytkownika z awatarem |

**Stories:** Storybook stories dla: Button, Card, Dialog, Input.

### 13.2 Komponenty funkcjonalne (features)

9 złożonych komponentów demonstracyjnych i produkcyjnych:

| Komponent | Linie | Opis |
|-----------|-------|------|
| `KanbanBoard` | 278 | Statyczna tablica Kanban (demo) z 4 kolumnami |
| `NotificationsCenter` | 294 | Pełne centrum powiadomień z zakładkami i filtrowaniem |
| `GitHubIntegration` | 140 | UI połączenia z repozytorium GitHub |
| `ActivityHeatmap` | 153 | Heatmapa aktywności w stylu GitHub (365 dni) |
| `CommitDiffViewer` | 229 | Przeglądarka diffów commitów (line-by-line) |
| `GitBranchGraph` | 183 | Wizualizacja gałęzi Git |
| `GitCommitTree` | 509 | Pełne drzewo commitów z grafem SVG |
| `GitStats` | 147 | Dashboard statystyk repozytorium |
| `CodeQualityInsights` | 206 | Metryki jakości kodu (pokrycie, duplikacja, bezpieczeństwo) |

### 13.3 Providery

`Providers` — wrapper opakowujący całą aplikację:

```
<SessionProvider refetchInterval={4 * 60}>
  <SessionTokenSync />
  <QueryClientProvider client={queryClient}>
    <ThemeProvider attribute="class" defaultTheme="system">
      {children}
      <Toaster />
      <ReactQueryDevtools />
    </ThemeProvider>
  </QueryClientProvider>
</SessionProvider>
```

**`SessionTokenSync`** — synchronizuje token z sesji NextAuth do in-memory store:
- `"authenticated"` → `setAccessToken(session.accessToken)`
- `"unauthenticated"` → `setAccessToken(null)`
- `session.error === "RefreshTokenError"` → `signOut({ callbackUrl: "/login" })`

### 13.4 Layout (Header, Footer)

**Header** (150 linii):
- Logo DevHunt + nazwa
- Nawigacja: Projects, Chats (warunkowa)
- Niezalogowany: Login, Register
- Zalogowany: ThemeToggle, LanguageSwitcher, User dropdown → Dashboard
- Sticky top z `backdrop-blur`

**Footer** (115 linii):
- 4-kolumnowy grid: marka, nawigacja, zasoby, social
- Linki: GitHub, Twitter, email
- Copyright ©

### 13.5 ErrorBoundary

Globalny error boundary z obsługą i18n:

```typescript
class ErrorBoundaryInner extends React.Component<Props, State> {
  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return (
        <Card>
          <CardContent>
            <p>{t("errorOccurred")}</p>
            {isDev && <pre>{this.state.error.stack}</pre>}
            <Button onClick={this.reset}>Try Again</Button>
            <Link href="/">Go Home</Link>
          </CardContent>
        </Card>
      );
    }
    return this.props.children;
  }
}
```

---

## 14. Hooki

15 custom React hooks:

| Hook | Opis |
|------|------|
| `useCurrentUser()` | Bieżący zalogowany użytkownik (z sesji NextAuth) |
| `useDebouncedValue(value, delay)` | Debounce wartości (domyślnie 300ms) |
| `useNotifications()` | Powiadomienia z API + real-time |
| `useToast()` | Wyświetlanie toast notifications |
| `useChat()` | Pełna logika czatu: wiadomości, wysyłanie, typing, SignalR |
| `useProjectTaskBoard()` | Tablica Kanban: kolumny, przeciąganie, reorderowanie |
| `useProjectTasks()` | CRUD zadań projektu |
| `useProjectTeam()` | Zarządzanie członkami zespołu |
| `useProjectDialogs()` | Stan dialogów na stronie projektu |
| `useProjectActivity()` | Feed aktywności projektu |
| `useProjectStatusActions()` | Zmiana statusu projektu z walidacją |
| `useProjectInvitations()` | Zaproszenia do projektu (wysłanie, akceptacja, odrzucenie) |
| `useProjectTaskHandlers()` | Handlery akcji na zadaniach (move, assign, delete) |
| `useProjectGallery()` | Galeria mediów projektu (upload, delete, lightbox) |
| `useGalleryLightbox()` | Lekki lightbox z nawigacją klawiaturą |

**Wzorzec hook-per-feature:** Złożone strony (np. projekt) dekomponowane na oddzielne hooki — każdy zarządza swoim kawałkiem logiki i stanu.

---

## 15. SEO i metatagi

### robots.ts

```typescript
export default function robots() {
  return {
    rules: { userAgent: '*', allow: '/' },
    sitemap: 'https://devhunt.pl/sitemap.xml',
  };
}
```

### sitemap.ts

Dynamicznie generowana mapa strony na podstawie publicznych stron.

### opengraph-image.tsx

Generowany obraz OG dla linków udostępnianych w social media.

### generateMetadata

Strona główna generuje metadata z `getTranslations()`:

```typescript
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("home");
  return {
    title: t("meta.title"),
    description: t("meta.description"),
    openGraph: { title: t("meta.title"), description: t("meta.description") },
  };
}
```

---

## 16. Testy

### 16.1 Strategia testowania

| Typ | Framework | Lokalizacja | Opis |
|-----|-----------|-------------|------|
| Unit | Vitest + Testing Library | `tests/unit/` | Testy komponentów i logiki |
| A11y | Vitest + jest-axe | `tests/a11y/` | Audyt dostępności |
| E2E (szybkie) | Playwright | `tests/e2e/` | Podstawowy test auth |
| E2E (pełne) | Playwright | `tests/e2e-full/` | 18+ specyfikacji |
| Visual | Storybook + Chromatic | `.storybook/` | Regresja wizualna |
| Type check | TypeScript | — | `tsc --noEmit` |

### 16.2 E2E Full Suite (Playwright)

```
tests/e2e-full/
├── auth/                    # Logowanie, rejestracja, OAuth
├── projects/                # CRUD projektów, szczegóły
├── tasks/                   # Tablica zadań, Kanban
├── admin/                   # Panel administracyjny
├── realtime/                # WebSocket/SignalR
├── social/                  # Interakcje społecznościowe
├── integrations/            # GitHub integration
├── edge-cases/              # Przypadki brzegowe
├── notifications-flow.spec.ts
├── profile-flow.spec.ts
├── projects-flow.spec.ts
├── projects-deep-flow.spec.ts
├── app-crawl.spec.ts        # Crawl wszystkich stron
├── page-objects/             # Page Object Model
├── fixtures/                 # Test fixtures
├── helpers.ts                # Funkcje pomocnicze
├── global-setup.ts           # Setup globalny
└── global-teardown.ts        # Teardown globalny
```

### 16.3 Konfiguracja Vitest

```typescript
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["tests/setup.ts"],
    include: ["tests/**/*.{test,spec}.{ts,tsx}"],
    exclude: ["**/e2e/**", "**/e2e-full/**"],
    testTimeout: 10_000,
    coverage: { reporter: ["text", "json", "html"], provider: "v8" },
  },
});
```

### 16.4 Konfiguracja Playwright

```typescript
export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  use: { baseURL: "http://localhost:3000" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
```

### 16.5 Uruchomienie testów

```bash
npm run test              # Vitest (watch mode)
npm run test:unit         # Testy jednostkowe
npm run test:a11y         # Testy dostępności
npm run test:e2e          # Playwright szybkie
npm run test:e2e:full     # Playwright pełna suite
npm run test:contract     # Testy kontraktowe
npm run lint              # ESLint
npm run type-check        # TypeScript
```

---

## 17. Storybook

### Konfiguracja

| Parametr | Wartość |
|----------|---------|
| Framework | React + Vite |
| Port | 6006 |
| Stories | `src/**/*.stories.*` |
| Addons | essentials, a11y, viewport, interactions |

### Dostępne stories

| Komponent | Warianty |
|-----------|----------|
| `Button` | Default, Destructive, Outline, Secondary, Ghost, Link, Sizes |
| `Card` | Default, With header/footer |
| `Dialog` | Open/close, With form |
| `Input` | Default, Disabled, Error |

### Visual Regression

- **Chromatic** — CI/CD pipeline do wykrywania zmian wizualnych
- `npm run chromatic` — upload stories do Chromatic
- `--exit-zero-on-changes` — nie failuje build przy zmianach (review mode)

---

## 18. Docker

### 18.1 Dockerfile (3-stage build)

```
Stage 1: deps (node:20-alpine)
├── npm ci --legacy-peer-deps
├── Cache: --mount=type=cache,target=/root/.npm
│
Stage 2: builder (node:20-alpine)
├── COPY node_modules from deps
├── Build-time env vars:
│   ├── DOCKER_ENV=true
│   ├── NEXT_PUBLIC_USE_PROXY=true
│   ├── NEXT_PUBLIC_API_URL=/api/proxy-core
│   └── NEXT_PUBLIC_AUTH_URL=/api/proxy-auth
├── npm run build (Next.js standalone)
│
Stage 3: runner (node:20-alpine)
├── Non-root user: nodejs (SEC-015)
├── COPY standalone + static
├── HOSTNAME=0.0.0.0, PORT=3000
├── HEALTHCHECK: wget localhost:3000
└── CMD: node server.js
```

### 18.2 Optymalizacja standalone

Next.js `output: "standalone"` generuje **minimalny zestaw plików**:
- `server.js` — serwer Node.js
- `.next/standalone/node_modules/` — tylko potrzebne moduły
- `.next/static/` — skompilowane assety

Rozmiar obrazu: ~150 MB (vs ~1 GB z pełnym `node_modules`).

### 18.3 Health Check

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget -q --spider http://localhost:3000 || exit 1
```

---

## 19. Zmienne środowiskowe

### Publiczne (NEXT_PUBLIC_*)

Dostępne w przeglądarce (osadzone w bundle JS):

| Zmienna | Domyślnie | Opis |
|---------|-----------|------|
| `NEXT_PUBLIC_API_URL` | `http://localhost:7002/api` | URL Core API |
| `NEXT_PUBLIC_AUTH_URL` | `http://localhost:7001/api` | URL Auth Service |
| `NEXT_PUBLIC_WS_URL` | `http://localhost:7002` | URL WebSocket (SignalR) |
| `NEXT_PUBLIC_USE_PROXY` | `false` | Tryb proxy (Docker) |
| `NEXT_PUBLIC_USE_MOCKS` | `false` | Tryb mock (offline dev) |
| `NEXT_PUBLIC_USE_API` | `true` | Czy używać prawdziwego API |

### Serwerowe (tylko server-side)

| Zmienna | Domyślnie | Opis |
|---------|-----------|------|
| `AUTH_SECRET` | fallback (dev only!) | Sekret sesji NextAuth |
| `NEXTAUTH_SECRET` | — | Alias dla AUTH_SECRET |
| `AUTH_SERVICE_URL` | `http://auth-service:8080` | URL Auth Service (server) |
| `CORE_SERVICE_URL` | `http://core-api:8080/api` | URL Core API (server) |
| `DOCKER_ENV` | `false` | Czy uruchomione w Docker |
| `NODE_ENV` | `development` | Środowisko Node.js |
| `NEXT_TELEMETRY_DISABLED` | `1` (Docker) | Wyłączenie telemetrii Next.js |

### Bezpieczeństwo

> ⚠️ **WAŻNE:** W produkcji `AUTH_SECRET` MUSI być ustawiony na silną losową wartość.
> Generowanie: `openssl rand -base64 32`
>
> Aplikacja wyświetli ostrzeżenie w konsoli jeśli brak sekretu w produkcji.

---

> **Poprzedni dokument:** [Auth Service](auth-service.md)  
> **Następny dokument:** [ML Service](ml-service.md)
