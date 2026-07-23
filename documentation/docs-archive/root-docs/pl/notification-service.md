# Notification Service — Dokumentacja Techniczna

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i zmienne środowiskowe](#5-konfiguracja-i-zmienne-środowiskowe)
6. [Bootstrap aplikacji (index.js)](#6-bootstrap-aplikacji-indexjs)
7. [Serwis email (emailService)](#7-serwis-email-emailservice)
8. [Serwis SMS (smsService)](#8-serwis-sms-smsservice)
9. [Serwis push (pushService)](#9-serwis-push-pushservice)
10. [System szablonów (templateRenderer)](#10-system-szablonów-templaterenderer)
11. [Konsumer zdarzeń RabbitMQ](#11-konsumer-zdarzeń-rabbitmq)
12. [Warstwa middleware](#12-warstwa-middleware)
13. [System throttlingu i backpressure](#13-system-throttlingu-i-backpressure)
14. [Metryki Prometheus](#14-metryki-prometheus)
15. [Telemetria OpenTelemetry](#15-telemetria-opentelemetry)
16. [Logowanie (Winston + OpenObserve)](#16-logowanie-winston--openobserve)
17. [Bezpieczeństwo](#17-bezpieczeństwo)
18. [Docker](#18-docker)
19. [Endpointy API — pełna referencja](#19-endpointy-api--pełna-referencja)
20. [Przepływy danych — diagramy](#20-przepływy-danych--diagramy)
21. [Obsługa błędów](#21-obsługa-błędów)
22. [Zależności i pakiety](#22-zależności-i-pakiety)

---

## 1. Przegląd systemu

**Notification Service** to mikroserwis Node.js odpowiedzialny za wysyłanie powiadomień do użytkowników platformy DevHunt za pośrednictwem trzech kanałów: **email**, **SMS** i **push notifications**.

### Główne obowiązki

| Funkcja | Opis |
|---------|------|
| **Email** | Wysyłka email przez SMTP (nodemailer) lub SendGrid API |
| **SMS** | Wysyłka wiadomości SMS przez Twilio REST API (z fallbackiem mock) |
| **Push** | Wysyłka push notifications przez FCM — Firebase Cloud Messaging (z fallbackiem mock) |
| **Szablony** | Renderowanie szablonów HTML (welcome, projectInvitation, newMessage, achievementUnlocked) |
| **Event Consumer** | Konsumpcja zdarzeń z RabbitMQ i automatyczne generowanie powiadomień |
| **Webhook alertów** | Odbiór webhooków z OpenObserve — alerting monitoringu |
| **Bulk notifications** | Wysyłka masowych powiadomień do wielu użytkowników równolegle |
| **Observability** | Prometheus metryki, OpenTelemetry tracing, logowanie Winston + OpenObserve |

### Port

Serwis nasłuchuje na porcie **5003** (`PORT` env var, domyślnie `5003`).

### Komunikacja z innymi serwisami

```
┌──────────────────┐                              ┌───────────────────┐
│    Core API      │ ── HTTP POST /api/notif. ──→ │ Notification Svc  │
│    :7002         │                              │    Port: 5003     │
└──────────────────┘                              └───────┬───────────┘
                                                          │
                    ┌─────────────────────────────────────┼────────────────────┐
                    │                                     │                    │
              ┌─────▼──────┐                       ┌──────▼───────┐     ┌──────▼──────┐
              │  RabbitMQ  │                       │ SMTP / SGrid │     │ Twilio API  │
              │   :5672    │                       │              │     │             │
              └────────────┘                       └──────────────┘     └─────────────┘
                    │                                                         │
              ┌─────▼──────┐                                           ┌──────▼──────┐
              │    Redis   │                                           │  FCM (GCP)  │
              │   :6379    │                                           │             │
              └────────────┘                                           └─────────────┘
```

---

## 2. Architektura

### Warstwy architektoniczne

```
┌─────────────────────────────────────────────────────────────────────┐
│                       Express 5.1 (HTTP)                           │
├──────────────┬──────────────┬──────────────┬───────────────────────┤
│ requestContext│ metricsMiddle│  rateLimit   │ throttle              │
├──────────────┴──────────────┴──────────────┴───────────────────────┤
│                        Endpoints                                    │
│  POST /api/notifications  │ POST /api/notifications/bulk           │
│  PUT .../read             │ PUT .../read-all                       │
│  POST /api/alerts/webhook │ GET /health, /metrics                  │
├─────────────────────────────────────────────────────────────────────┤
│                     Services Layer                                  │
│  emailService  │  smsService  │  pushService  │  templateRenderer  │
├─────────────────────────────────────────────────────────────────────┤
│                Event Consumer (RabbitMQ)                             │
│  rabbitmqConsumer — 6 routing keys (wildcard), retry ×3             │
├─────────────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                               │
│  logger (Winston + OpenObserve) │ metrics (Prometheus) │ telemetry  │
└─────────────────────────────────────────────────────────────────────┘
```

### Wzorce architektoniczne

| Wzorzec | Implementacja |
|---------|---------------|
| **Provider Pattern** | Konfigurowalny dostawca per kanał (SMTP/SendGrid, Twilio/Mock, FCM/Mock) |
| **Middleware Chain** | requestContext → metrics → rateLimit → throttle → logging |
| **Event-Driven** | RabbitMQ consumer z wildcard routing keys |
| **Template Engine** | Proste funkcje szablonów HTML z interpolacją danych |
| **Bulk Processing** | `Promise.allSettled()` dla masowych powiadomień |
| **Graceful Degradation** | Fallback do mock w development, do SMTP jeśli SendGrid nie skonfigurowany |
| **Graceful Shutdown** | SIGTERM → zamknięcie serwera HTTP |

---

## 3. Stos technologiczny

| Kategoria | Technologia | Wersja |
|-----------|-------------|--------|
| Runtime | Node.js | >= 20 |
| Framework | Express | 5.1.0 |
| Moduły | ES Modules (`"type": "module"`) | — |
| Email — SMTP | nodemailer | 7.0.11 |
| Email — SendGrid | @sendgrid/mail | 8.1.3 |
| SMS | twilio (via REST API + axios) | 5.10.6 (zadeklarowane) |
| Message Broker | amqplib | 0.10.7 |
| Cache/Rate Limiting | ioredis | 5.4.2 |
| Rate Limiter | rate-limiter-flexible | 5.0.4 |
| JWT | jsonwebtoken | 9.0.2 |
| HTTP Client | axios | 1.7.7 |
| Logging | winston | 3.15.0 |
| Metrics | prom-client | 15.1.3 |
| Tracing | @opentelemetry/sdk-node + auto-instrumentations + OTLP HTTP | — |
| CORS | cors | 2.8.5 |
| Env Config | dotenv | 17.2.3 |
| Backpressure | Shared package `packages/throttle` | — |

---

## 4. Struktura projektu

```
notification-service/
├── package.json                    # Manifest — zależności, skrypty, Node >=20
├── Dockerfile                      # node:20-alpine, healthcheck :5003
├── README.md                       # Dokumentacja (RU)
└── src/
    ├── index.js                    # Bootstrap Express — endpointy, middleware, startup
    ├── metrics.js                  # Prometheus registry — 8 metryk + handler
    ├── telemetry.js                # OpenTelemetry SDK + OTLP exporter
    ├── middleware/
    │   ├── auth.js                 # JWT authenticateToken + requireRole
    │   ├── rateLimit.js            # Redis → RateLimiterRedis + fallback Memory
    │   ├── requestContext.js       # requestId (UUID) + child logger
    │   └── throttle.js             # Wrapper nad packages/throttle
    ├── services/
    │   ├── emailService.js         # SMTP (nodemailer) + SendGrid adapter
    │   ├── smsService.js           # Twilio REST API + mock adapter
    │   ├── pushService.js          # FCM + mock adapter
    │   └── rabbitmqConsumer.js     # RabbitMQ consumer — zdarzenia powiadomień
    └── utils/
        ├── logger.js               # Winston + OpenObserve transport
        └── templateRenderer.js     # Szablony HTML email (4 szablony)
```

---

## 5. Konfiguracja i zmienne środowiskowe

### Zmienne — Email

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `EMAIL_PROVIDER` | `smtp` | Dostawca email: `smtp` lub `sendgrid` |
| `SENDGRID_API_KEY` | — | Klucz API SendGrid (wymagany jeśli provider=sendgrid) |
| `SMTP_HOST` | `smtp.gmail.com` | Host serwera SMTP |
| `SMTP_PORT` | `587` | Port SMTP (587=STARTTLS, 465=SSL) |
| `SMTP_USER` | — | Nazwa użytkownika SMTP |
| `SMTP_PASSWORD` | — | Hasło SMTP |
| `FROM_EMAIL` | `noreply@devhunt.com` | Adres nadawcy |
| `FROM_NAME` | `DevHunt` | Nazwa nadawcy |

### Zmienne — SMS

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `SMS_PROVIDER` | `twilio` | Dostawca SMS: `twilio`, `aws-sns`, lub inne |
| `TWILIO_ACCOUNT_SID` | — | Twilio Account SID |
| `TWILIO_AUTH_TOKEN` | — | Twilio Auth Token |
| `TWILIO_PHONE_NUMBER` | — | Numer telefonu Twilio (nadawca) |

### Zmienne — Push

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `PUSH_PROVIDER` | `fcm` | Dostawca push: `fcm`, `apns`, lub inne |
| `FCM_SERVER_KEY` | — | Firebase Cloud Messaging Server Key |

### Zmienne — Infrastruktura

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `PORT` | `5003` | Port serwera HTTP |
| `CORS_ALLOWED_ORIGINS` | `http://localhost:3000` | Dozwolone originy CORS (przecinki) |
| `ENVIRONMENT` / `NODE_ENV` | `development` | Środowisko |
| `JWT_SECRET` | — | Klucz JWT (wymagany w produkcji) |
| `JWT_ISSUER` | `DevHunt.AuthService` | Issuer JWT do weryfikacji |
| `RABBITMQ_URL` | `amqp://devhunt:devhunt_password@message-broker:5672` | URL brokera wiadomości |
| `RABBITMQ_ENABLED` | `true` | Włączenie/wyłączenie konsumera RabbitMQ |
| `RABBITMQ_EXCHANGE_NAME` | `devhunt.events` | Nazwa exchange |
| `NOTIFICATION_QUEUE` | `devhunt.notifications` | Nazwa kolejki |
| `RABBITMQ_MAX_RETRIES` | `3` | Max liczba prób przetworzenia wiadomości |
| `REDIS_URL` | `redis://cache-service:6379` | URL Redis (rate limiter) |
| `MAX_CONCURRENT_REQUESTS` | `50` | Max równoległych żądań (throttle) |
| `MAX_QUEUE_SIZE` | `100` | Max rozmiar kolejki throttle |
| `LOG_LEVEL` | `info` | Poziom logowania |
| `ALERT_ADMIN_EMAIL` | `SMTP_FROM` fallback | Email administratora do alertów krytycznych |

### Zmienne — Observability

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://openobserve:5080/api/default/v1/traces` | Endpoint OTLP |
| `OTEL_SERVICE_NAME` | `notification-service` | Nazwa serwisu w telemetrii |
| `OPENOBSERVE_LOG_URL` | `http://openobserve:5080/api/default/logs/_json` | Endpoint logów |
| `OPENOBSERVE_ROOT_USER` | `admin@devhunt.local` | Użytkownik OpenObserve |
| `OPENOBSERVE_ROOT_PASSWORD` | `ChangeMe123!` | Hasło OpenObserve |
| `OPENOBSERVE_LOG_ENABLED` | `false` | Włączenie transportu logów |

### Walidacja produkcyjna

W `ENVIRONMENT=production`:
- `JWT_SECRET` **musi być ustawiony** — serwis odmawia uruchomienia bez niego
- `CORS_ALLOWED_ORIGINS` **musi być zdefiniowany** — nie może zawierać `*`

---

## 6. Bootstrap aplikacji (index.js)

Plik `src/index.js` (424 linie) jest głównym punktem wejścia serwisu.

### 6.1 Inicjalizacja

1. **Import telemetrii** — `./telemetry.js` importowany jako pierwszy (instrumentuje HTTP/Express)
2. **CORS** — konfiguracja z `CORS_ALLOWED_ORIGINS`, produkcja wymaga jawnych originów
3. **Body parser** — `express.json({ limit: "5mb" })` + `urlencoded({ limit: "5mb" })`

### 6.2 Łańcuch middleware

```
requestContext → metricsMiddleware → rateLimit → throttle → request logging
```

| Middleware | Funkcja |
|-----------|---------|
| `requestContext` | UUID requestId, x-user-id stripping, child logger |
| `metricsMiddleware` | Pomiar czasu HTTP (Prometheus histogram) |
| `rateLimit` | Redis-backed limiter (100 req/60s) z fallbackiem Memory |
| `throttle` | Backpressure — max 50 aktywnych, kolejka do 100 |
| Request logging | Loguje method, path, IP |

### 6.3 Endpointy

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/notifications` | JWT | Wysyłka pojedynczego powiadomienia (email/SMS/push) |
| `POST` | `/api/notifications/bulk` | JWT | Wysyłka masowa do wielu użytkowników |
| `PUT` | `/api/notifications/:id/read` | JWT | Oznaczenie powiadomienia jako przeczytanego |
| `PUT` | `/api/notifications/user/:userId/read-all` | JWT | Oznaczenie wszystkich powiadomień użytkownika jako przeczytane |
| `POST` | `/api/alerts/webhook` | — | Webhook alertów z OpenObserve (sieć wewnętrzna) |
| `GET` | `/health` | — | Health check |
| `GET` | `/metrics` | — | Metryki Prometheus |
| `GET` | `/metrics/throttle` | — | Diagnostyka throttle |
| `GET` | `/` | — | Informacje o serwisie |

### 6.4 Startup

```javascript
const server = app.listen(PORT, () => {
  logger.info(`Notification Service running on port ${PORT}`);
  logger.info(`Email provider: ${process.env.EMAIL_PROVIDER || "SMTP"}`);

  // Start RabbitMQ consumer (jeśli RABBITMQ_ENABLED !== "false")
  if (process.env.RABBITMQ_ENABLED !== "false") {
    rabbitMQConsumer().catch(err => logger.error("Failed to start RabbitMQ consumer:", err));
  }
});
```

### 6.5 Graceful Shutdown

Na sygnał `SIGTERM`:
1. Zamknięcie serwera HTTP (`server.close()`)
2. Ustawienie `process.exitCode = 0`

---

## 7. Serwis email (emailService)

Plik `src/services/emailService.js` implementuje dwa adaptery email z automatycznym fallbackiem.

### 7.1 Architektura dostawców

```
                    sendEmail(options)
                         │
              ┌──────────▼──────────┐
              │  EMAIL_PROVIDER?     │
              └──────────┬──────────┘
                         │
          ┌──────────────┼──────────────┐
          │ "sendgrid"   │ "smtp"       │
          ▼              ▼              │
   initSendGrid()  initSMTP()          │
          │              │              │
   ┌──────▼──────┐  ┌───▼───────┐      │
   │ SENDGRID_   │  │ nodemailer│      │
   │ API_KEY     │  │ transport │      │
   │ ustawiony?  │  │           │      │
   └──────┬──────┘  └───────────┘      │
    TAK   │  NIE                        │
    ▼     └────────── fallback ─────────┘
  SendGrid API        SMTP
```

### 7.2 Konfiguracja SMTP

```javascript
nodemailer.createTransport({
  host: SMTP_HOST,       // domyślnie: smtp.gmail.com
  port: SMTP_PORT,       // domyślnie: 587
  secure: SMTP_PORT === 465,  // true dla 465 (SSL), false dla 587 (STARTTLS)
  auth: {
    user: SMTP_USER,
    pass: SMTP_PASSWORD,
  },
});
```

| Parametr | Wartość |
|----------|---------|
| Transporter | Singleton — inicjalizowany raz i reużywany |
| Format from | `"${FROM_NAME}" <${FROM_EMAIL}>` |
| Wsparcie text | Opcjonalne pole `text` oprócz `html` |

### 7.3 Konfiguracja SendGrid

```javascript
sgMail.setApiKey(SENDGRID_API_KEY);
const msg = {
  to: options.to,
  from: { email: FROM_EMAIL, name: FROM_NAME },
  subject: options.subject,
  html: renderedContent,
};
await sgMail.send(msg);
```

### 7.4 Szablony

Jeśli `options.template` jest podany, treść HTML jest generowana przez `renderTemplate()`:

```javascript
const html = options.template
  ? renderTemplate(options.template, options.data || {})
  : options.html;
```

### 7.5 Walidacja

Wymagane pola:
- `to` — adres email odbiorcy
- `subject` — temat wiadomości
- `html` lub `template` — treść (surowy HTML lub nazwa szablonu)

### 7.6 Odpowiedź

```javascript
// Sukces SMTP
{ success: true, provider: "smtp", messageId: "abc123" }

// Sukces SendGrid
{ success: true, provider: "sendgrid" }

// Błąd
{ success: false, error: "Error message", message: "Failed to send email" }
```

---

## 8. Serwis SMS (smsService)

Plik `src/services/smsService.js` implementuje wysyłkę SMS z wzorcem provider.

### 8.1 Dostawcy

| Dostawca | Status | Opis |
|----------|--------|------|
| `twilio` | ✅ Zaimplementowany | Twilio REST API via axios |
| `aws-sns` | ⚠️ Placeholder | Loguje ostrzeżenie, fallback do mock |
| inne | ⚠️ Mock | Tryb deweloperski — tylko logowanie |

### 8.2 Twilio API

```javascript
axios.post(
  `https://api.twilio.com/2010-04-01/Accounts/${TWILIO_ACCOUNT_SID}/Messages.json`,
  new URLSearchParams({
    From: TWILIO_PHONE_NUMBER,
    To: to,
    Body: message,
  }),
  {
    auth: { username: TWILIO_ACCOUNT_SID, password: TWILIO_AUTH_TOKEN },
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
  }
);
```

**Uwaga:** Twilio SDK jest zadeklarowany w `package.json` (`twilio: ^5.10.6`), ale w kodzie używany jest axios + REST API bezpośrednio. SDK nie jest importowane.

### 8.3 Walidacja numeru telefonu

```javascript
const phoneRegex = /^\+?[1-9]\d{1,14}$/;
if (!phoneRegex.test(to.replace(/\s/g, ""))) {
  return { success: false, error: "Invalid phone number format. Expected: +1234567890" };
}
```

Format: E.164 (`+1234567890`), białe znaki usuwane przed walidacją.

### 8.4 Mock mode

W trybie deweloperskim (brak credentials Twilio):

```javascript
{
  success: true,
  id: "sms_mock_1234567890",
  provider: "mock",
  sentAt: "2025-01-01T00:00:00.000Z",
  note: "SMS sent in mock mode (not actually sent)"
}
```

### 8.5 Graceful degradation

```
TWILIO credentials podane?
  TAK → Twilio REST API
  NIE → Mock mode (logowanie)

SMS_PROVIDER = "aws-sns"?
  → Warning + Mock mode

SMS_PROVIDER = nieznany?
  → Warning + Mock mode
```

---

## 9. Serwis push (pushService)

Plik `src/services/pushService.js` implementuje push notifications z wzorcem provider.

### 9.1 Dostawcy

| Dostawca | Status | Opis |
|----------|--------|------|
| `fcm` | ✅ Zaimplementowany | Firebase Cloud Messaging via HTTP API |
| `apns` | ⚠️ Placeholder | Apple Push — TODO |
| inne | ⚠️ Mock | Tryb deweloperski |

### 9.2 Firebase Cloud Messaging (FCM)

```javascript
axios.post("https://fcm.googleapis.com/fcm/send", {
  to: deviceToken,
  notification: {
    title,
    body,
    sound: "default",
  },
  data: data || {},
  priority: "high",
}, {
  headers: {
    Authorization: `key=${FCM_SERVER_KEY}`,
    "Content-Type": "application/json",
  },
});
```

| Parametr | Wartość |
|----------|---------|
| Endpoint | `https://fcm.googleapis.com/fcm/send` (legacy HTTP API) |
| Auth | `key=${FCM_SERVER_KEY}` |
| Priority | `high` |
| Sound | `default` |

### 9.3 Walidacja

Wymagane pola:
- `to` — device token (identyfikator urządzenia)
- `title` — tytuł powiadomienia
- `body` — treść powiadomienia

Opcjonalne:
- `data` — dodatkowe dane (obiekt JSON)

### 9.4 Odpowiedź

```javascript
// Sukces FCM
{
  success: true,
  id: "multicast_id lub message_id",
  provider: "fcm",
  sentAt: "2025-01-01T00:00:00.000Z"
}

// Mock
{
  success: true,
  id: "push_mock_1234567890",
  provider: "mock",
  note: "Push notification sent in mock mode (not actually sent)"
}
```

---

## 10. System szablonów (templateRenderer)

Plik `src/utils/templateRenderer.js` implementuje prosty system szablonów HTML.

### 10.1 Dostępne szablony

| Nazwa szablonu | Parametry danych | Zastosowanie |
|---------------|------------------|-------------|
| `welcome` | `userName` | Powitanie nowego użytkownika |
| `projectInvitation` | `projectName`, `userName`, `inviterName`, `projectUrl` | Zaproszenie do projektu |
| `newMessage` | `senderName`, `messageContent`, `chatUrl` | Nowa wiadomość na czacie |
| `achievementUnlocked` | `userName`, `achievementName`, `achievementDescription` | Zdobycie osiągnięcia |

### 10.2 Implementacja

Szablony są **funkcjami JavaScript** zwracającymi ciągi HTML:

```javascript
const templates = {
  welcome: (data) => `
    <html>
      <body style="font-family: Arial, sans-serif; padding: 20px;">
        <h2>Witaj w DevHunt, ${data.userName || "użytkowniku"}!</h2>
        <p>Dziękujemy za rejestrację...</p>
      </body>
    </html>
  `,
  // ...
};
```

**Uwaga:** Szablony są napisane po **polsku** (np. "Witaj w DevHunt", "Zaproszenie do projektu").

### 10.3 Renderowanie

```javascript
export function renderTemplate(templateName, data = {}) {
  const template = templates[templateName];
  if (!template) {
    logger.warn(`Template "${templateName}" not found, using default`);
    return `<html><body><p>Notification: ${JSON.stringify(data)}</p></body></html>`;
  }
  if (typeof template === "function") {
    return template(data);
  }
  return template;
}
```

Fallback: Jeśli szablon nie istnieje, generowany jest prosty HTML z surowym JSON danych.

### 10.4 Przykład szablonu `projectInvitation`

```html
<html>
  <body style="font-family: Arial, sans-serif; padding: 20px;">
    <h2>Zaproszenie do projektu: {projectName}</h2>
    <p>Cześć, {userName}!</p>
    <p>{inviterName} zaprasza Cię do dołączenia do projektu "{projectName}".</p>
    <p>
      <a href="{projectUrl}" style="background: #007bff; color: white;
         padding: 10px 20px; text-decoration: none; border-radius: 5px;">
        Zobacz projekt
      </a>
    </p>
  </body>
</html>
```

---

## 11. Konsumer zdarzeń RabbitMQ

Plik `src/services/rabbitmqConsumer.js` implementuje konsumer wiadomości z kolejki powiadomień.

### 11.1 Konfiguracja połączenia

| Parametr | Wartość |
|----------|---------|
| Exchange | `devhunt.events` (topic, durable) |
| Kolejka | `devhunt.notifications` (durable) |
| Max retry | `RABBITMQ_MAX_RETRIES` (domyślnie 3) |
| Acknowledgment | Ręczny (`noAck: false`) |

### 11.2 Routing keys (subskrypcje)

```javascript
const routingKeys = [
  "project.*",          // Wszystkie zdarzenia projektów
  "showcase.*",         // Wszystkie zdarzenia showcase
  "team.*",             // Zdarzenia zespołu
  "invitation.*",       // Zdarzenia zaproszeń
  "message.sent",       // Nowe wiadomości
  "conversation.created", // Nowe czaty
];
```

**Uwaga:** Używane są **wildcard routing keys** (np. `project.*`) w przeciwieństwie do Integration Gateway, który binduje konkretne klucze.

### 11.3 Event handlers

| EventType | Handler | Działanie |
|-----------|---------|-----------|
| `project.created` | `sendProjectNotifications()` | Email do właściciela projektu |
| `project.updated` | `sendProjectNotifications()` | Email o aktualizacji |
| `project.archived` / `project.cancelled` | `sendProjectNotifications()` | Email o zmianie statusu |
| `team.member.joined` | `sendTeamMemberNotifications()` | Email do właściciela o nowym członku |
| `team.member.removed` / `team.member.left` | `sendTeamMemberNotifications()` | Email o odejściu członka |
| `invitation.sent` | `sendInvitationNotifications()` | Email do zaproszonego użytkownika |
| `invitation.accepted` | `sendInvitationNotifications()` | Logowanie (pełna implementacja — TODO) |
| `showcase.published` | `sendShowcaseNotifications()` | Logowanie (pełna implementacja — TODO) |
| `message.sent` | `sendMessageNotifications()` | Logowanie (pełna implementacja — TODO) |

### 11.4 Wzorzec przetwarzania wiadomości

```
RabbitMQ → consume() → parse JSON → processEvent()
                                        │
                                        ▼
                              ┌── handler istnieje? ──┐
                              │ TAK                   │ NIE
                              ▼                       ▼
                         execute handler        log.debug("skip")
                              │
                         ┌── sukces? ──┐
                         │ TAK         │ NIE
                         ▼             ▼
                    channel.ack()   retry < MAX?
                    record("processed")  │ TAK         │ NIE
                                         ▼             ▼
                                   republish z      channel.ack()
                                   x-retry-count++  record("dropped")
                                   channel.ack()
                                   record("retried")
```

### 11.5 Retry logika

```javascript
const retryCount = headers["x-retry-count"] ?? 0;
if (retryCount < MAX_RETRY_COUNT) {
  channel.publish(EXCHANGE_NAME, routingKey, msg.content, {
    headers: {
      ...headers,
      "x-retry-count": retryCount + 1,
      "x-last-error": error?.message || "unknown error",
    },
    persistent: true,
  });
  channel.ack(msg);   // Zawsze ack oryginał
}
```

Nagłówek `x-last-error` zapisuje komunikat ostatniego błędu.

### 11.6 Auto-reconnect

| Zdarzenie | Opóźnienie |
|-----------|------------|
| `connection.close` | 5 sekund |
| Błąd startowy | 10 sekund |
| `connection.error` | Logowanie (reconnect przez close handler) |

### 11.7 Stan implementacji handlerów

| Handler | Status |
|---------|--------|
| `sendProjectNotifications()` | ⚠️ Częściowo — wysyła email do OwnerId z placeholder adresem |
| `sendTeamMemberNotifications()` | ⚠️ Częściowo — wysyła email z placeholder adresem |
| `sendInvitationNotifications()` | ⚠️ Częściowo — wysyła email do InviteeId |
| `sendShowcaseNotifications()` | ⚠️ Placeholder — tylko logowanie |
| `sendMessageNotifications()` | ⚠️ Placeholder — tylko logowanie |

**Uwaga:** Aktualnie używany jest wzorzec `{userId}@devhunt.local` jako placeholder email. W produkcji powinno to być zastąpione pobieraniem prawdziwych adresów email z Core API.

---

## 12. Warstwa middleware

### 12.1 Autentykacja JWT (`middleware/auth.js`)

#### `authenticateToken(req, res, next)`

1. **Skip** — ścieżki `/health` i `/` nie wymagają autentykacji
2. **Tryb deweloperski** — brak `JWT_SECRET` → `req.user = { user_id: "anonymous", role: "user" }`
3. **Produkcja** — `JWT_SECRET` wymagany, odmowa uruchomienia bez niego

Weryfikacja:
```javascript
jwt.verify(token, JWT_SECRET, {
  issuer: "DevHunt.AuthService",
  algorithms: ["HS256"],
});
```

Mapowanie `req.user`:
```javascript
req.user = {
  user_id: decoded.sub || decoded.userId,
  role: decoded.role || "user",
};
```

Obsługa błędów tokenów:
| Błąd | Kod HTTP | Komunikat |
|------|----------|-----------|
| `TokenExpiredError` | 401 | "Token expired" |
| `JsonWebTokenError` | 401 | "Invalid token" |
| Inny | 401 | "Token verification failed" |

#### `requireRole(...roles)`

Sprawdzanie roli użytkownika. Zwraca 403 jeśli rola nie pasuje.

### 12.2 Rate Limiter (`middleware/rateLimit.js`)

#### Konfiguracja

| Parametr | Zmienna env | Domyślna |
|----------|-------------|----------|
| Okno czasowe | `RATE_LIMIT_WINDOW_MS` | 60 000 ms (1 min) |
| Max żądań | `RATE_LIMIT_MAX_REQUESTS` | 100 |
| Redis URL | `RATE_LIMIT_REDIS_URL` / `REDIS_URL` | `redis://cache-service:6379` |
| Key prefix (Redis) | — | `notification_service_rl` |
| Key prefix (Memory) | — | `notification_service_rl_mem` |

#### Redis failover

```
Redis dostępny?
  TAK → RateLimiterRedis (z insuranceLimiter = RateLimiterMemory)
  NIE → RateLimiterMemory (logowanie ostrzeżenia)
```

Dodatkowa opcja Redis: `execEvenly: true` — rozkłada żądania równomiernie w oknie czasowym.

#### Klucz identyfikacji

```javascript
const ip = req.ip || req.headers["x-forwarded-for"]?.split(",")[0] || req.connection.remoteAddress;
```

#### Nagłówki odpowiedzi

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0      (przy przekroczeniu)
Retry-After: 45                (przy przekroczeniu)
```

### 12.3 Request Context (`middleware/requestContext.js`)

```javascript
export function requestContext(req, res, next) {
  const requestId = req.headers["x-request-id"] || randomUUID();
  delete req.headers["x-user-id"];   // Security: never trust caller-supplied user ID
  const userId = req.user?.user_id ?? req.user?.id ?? req.user?.sub ?? null;

  req.requestId = requestId;
  req.log = logger.child({ requestId, userId });
  res.setHeader("x-request-id", requestId);
  next();
}
```

### 12.4 Throttle (`middleware/throttle.js`)

Wrapper nad wspólnym pakietem `packages/throttle`:

```javascript
const throttleInstance = createThrottle({
  logger,
  maxConcurrent: parseInt(process.env.MAX_CONCURRENT_REQUESTS || "50"),
  maxQueueSize: parseInt(process.env.MAX_QUEUE_SIZE || "100"),
  cpuThreshold: parseFloat(process.env.CPU_THRESHOLD || "0.8"),
  memoryThreshold: parseFloat(process.env.MEMORY_THRESHOLD || "0.9"),
  metricsRefreshMs: parseInt(process.env.SYSTEM_METRICS_REFRESH_MS || "2000"),
});
```

| Parametr | Integration GW | Notification Svc |
|----------|---------------|------------------|
| maxConcurrent | 100 | **50** |
| maxQueueSize | 200 | **100** |

Notification Service ma **niższe limity** throttle (50/100 vs 100/200) — mniejszy wolumen HTTP, więcej czasu na wysyłkę email/SMS.

---

## 13. System throttlingu i backpressure

Identyczna implementacja co w Integration Gateway — współdzielony pakiet `packages/throttle/index.js`.

### 13.1 Algorytm

1. **System overloaded?** (CPU > 80% lub RAM > 90%) → 503
2. **Aktywne żądania < maxConcurrent?** → Przetwarzaj natychmiast
3. **Kolejka < maxQueueSize?** → Dodaj do kolejki
4. **Kolejka pełna?** → 503 (`retryAfter: 10`)

### 13.2 Timeout żądań w kolejce

30 sekund — po tym czasie żądanie jest automatycznie zakończone.

### 13.3 Diagnostyka

Endpoint `GET /metrics/throttle`:

```json
{
  "activeRequests": 12,
  "queueLength": 0,
  "maxConcurrentRequests": 50,
  "maxQueueSize": 100,
  "cpuLoad": 0.25,
  "memoryUsage": 0.55,
  "isOverloaded": false
}
```

---

## 14. Metryki Prometheus

Plik `src/metrics.js` definiuje 8 metryk z dedykowanym rejestrem.

### 14.1 Lista metryk

| Metryka | Typ | Labels | Opis |
|---------|-----|--------|------|
| `notification_service_http_request_duration_seconds` | Histogram | method, route, status_code | Czas trwania żądań HTTP |
| `notification_service_http_requests_total` | Counter | method, route, status_code | Łączna liczba żądań HTTP |
| `notification_service_notifications_total` | Counter | channel, status | Powiadomienia wg kanału i statusu |
| `notification_service_notification_duration_seconds` | Histogram | channel, status | Czas wysyłki powiadomienia |
| `notification_service_rabbitmq_connection_state` | Gauge | — | Stan połączenia RabbitMQ (0/1) |
| `notification_service_event_messages_total` | Counter | routing_key, status | Zdarzenia z RabbitMQ wg statusu |
| `notification_service_throttle_active_requests` | Gauge | — | Aktywne żądania throttle |
| `notification_service_throttle_queue_length` | Gauge | — | Długość kolejki throttle |
| `notification_service_throttle_overloaded` | Gauge | — | Flaga przeciążenia (0/1) |

### 14.2 Metryki specyficzne dla powiadomień

W przeciwieństwie do Integration Gateway, Notification Service ma **dedykowane metryki powiadomień**:

#### `notification_service_notifications_total`

Zlicza powiadomienia wg kanału (`email`, `sms`, `push`) i statusu (`success`, `failure`):

```javascript
function recordNotificationAttempt(channel, status, count = 1) {
  notificationsTotal.inc({ channel, status }, count);
}
```

#### `notification_service_notification_duration_seconds`

Mierzy czas wysyłki powiadomienia:

```javascript
function observeNotificationLatency(channel, status, seconds) {
  notificationLatency.labels(channel, status).observe(seconds);
}
```

Buckety: `[0.1, 0.25, 0.5, 1, 2, 5, 10]` sekund.

### 14.3 Prefiks metryk

Wszystkie metryki mają prefiks `notification_service_` z domyślnym labelem `service: "notification-service"`.

### 14.4 Default metrics

Zbierane automatycznie: metryki procesu Node.js (CPU, memory, event loop, GC, V8 heap).

---

## 15. Telemetria OpenTelemetry

Identyczna konfiguracja co w Integration Gateway.

### 15.1 Konfiguracja

```javascript
const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    "service.name": "notification-service",
    "service.namespace": "devhunt",
    "service.environment": process.env.ENVIRONMENT || "development",
  }),
  spanProcessor: new BatchSpanProcessor(traceExporter),
  instrumentations: [
    getNodeAutoInstrumentations({
      "@opentelemetry/instrumentation-http": {
        enabled: true,
        ignoreOutgoingPaths: [/\/metrics/],
      },
      "@opentelemetry/instrumentation-express": {
        enabled: true,
      },
    }),
  ],
});
```

### 15.2 Exporter

| Parametr | Wartość |
|----------|---------|
| Typ | OTLP HTTP |
| Endpoint | `OTEL_EXPORTER_OTLP_ENDPOINT` (domyślnie OpenObserve) |
| Processor | `BatchSpanProcessor` |

### 15.3 Auto-instrumentacja

- HTTP — wszystkie żądania (oprócz `/metrics`)
- Express — middleware i route handlers

---

## 16. Logowanie (Winston + OpenObserve)

Identyczna architektura co w Integration Gateway.

### 16.1 Transporty

| Transport | Cel | Format |
|-----------|-----|--------|
| `Console` | stdout/stderr (Docker) | JSON ze znacznikiem czasu |
| `OpenObserveTransport` | Centralizowane logowanie | JSON via HTTP POST |

### 16.2 OpenObserve Transport

- **Asynchroniczny** — `setImmediate(callback)` nie blokuje
- **Fire & forget** — błędy HTTP do OpenObserve ignorowane
- **Timeout** — 2000 ms
- **Auth** — Basic (base64)
- **Włączenie** — `OPENOBSERVE_LOG_ENABLED=true`

### 16.3 Child logger

Tworzony w `requestContext` middleware:

```javascript
req.log = logger.child({ requestId, userId });
```

---

## 17. Bezpieczeństwo

### 17.1 Mechanizmy bezpieczeństwa

| Mechanizm | Implementacja |
|-----------|---------------|
| **JWT Auth** | HS256, issuer `DevHunt.AuthService`, wymagany w produkcji |
| **JWT_SECRET validation** | Serwis odmawia startu w produkcji bez JWT_SECRET |
| **X-User-Id stripping** | Middleware kasuje nagłówek `x-user-id` z żądań |
| **CORS** | Konfigurowalny, produkcja wymaga jawnych originów, odrzuca `*` |
| **Rate Limiting** | Redis-backed, 100 req/60s z fallbackiem Memory |
| **Throttle** | Max 50 aktywnych żądań, kolejka 100, CPU/RAM monitoring |
| **Owner check** | `read-all` — użytkownik może oznaczyć tylko własne powiadomienia |
| **Body Size Limit** | 5 MB |
| **Alert webhook** | Bez auth — zakłada dostęp z sieci wewnętrznej |

### 17.2 Kontrola dostępu — read-all

```javascript
app.put("/api/notifications/user/:userId/read-all", authenticateToken, (req, res) => {
  if (req.user.user_id !== req.params.userId && req.user.role !== "admin") {
    return res.status(403).json({ error: "Access denied" });
  }
  // ...
});
```

Tylko właściciel powiadomień lub administrator może oznaczyć wszystkie jako przeczytane.

---

## 18. Docker

### 18.1 Dockerfile

```dockerfile
FROM node:20-alpine
WORKDIR /app

COPY notification-service/package*.json ./
RUN npm install --omit=dev

COPY packages ./packages
COPY notification-service/. .

EXPOSE 5003
USER node

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD node -e "require('http').get('http://localhost:5003/health', (r) => {
    process.exit(r.statusCode === 200 ? 0 : 1)
  })"

CMD ["node", "src/index.js"]
```

### 18.2 Health check

Endpoint `GET /health`:

```json
{
  "status": "healthy",
  "service": "notification-service",
  "version": "1.0.0",
  "timestamp": "2025-01-01T00:00:00.000Z"
}
```

### 18.3 docker-compose

```yaml
notification-service:
  build:
    context: .
    dockerfile: notification-service/Dockerfile
  ports:
    - "5003:5003"
  environment:
    - RABBITMQ_URL=amqp://devhunt:devhunt_password@message-broker:5672
    - REDIS_URL=redis://cache-service:6379
  depends_on:
    - message-broker
    - cache-service
```

---

## 19. Endpointy API — pełna referencja

### 19.1 Wysyłka powiadomień

#### `POST /api/notifications`

Wysyła pojedyncze powiadomienie przez wybrany kanał.

**Auth:** JWT (Bearer token)

**Request Body:**
```json
{
  "userId": "guid-user-id",
  "type": "email",
  "recipient": "user@example.com",
  "subject": "Welcome",
  "title": "Welcome to DevHunt",
  "content": "<h1>Hello</h1>",
  "template": "welcome",
  "data": { "userName": "John" },
  "config": {
    "phoneNumber": "+1234567890",
    "deviceToken": "fcm-token-here"
  }
}
```

**Kompatybilność z Core API** — pola mogą być w PascalCase (`UserId`, `Type`, `Title`, `Content`) lub camelCase.

**Dispatch kanałów:**

| Type | Wymagane | Adapter |
|------|----------|---------|
| `email` | `recipient` (email) | emailService → SMTP/SendGrid |
| `sms` | `recipient` lub `config.phoneNumber` | smsService → Twilio |
| `push` | `recipient` lub `config.deviceToken` | pushService → FCM |

**Response (sukces):**
```json
{
  "success": true,
  "notificationId": "1234567890",
  "type": "email",
  "sentAt": "2025-01-01T00:00:00.000Z"
}
```

**Response (błąd):**
```json
{
  "success": false,
  "error": "Missing required fields: UserId (or userId), Type (or type)"
}
```

#### `POST /api/notifications/bulk`

Wysyłka masowa do wielu użytkowników via email.

**Auth:** JWT

**Request Body:**
```json
{
  "userIds": ["guid-1", "guid-2", "guid-3"],
  "type": "email",
  "title": "System Update",
  "content": "<p>Important update...</p>"
}
```

**Response:**
```json
{
  "success": true,
  "total": 3,
  "successful": 2,
  "failed": 1,
  "sentAt": "2025-01-01T00:00:00.000Z"
}
```

Używa `Promise.allSettled()` — nie przerywa na błędach.

### 19.2 Zarządzanie powiadomieniami

#### `PUT /api/notifications/:id/read`

Oznacza powiadomienie jako przeczytane.

**Auth:** JWT

**Response:**
```json
{
  "success": true,
  "notificationId": "123",
  "readAt": "2025-01-01T00:00:00.000Z"
}
```

⚠️ **Aktualnie placeholder** — nie ma faktycznej logiki bazy danych.

#### `PUT /api/notifications/user/:userId/read-all`

Oznacza wszystkie powiadomienia użytkownika jako przeczytane.

**Auth:** JWT + owner check (user_id === userId || role === admin)

**Response:**
```json
{
  "success": true,
  "userId": "guid",
  "readAt": "2025-01-01T00:00:00.000Z"
}
```

### 19.3 Webhook alertów

#### `POST /api/alerts/webhook`

Odbiór alertów z OpenObserve. Bez autentykacji (zakłada sieć wewnętrzną).

**Request Body (OpenObserve alert format):**
```json
{
  "alert_name": "High Error Rate",
  "severity": "critical",
  "stream_name": "logs",
  "alert_message": "Error rate exceeded threshold",
  "alert_start_time": "2025-01-01T00:00:00.000Z"
}
```

**Logika:**
1. Loguje alert jako `logger.warn`
2. Jeśli severity = `critical` lub `high` → wysyła email do `ALERT_ADMIN_EMAIL`

**Response:**
```json
{
  "success": true,
  "received": true,
  "timestamp": "2025-01-01T00:00:00.000Z"
}
```

### 19.4 System

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/health` | — | Health check |
| `GET` | `/metrics` | — | Metryki Prometheus |
| `GET` | `/metrics/throttle` | — | Diagnostyka throttle |
| `GET` | `/` | — | Informacje o serwisie |

---

## 20. Przepływy danych — diagramy

### 20.1 Wysyłka email z API

```
Frontend ── POST /api/notifications ──→ Notification Service
                                              │
                                    [1] authenticateToken()
                                    [2] Normalize body (PascalCase/camelCase)
                                    [3] Validate fields
                                              │
                                    type = "email"
                                              │
                                    [4] sendEmail({ to, subject, html/template, data })
                                              │
                              ┌───────────────┼───────────────┐
                              │               │               │
                        template?     EMAIL_PROVIDER?     Validation
                              │               │
                    renderTemplate()    ┌──────┴──────┐
                              │        │ "sendgrid"   │ "smtp"
                              ▼        ▼              ▼
                         HTML content  SendGrid API   nodemailer
                                              │
                                    [5] recordNotificationAttempt()
                                    [6] observeNotificationLatency()
                                              │
                                    Response: { success, notificationId }
```

### 20.2 Zdarzenie RabbitMQ → Powiadomienie

```
Core API ── publish("invitation.sent") ──→ RabbitMQ
                                               │
                              devhunt.notifications queue
                              (bound: invitation.*)
                                               │
                                        rabbitmqConsumer
                                               │
                                     processEvent()
                                               │
                              EventType = "invitation.sent"
                                               │
                              sendInvitationNotifications()
                                               │
                                     sendEmail({
                                       to: `${InviteeId}@devhunt.local`,
                                       subject: "New project invitation",
                                       html: "..."
                                     })
                                               │
                                   SMTP / SendGrid
```

### 20.3 Alert OpenObserve → Email administratora

```
OpenObserve ── POST /api/alerts/webhook ──→ Notification Service
                                                   │
                                    [1] Log alert as warning
                                    [2] severity === "critical" || "high"?
                                                   │ TAK
                                    [3] sendEmail({
                                          to: ALERT_ADMIN_EMAIL,
                                          subject: "[CRITICAL] DevHunt Alert: ...",
                                          html: "<h2>DevHunt Monitoring Alert</h2>..."
                                        })
```

### 20.4 Bulk notifications

```
Core API ── POST /api/notifications/bulk ──→ Notification Service
                                                   │
                                    [1] authenticateToken()
                                    [2] Validate UserIds array
                                                   │
                                    [3] Promise.allSettled(
                                          userIds.map(userId =>
                                            sendEmail({
                                              to: `${userId}@devhunt.local`,
                                              subject: title,
                                              html: content,
                                            })
                                          )
                                        )
                                                   │
                                    [4] Count successful / failed
                                    [5] recordNotificationAttempt() × (success + failure)
                                                   │
                                    Response: { success, total, successful, failed }
```

---

## 21. Obsługa błędów

### 21.1 Wzorce obsługi

| Kontekst | Strategia |
|----------|-----------|
| Wysyłka email (SMTP/SendGrid) | Try/catch → zwrot `{ success: false, error }` |
| Wysyłka SMS (Twilio) | Try/catch → zwrot `{ success: false, error }` |
| Wysyłka push (FCM) | Try/catch → zwrot `{ success: false, error }` |
| Brak credentials | Graceful degradation do **mock mode** |
| RabbitMQ events | Retry do 3 razy (`x-retry-count`), potem drop z metryką |
| Bulk notifications | `Promise.allSettled()` — nie przerywa na błędach |
| Alert webhook | Try/catch → 500, ale alert jest logowany nawet przy błędzie email |

### 21.2 Kody HTTP

| Kod | Kiedy |
|-----|-------|
| 200 | Sukces / powiadomienie wysłane |
| 400 | Brak wymaganych pól / nieprawidłowy typ / brak numeru telefonu / brak device token |
| 401 | Brak lub nieprawidłowy JWT |
| 403 | Brak dostępu (read-all obcych powiadomień) |
| 429 | Rate limit exceeded |
| 500 | Błąd wysyłki / wewnętrzny błąd serwera |
| 503 | System overloaded / queue full (throttle) |

### 21.3 Fallback providers

| Kanał | Primary | Fallback |
|-------|---------|----------|
| Email | SendGrid API | SMTP (jeśli SendGrid nie skonfigurowany) |
| SMS | Twilio REST API | Mock (jeśli credentials brak) |
| Push | FCM HTTP API | Mock (jeśli FCM_SERVER_KEY brak) |

---

## 22. Zależności i pakiety

### 22.1 Zależności runtime (17)

| Pakiet | Wersja | Zastosowanie |
|--------|--------|-------------|
| `express` | ^5.1.0 | Framework HTTP |
| `axios` | ^1.7.7 | Klient HTTP (Twilio REST, FCM, OpenObserve) |
| `amqplib` | ^0.10.7 | Klient RabbitMQ (AMQP) |
| `ioredis` | ^5.4.2 | Klient Redis (rate limiter) |
| `rate-limiter-flexible` | ^5.0.4 | Rate limiting |
| `jsonwebtoken` | ^9.0.2 | Weryfikacja JWT |
| `nodemailer` | ^7.0.11 | Wysyłka email SMTP |
| `@sendgrid/mail` | ^8.1.3 | Wysyłka email SendGrid API |
| `twilio` | ^5.10.6 | ⚠️ Zadeklarowane, ale nieużywane (używany axios + REST) |
| `cors` | ^2.8.5 | CORS middleware |
| `dotenv` | ^17.2.3 | Zmienne środowiskowe |
| `winston` | ^3.15.0 | Logowanie strukturyzowane |
| `prom-client` | ^15.1.3 | Metryki Prometheus |
| `@opentelemetry/sdk-node` | ^0.208.0 | OpenTelemetry SDK |
| `@opentelemetry/auto-instrumentations-node` | ^0.67.0 | Auto-instrumentacja |
| `@opentelemetry/exporter-trace-otlp-http` | ^0.208.0 | OTLP HTTP exporter |
| `@opentelemetry/resources` | ^2.2.0 | Atrybuty zasobów |

**Dodatkowe pakiety OpenTelemetry:**
- `@opentelemetry/api` ^1.9.0
- `@opentelemetry/sdk-trace-base` ^2.2.0
- `@opentelemetry/semantic-conventions` ^1.38.0

### 22.2 Zależności deweloperskie (1)

| Pakiet | Wersja | Zastosowanie |
|--------|--------|-------------|
| `@types/node` | ^24.10.1 | Definicje typów Node.js |

### 22.3 Pakiety wspólne

| Pakiet | Ścieżka | Zastosowanie |
|--------|---------|-------------|
| `throttle` | `packages/throttle/index.js` | Middleware backpressure |

### 22.4 Skrypty npm

| Skrypt | Komenda | Opis |
|--------|---------|------|
| `start` | `node src/index.js` | Uruchomienie produkcyjne |
| `dev` | `node --watch src/index.js` | Hot reload (Node.js `--watch`) |
| `test` | `node --test` | Testy (wbudowany runner) |

### 22.5 Wymagania runtime

```json
{
  "engines": {
    "node": ">=20.0.0"
  }
}
```

---

*Dokument wygenerowany na podstawie analizy kodu źródłowego Notification Service — DevHunt Platform.*
*Wersja: 1.0 | Data: 2025*
