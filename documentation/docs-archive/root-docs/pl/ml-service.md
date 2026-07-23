# ML Service — Dokumentacja Techniczna

> **Serwis:** DevHunt ML Service  
> **Technologia:** Python 3.11, FastAPI 0.115+, Uvicorn 0.38  
> **Port:** 8000  
> **Repozytorium:** `ml-service/`  
> **Wersja:** 1.0.0

---

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i zmienne środowiskowe](#5-konfiguracja-i-zmienne-środowiskowe)
6. [Punkt wejścia — main.py](#6-punkt-wejścia--mainpy)
7. [API — endpointy AI](#7-api--endpointy-ai)
8. [API — endpointy rekomendacji](#8-api--endpointy-rekomendacji)
9. [API — paszport projektu](#9-api--paszport-projektu)
10. [System dostawców AI](#10-system-dostawców-ai)
11. [Klient Groq](#11-klient-groq)
12. [Serwis AI — logika biznesowa](#12-serwis-ai--logika-biznesowa)
13. [System promptów](#13-system-promptów)
14. [Chat agentowy z Function Calling](#14-chat-agentowy-z-function-calling)
15. [Definicje narzędzi (Tools)](#15-definicje-narzędzi-tools)
16. [Resolver narzędzi](#16-resolver-narzędzi)
17. [Konsument zdarzeń RabbitMQ](#17-konsument-zdarzeń-rabbitmq)
18. [Cache — Redis](#18-cache--redis)
19. [Modele Pydantic](#19-modele-pydantic)
20. [Bezpieczeństwo — JWT](#20-bezpieczeństwo--jwt)
21. [Obserwowalność — metryki i tracing](#21-obserwowalność--metryki-i-tracing)
22. [Docker](#22-docker)
23. [Testy](#23-testy)
24. [Zależności](#24-zależności)
25. [Diagramy przepływu](#25-diagramy-przepływu)

---

## 1. Przegląd systemu

ML Service to mikroserwis odpowiedzialny za wszystkie operacje związane ze sztuczną inteligencją w platformie DevHunt. Serwis realizuje następujące funkcje:

| Funkcja | Opis |
|---------|------|
| **Generowanie tech stacku** | Propozycja 3 wariantów stosu technologicznego na podstawie opisu projektu |
| **Planowanie projektu** | Generowanie faz, zadań i zależności z AI-asystentem |
| **Udoskonalanie planów** | Iteracyjna modyfikacja planu/stacku na podstawie instrukcji użytkownika |
| **Generowanie diagramów** | Mermaid/PlantUML diagramy (architektura, sekwencja, ERD, deployment, user flow, state) |
| **Chat AI** | Konwersacja z modelem AI w kontekście projektu (historia, sliding window) |
| **Chat agentowy** | Function Calling z 14 narzędziami — zarządzanie zadaniami, projekt, treści |
| **Rekomendacje projektów** | Algorytm scoringu 5-czynnikowego z cache'em Redis |
| **Paszport projektu** | Kompleksowy dokument techniczny projektu (5 sekcji) |
| **Konsumpcja zdarzeń** | Reakcja na zdarzenia RabbitMQ (profil, projekty, showcase) |

### Kluczowe cechy

- **Dwóch dostawców AI**: Groq API (główny, 7 aliasów modeli) + Google Gemini (fallback)
- **Łańcuch fallbacków**: `smart → versatile → smart_fallback → scout → fast`
- **Cache dwupoziomowy**: odpowiedzi AI (10 min TTL) + rekomendacje (1h TTL)
- **Obserwabilność**: OpenTelemetry (tracing + logi → OpenObserve) + Prometheus (11 metryk)
- **Bezpieczeństwo**: JWT HS256 z walidacją issuer/expiration, SEC-014 w produkcji

---

## 2. Architektura

```
┌──────────────────────────────────────────────────────────────────┐
│                        Frontend (Next.js)                        │
│              /api/ai/*   /api/recommendations/*                  │
└──────────────────────────────┬───────────────────────────────────┘
                               │ HTTP (JWT Bearer)
                               ▼
┌──────────────────────────────────────────────────────────────────┐
│                    ML Service (FastAPI :8000)                     │
│                                                                  │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │  AI Router  │  │ Recommend.   │  │  Passport Router       │  │
│  │  /api/ai/*  │  │ Router       │  │  /api/ai/generate-     │  │
│  │  13 endp.   │  │ 4 endp.      │  │  passport              │  │
│  └──────┬──────┘  └──────┬───────┘  └───────────┬────────────┘  │
│         │                │                      │                │
│  ┌──────▼──────────────────────────────────────▼──────────────┐  │
│  │                   AI Service Layer                          │  │
│  │  • Zarządzanie promptami (template rendering)               │  │
│  │  • Detekcja typu projektu                                   │  │
│  │  • Budowanie sekcji (goals, constraints, scale)             │  │
│  │  • Parsowanie JSON z odpowiedzi AI                          │  │
│  └──────────────────────────┬────────────────────────────────┘  │
│                              │                                   │
│  ┌──────────────────────────▼────────────────────────────────┐  │
│  │              Provider Layer (switchable)                    │  │
│  │  ┌─────────────────┐    ┌──────────────────────────────┐  │  │
│  │  │   Groq Client   │    │   Google Gemini (fallback)   │  │  │
│  │  │   7 modeli       │    │   gemini-2.0-flash          │  │  │
│  │  │   HTTPX async    │    │   google-generativeai SDK   │  │  │
│  │  └─────────────────┘    └──────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌─────────────────┐  ┌──────────┐  ┌────────────────────────┐  │
│  │  Event Consumer │  │ AI Cache │  │  Tools (14 definicji)  │  │
│  │  (RabbitMQ)     │  │ (Redis)  │  │  + Resolver            │  │
│  └────────┬────────┘  └─────┬────┘  └────────────────────────┘  │
│           │                 │                                    │
└───────────┼─────────────────┼────────────────────────────────────┘
            │                 │
     ┌──────▼────┐     ┌──────▼──────┐     ┌───────────────┐
     │ RabbitMQ  │     │    Redis    │     │  PostgreSQL   │
     │   :5672   │     │   :6379    │     │    :5432      │
     └───────────┘     └────────────┘     └───────────────┘
```

### Przepływ danych

1. **Request AI** → Frontend wysyła żądanie HTTP z JWT → Router waliduje token → AI Service buduje prompt → Groq/Gemini generuje odpowiedź → Cache zapisuje → Response
2. **Rekomendacje** → JWT auth → Sprawdzenie cache Redis → Jeśli miss: zapytanie SQL do PostgreSQL → Algorytm scoringu → Cache store → Response  
3. **Zdarzenia** → RabbitMQ publikuje event → Event Consumer reaguje (inwalidacja cache, aktualizacja ratingu)
4. **Agent Chat** → Wiadomość + kontekst → Groq z tool calling → Pętla query tools (max 3) → Odpowiedź z tool calls

---

## 3. Stos technologiczny

### Runtime i framework

| Komponent | Technologia | Wersja |
|-----------|-------------|--------|
| Język | Python | 3.11 |
| Framework webowy | FastAPI | 0.115 / 0.124 |
| Serwer ASGI | Uvicorn | 0.38 |
| Walidacja danych | Pydantic | 2.9.2 |
| Menedżer zależności | Poetry | 1.7.1 |

### Dostawcy AI

| Dostawca | SDK / Klient | Rola |
|----------|-------------|------|
| Groq API | HTTPX (natywny HTTP) | Główny — 7 aliasów modeli |
| Google Gemini | `google-generativeai` 0.8.3 | Fallback — `gemini-2.0-flash` |

### Bazy danych i cache

| Komponent | Technologia | Biblioteka |
|-----------|-------------|------------|
| PostgreSQL | 16 | AsyncPG 0.30 |
| Redis | 7 | redis-py 7.1 (async) |
| RabbitMQ | 3 | aio-pika 9.4.1 |

### Obserwabilność

| Komponent | Technologia |
|-----------|-------------|
| Tracing | OpenTelemetry SDK 1.27 → OpenObserve (OTLP HTTP) |
| Logi | OpenTelemetry Logging → OpenObserve |
| Metryki | Prometheus Client 0.21 (scraping `/metrics`) |

### Bezpieczeństwo

| Komponent | Technologia |
|-----------|-------------|
| JWT | PyJWT 2.10, algorytm HS256 |
| CORS | FastAPI CORSMiddleware |

---

## 4. Struktura projektu

```
ml-service/
├── main.py                          # Punkt wejścia FastAPI, lifecycle, middleware
├── deps.py                          # Fabryki zależności (DB pool, Redis)
├── security.py                      # JWT auth, verify_token dependency
├── metrics.py                       # Definicje metryk Prometheus (11 metryk)
│
├── routers/
│   ├── ai.py                        # 13 endpointów AI (1058 linii)
│   ├── recommendations.py           # 4 endpointy rekomendacji (527 linii)
│   └── passport.py                  # 1 endpoint paszportu (260 linii)
│
├── services/
│   ├── ai_service.py                # Logika AI: prompty, parsowanie, call_model (665 linii)
│   └── ai_cache.py                  # Redis cache dla odpowiedzi AI (85 linii)
│
├── clients/
│   └── groq_client.py               # Klient Groq API z fallback chain (468 linii)
│
├── consumers/
│   └── event_consumer.py            # Konsument RabbitMQ (261 linii)
│
├── models/
│   └── ai_models.py                 # ~20 modeli Pydantic (396 linii)
│
├── tools/
│   ├── __init__.py                  # Eksport modułu
│   ├── definitions.py               # 14 definicji narzędzi OpenAI Function Calling (594 linii)
│   └── resolver.py                  # Server-side resolver narzędzi query (252 linie)
│
├── prompts/
│   ├── plan/
│   │   ├── v1.prompt.txt            # Szablon generowania planu (109 linii)
│   │   └── v1.refine.prompt.txt     # Szablon udoskonalania planu (51 linii)
│   └── techstack/
│       ├── v1.prompt.txt            # Szablon generowania tech stacku (110 linii)
│       └── v1.refine.prompt.txt     # Szablon udoskonalania tech stacku (52 linii)
│
├── tests/
│   ├── conftest.py                  # Fixtury pytest (path setup)
│   ├── test_ai_service.py           # 34 testy AI service (340 linii)
│   ├── test_ai_cache.py             # 10 testów cache (130 linii)
│   └── test_groq_client.py          # 15 testów Groq client (239 linii)
│
├── pyproject.toml                   # Konfiguracja Poetry
├── requirements.txt                 # Pinned dependencies (fallback pip)
├── Dockerfile                       # Multi-stage Docker build
└── README.md                        # Opis serwisu (rosyjski)
```

### Statystyki kodu źródłowego

| Plik | Linie | Złożoność |
|------|-------|-----------|
| `routers/ai.py` | 1058 | Wysoka — 13 endpointów, sanityzacja Mermaid, logika agenta |
| `services/ai_service.py` | 665 | Średnia — zarządzanie promptami, detekcja typu |
| `tools/definitions.py` | 594 | Niska — deklaratywne definicje narzędzi |
| `routers/recommendations.py` | 527 | Średnia — algorytm scoringu, cache Redis |
| `clients/groq_client.py` | 468 | Wysoka — fallback chain, obsługa rate limit |
| `models/ai_models.py` | 396 | Niska — modele Pydantic |
| `main.py` | 299 | Średnia — lifecycle, middleware, konfiguracja |

---

## 5. Konfiguracja i zmienne środowiskowe

### Zmienne obowiązkowe

| Zmienna | Opis | Wymagana w produkcji |
|---------|------|---------------------|
| `DATABASE_URL` | Connection string PostgreSQL | ✅ |
| `JWT_SECRET` | Klucz do weryfikacji JWT (min. 32 znaki — SEC-014) | ✅ |
| `GROQ_API_KEY` | Klucz API Groq | ✅ |
| `REDIS_URL` | URL Redis | ✅ |
| `RABBITMQ_URL` | URL RabbitMQ (AMQP) | ✅ |

### Zmienne opcjonalne

| Zmienna | Opis | Wartość domyślna |
|---------|------|-----------------|
| `JWT_ISSUER` | Issuer tokena JWT | `DevHunt.AuthService` |
| `GEMINI_API_KEY` | Klucz API Google Gemini (fallback) | — |
| `AI_PROVIDER` | Główny dostawca AI | `groq` |
| `CORS_ALLOWED_ORIGINS` | Dozwolone origins (oddzielone przecinkiem) | `*` |
| `LOG_LEVEL` | Poziom logowania | `INFO` |
| `RABBITMQ_EXCHANGE_NAME` | Nazwa exchange RabbitMQ | `devhunt.events` |
| `ML_QUEUE` | Nazwa kolejki ML | `devhunt.ml.recommendations` |
| `OTEL_SERVICE_NAME` | Nazwa serwisu w OpenTelemetry | `ml-service` |
| `OTEL_EXPORTER_OPENOBSERVE_ENDPOINT` | Endpoint OpenObserve OTLP | `http://openobserve:5080/...` |
| `OPENOBSERVE_USER` | Użytkownik OpenObserve | — |
| `OPENOBSERVE_PASSWORD` | Hasło OpenObserve | — |

### Walidacja konfiguracji

```python
# security.py — SEC-014
JWT_SECRET = os.getenv("JWT_SECRET", "")
if not JWT_SECRET:
    JWT_SECRET = "dev-secret-key-change-in-production"
    logger.warning("⚠️ JWT_SECRET not set! Using insecure default.")

# Produkcja wymaga min. 32 znaków
if os.getenv("ENVIRONMENT") == "production" and len(JWT_SECRET) < 32:
    raise ValueError("SEC-014: JWT_SECRET must be at least 32 characters in production")
```

### Konfiguracja CORS

```python
# main.py — produkcja odrzuca wildcard
origins_env = os.getenv("CORS_ALLOWED_ORIGINS", "*")
if origins_env == "*":
    logger.warning("CORS: wildcard (*) allowed — not recommended for production")
    origins = ["*"]
else:
    origins = [o.strip() for o in origins_env.split(",")]
```

---

## 6. Punkt wejścia — main.py

### Inicjalizacja aplikacji

```python
app = FastAPI(
    title="DevHunt ML Service",
    version="1.0.0",
    description="ML & AI Service for DevHunt platform"
)
```

### OpenTelemetry Setup

Konfiguracja tracingu i logowania do OpenObserve:

```python
resource = Resource.create({"service.name": OTEL_SERVICE_NAME})

# Tracing → OTLP HTTP → OpenObserve
trace_provider = TracerProvider(resource=resource)
trace_provider.add_span_processor(
    BatchSpanProcessor(
        OTLPSpanExporter(
            endpoint=f"{OTEL_ENDPOINT}/v1/traces",
            headers={"Authorization": f"Basic {credentials}"}
        )
    )
)

# Logging → OTLP HTTP → OpenObserve
log_provider = LoggerProvider(resource=resource)
log_provider.add_log_record_processor(
    BatchLogRecordProcessor(
        OTLPLogExporter(
            endpoint=f"{OTEL_ENDPOINT}/v1/logs",
            headers={"Authorization": f"Basic {credentials}"}
        )
    )
)
```

### Middleware

1. **CORSMiddleware** — obsługa cross-origin z konfigurowalnymi origins
2. **HTTP Metrics Middleware** — Prometheus REQUEST_COUNTER + REQUEST_LATENCY

```python
@app.middleware("http")
async def http_metrics(request: Request, call_next):
    start = time.time()
    response = await call_next(request)
    duration = time.time() - start
    REQUEST_COUNTER.labels(
        method=request.method,
        endpoint=request.url.path,
        status=response.status_code
    ).inc()
    REQUEST_LATENCY.labels(
        method=request.method,
        endpoint=request.url.path
    ).observe(duration)
    return response
```

### Lifecycle Events

#### Startup

```python
@app.on_event("startup")
async def startup_event():
    # 1. Inicjalizacja Redis
    redis_client = await get_redis_client()
    ai_cache.init(redis_client)
    
    # 2. Start konsumenta RabbitMQ (jako asyncio.Task)
    asyncio.create_task(event_consumer.start())
```

#### Shutdown

```python
@app.on_event("shutdown")
async def shutdown_event():
    await event_consumer.stop()       # Zamknij RabbitMQ
    pool = getattr(app.state, 'db_pool', None)
    if pool:
        await pool.close()           # Zamknij pulę PostgreSQL
    redis = getattr(app.state, 'redis_client', None)
    if redis:
        await redis.close()          # Zamknij Redis
```

### Endpointy systemowe

| Endpoint | Metoda | Opis |
|----------|--------|------|
| `GET /` | — | Informacje o serwisie (nazwa, wersja, lista endpointów) |
| `GET /health` | — | Health check — weryfikacja połączenia DB |
| `GET /metrics` | — | Metryki Prometheus (text/plain) |

#### Health Check

```python
@app.get("/health")
async def health_check():
    try:
        pool = await get_db_pool(app)
        async with pool.acquire() as conn:
            await conn.fetchval("SELECT 1")
        return {"status": "healthy", "database": "connected"}
    except Exception as e:
        return JSONResponse(
            status_code=503,
            content={"status": "degraded", "database": str(e)}
        )
```

### Routery

```python
app.include_router(ai_router)              # /api/ai/*
app.include_router(recommendations_router)  # /api/recommendations/*
app.include_router(passport_router)         # /api/ai/generate-passport
```

---

## 7. API — endpointy AI

**Plik:** `routers/ai.py` (1058 linii)  
**Prefix:** `/api/ai`  
**Tag:** `AI Planning`

### Przegląd endpointów

| Endpoint | Metoda | Opis | Auth |
|----------|--------|------|------|
| `/api/ai/models` | GET | Lista dostępnych modeli Groq + Gemini | ❌ |
| `/api/ai/generate-tech-stack` | POST | Generowanie 3 wariantów tech stacku | ❌ |
| `/api/ai/generate-plan` | POST | Generowanie planu projektu (fazy + zadania) | ❌ |
| `/api/ai/refine-plan` | POST | Udoskonalanie planu na podstawie instrukcji | ❌ |
| `/api/ai/refine-tech-stack` | POST | Udoskonalanie tech stacku | ❌ |
| `/api/ai/generate-diagram` | POST | Generowanie diagramu Mermaid/PlantUML | ❌ |
| `/api/ai/chat` | POST | Chat AI z historią (sliding window 10 msg) | ❌ |
| `/api/ai/chat-agent` | POST | Chat agentowy z Function Calling | ❌ |
| `/api/ai/tools` | GET | Lista dostępnych narzędzi z info wyświetlania | ❌ |
| `/api/ai/tech-stack` | POST | Legacy — generowanie tech stacku | ❌ |
| `/api/ai/roadmap` | POST | Legacy — generowanie roadmapy | ❌ |
| `/api/ai/tasks` | POST | Legacy — generowanie zadań | ❌ |

### 7.1 Generowanie tech stacku

**POST** `/api/ai/generate-tech-stack`

**Request body** (`TechStackRequest`):
```json
{
  "idea": "Social platform for developers",
  "goals": {
    "performance": true,
    "cost": false,
    "developerSpeed": true,
    "scalability": true,
    "security": false,
    "maintainability": true
  }
}
```

**Response** (`TechStackResponse`):
```json
{
  "options": [
    {
      "name": "Node.js + React Full Stack",
      "description": "Nowoczesny stos JavaScript...",
      "pros": ["Szybki development", "Duży ekosystem"],
      "cons": ["Callback hell", "Brak strict typing"],
      "architecture": "Monolith → Microservices",
      "backend": ["Node.js", "Express", "TypeScript"],
      "frontend": ["React", "TailwindCSS"],
      "database": ["PostgreSQL", "Redis"],
      "components": ["Docker", "Nginx"],
      "why_this_fits": "Idealny dla szybkiego MVP..."
    }
  ],
  "usage": {
    "promptTokens": 1200,
    "completionTokens": 800,
    "totalTokens": 2000,
    "model": "openai/gpt-oss-120b"
  }
}
```

**Przepływ:**
1. Walidacja `TechStackRequest` (Pydantic)
2. Budowanie promptu: `load_prompt("techstack/v1")` + sekcje goals/constraints/scale/project_type
3. Renderowanie szablonu z `{{idea}}`, `{{goals_section}}`, etc.
4. `call_model(prompt)` → cache check → Groq `generate_json()` → cache store
5. Parsowanie JSON → walidacja `TechStackResponse`
6. Return z informacją o użyciu tokenów

### 7.2 Generowanie planu

**POST** `/api/ai/generate-plan`

**Request body** (`PlanRequest`):
```json
{
  "idea": "Social platform for developers",
  "techStack": "Node.js + React + PostgreSQL",
  "customTags": ["auth", "real-time"],
  "customRoles": ["backend", "frontend", "devops"],
  "goals": {
    "performance": true,
    "scalability": true
  }
}
```

**Response** (`PlanDraft`):
```json
{
  "phases": [
    {
      "name": "Faza 1: Fundament",
      "description": "Setup projektu i podstawowa infrastruktura",
      "tasks": [
        {
          "id": "task-001",
          "title": "Konfiguracja repozytorium i CI/CD",
          "description": "Inicjalizacja Git repo...",
          "priority": "high",
          "tags": ["devops", "setup"],
          "dependsOn": []
        }
      ]
    }
  ],
  "usage": { "promptTokens": 1500, "completionTokens": 2000 }
}
```

### 7.3 Udoskonalanie planu

**POST** `/api/ai/refine-plan`

**Request body** (`RefinePlanRequest`):
```json
{
  "idea": "Social platform for developers",
  "techStack": "Node.js + React + PostgreSQL",
  "currentPlan": { "phases": [...] },
  "instructions": "Dodaj fazę testowania i deployment. Rozbij duże zadania na mniejsze."
}
```

System zachowuje niezmienione części planu, aktualizuje `dependsOn` i przydziela unikalne ID nowym zadaniom.

### 7.4 Generowanie diagramów

**POST** `/api/ai/generate-diagram`

**Request body** (`DiagramRequest`):
```json
{
  "techStack": "Node.js, PostgreSQL, Redis",
  "idea": "E-commerce platform",
  "format": "mermaid",
  "diagramType": "architecture",
  "projectContext": "Microservices architecture with 3 APIs"
}
```

**Wspierane typy diagramów:**

| Typ | Opis | Format Mermaid |
|-----|------|---------------|
| `architecture` | Architektura systemu — komponenty, relacje, porty | `flowchart TD` |
| `sequence` | Sekwencja interakcji między komponentami | `sequenceDiagram` |
| `erd` | Diagram relacji encji (bazy danych) | `erDiagram` |
| `user_flow` | Przepływ użytkownika — scenariusze UX | `flowchart LR` |
| `deployment` | Infrastruktura wdrożenia — Docker, K8s, cloud | `flowchart TD` |
| `state` | Maszyna stanów — cykl życia obiektu | `stateDiagram-v2` |

**Sanityzacja Mermaid:**

Funkcja `_sanitize_mermaid_code()` naprawia typowe problemy generowane przez AI:

```python
def _sanitize_mermaid_code(code: str) -> str:
    # 1. Zamień słowa zastrzeżone
    replacements = {
        r'\bend\b': 'finish',      # "end" → "finish"
        r'\bstart\b': 'begin',     # "start" → "begin"  
    }
    
    # 2. Napraw brakujące nazwy atrybutów w ERD
    # np. "string" → "string name" (wymagane przez parser Mermaid)
    
    # 3. Zamień nawiasy okrągłe w nawiasach kwadratowych
    # np. "[text (info)]" → "[text info]"
    
    return sanitized_code
```

### 7.5 Chat AI

**POST** `/api/ai/chat`

**Request body** (`ChatRequest`):
```json
{
  "message": "Jak zoptymalizować zapytania PostgreSQL?",
  "history": [
    { "role": "user", "content": "Buduję platformę społecznościową" },
    { "role": "assistant", "content": "Świetny pomysł! Rozważmy..." }
  ],
  "context": "Tech stack: Node.js, PostgreSQL, Redis"
}
```

**Mechanizm sliding window:**
- Maksymalnie 10 ostatnich wiadomości z historii
- Kontekst projektu dodawany jako system message

### 7.6 Chat agentowy (Function Calling)

Szczegółowy opis w [sekcji 14](#14-chat-agentowy-z-function-calling).

### 7.7 Endpointy diagnostyczne

**GET** `/api/ai/models`

Zwraca listę dostępnych modeli od obu dostawców:

```json
{
  "groq": {
    "available": true,
    "models": {
      "smart": "openai/gpt-oss-120b",
      "fast": "llama-3.1-8b-instant",
      "versatile": "llama-3.3-70b-versatile"
    }
  },
  "gemini": {
    "available": true,
    "models": ["gemini-2.0-flash"]
  }
}
```

**GET** `/api/ai/tools`

Zwraca 14 definicji narzędzi z metadanymi wyświetlania (ikony, etykiety, kategorie).

---

## 8. API — endpointy rekomendacji

**Plik:** `routers/recommendations.py` (527 linii)  
**Tag:** `Recommendations`

### Przegląd endpointów

| Endpoint | Metoda | Opis | Auth |
|----------|--------|------|------|
| `/api/recommendations/generate` | POST | Generowanie rekomendacji dla użytkownika | ✅ JWT |
| `/api/recommendations/user/{user_id}` | GET | Pobranie cached rekomendacji | ❌ |
| `/api/recommendations/refresh` | POST | Bulk refresh dla wszystkich aktywnych użytkowników | ✅ Admin |
| `/api/recommendations/cache/{user_id}` | DELETE | Inwalidacja cache (PERF-008) | ❌ |

### Algorytm scoringu

System używa 5-czynnikowego algorytmu ważonego:

| Czynnik | Waga | Opis |
|---------|------|------|
| **Skill Match** | 40% | Dopasowanie umiejętności użytkownika do tech stacku projektu |
| **Difficulty** | 20% | Preferencja odpowiedniego poziomu trudności |
| **Rating** | 20% | Ocena projektu (recenzje, gwiazdki) |
| **Team Size** | 10% | Preferencja odpowiedniego rozmiaru zespołu |
| **Featured** | 10% | Bonus za promowane projekty |

```python
def calculate_score(user_profile, project) -> float:
    skill_score = len(matching_skills) / max(len(user_skills), 1)
    difficulty_score = 1.0 - abs(user_level - project_difficulty) / 5.0
    rating_score = project.avg_rating / 5.0
    team_score = 1.0 if min_team <= user_team_pref <= max_team else 0.5
    featured_score = 1.0 if project.is_featured else 0.0
    
    return (skill_score * 0.4 + difficulty_score * 0.2 + 
            rating_score * 0.2 + team_score * 0.1 + 
            featured_score * 0.1)
```

### Zapytania SQL

#### Profil użytkownika

```sql
-- USER_PROFILE_QUERY
SELECT u.*, 
       array_agg(DISTINCT s."Name") as skills,
       u."ExperienceLevel" as level
FROM "Users" u
LEFT JOIN "UserSkills" us ON u."Id" = us."UserId"
LEFT JOIN "Skills" s ON us."SkillId" = s."Id"
WHERE u."Id" = $1
GROUP BY u."Id"
```

#### Kandydaci projektów

```sql
-- PROJECTS_QUERY  
SELECT p.*,
       array_agg(DISTINCT pts."Technology") as tech_stack,
       array_agg(DISTINCT pr."RoleName") as roles,
       COUNT(DISTINCT tm."UserId") as team_size,
       AVG(r."Rating") as avg_rating
FROM "Projects" p
LEFT JOIN "ProjectTechStacks" pts ON p."Id" = pts."ProjectId"
LEFT JOIN "ProjectRoles" pr ON p."Id" = pr."ProjectId"
LEFT JOIN "TeamMembers" tm ON p."Id" = tm."ProjectId"
LEFT JOIN "Reviews" r ON p."Id" = r."ProjectId"
WHERE p."Status" = 'recruiting'
  AND p."Visibility" = 'public'
GROUP BY p."Id"
HAVING COUNT(DISTINCT tm."UserId") < 20
LIMIT 100
```

### Cache Redis

- **Klucz:** `recommendations:user:{user_id}:limit:{limit}`
- **TTL:** 3600 sekund (1 godzina)
- **Inwalidacja:** DELETE `/api/recommendations/cache/{user_id}` (PERF-008)

### POST /api/recommendations/generate

```python
@router.post("/api/recommendations/generate")
async def generate_recommendations(
    request: Request,
    body: RecommendationRequest,
    user = Depends(verify_token)  # JWT required
):
    # 1. Sprawdź cache
    cached = await get_cached_recommendations(user["user_id"], body.limit)
    if cached:
        return cached
    
    # 2. Pobierz profil użytkownika z DB
    profile = await fetch_user_profile(pool, user["user_id"])
    
    # 3. Pobierz kandydatów (max 100 projektów)
    projects = await fetch_project_candidates(pool)
    
    # 4. Oblicz score dla każdego projektu
    scored = [(p, calculate_score(profile, p)) for p in projects]
    
    # 5. Sortuj i ogranicz
    top = sorted(scored, key=lambda x: x[1], reverse=True)[:body.limit]
    
    # 6. Cache i zwróć
    await cache_recommendations(user["user_id"], body.limit, top)
    return {"recommendations": top}
```

### POST /api/recommendations/refresh (Admin)

Masowy refresh rekomendacji dla wszystkich aktywnych użytkowników:

```python
@router.post("/api/recommendations/refresh")
async def refresh_all_recommendations(user = Depends(verify_token)):
    if user["role"] != "admin":
        raise HTTPException(403, "Admin access required")
    
    # Pobierz do 1000 aktywnych użytkowników
    users = await fetch_active_users(pool, limit=1000)
    
    for u in users:
        await invalidate_cache(u["Id"])
        await generate_for_user(u["Id"])
    
    return {"refreshed": len(users)}
```

---

## 9. API — paszport projektu

**Plik:** `routers/passport.py` (260 linii)  
**Prefix:** `/api/ai`  
**Tag:** `AI Passport`

### POST /api/ai/generate-passport

Generuje kompleksowy dokument techniczny projektu składający się z 5 sekcji.

**Request body** (`PassportRequest`):
```json
{
  "idea": "Social platform for developers",
  "techStack": "Node.js, React, PostgreSQL, Redis, Docker",
  "description": "Platform where developers find projects and teams",
  "phases": [
    {
      "name": "MVP",
      "description": "Core features: auth, projects, search"
    }
  ]
}
```

**Response** (`PassportResponse`):
```json
{
  "sections": [
    {
      "title": "Project Overview",
      "content": "DevHunt to platforma społecznościowa..."
    },
    {
      "title": "Technology Stack",
      "content": "### Backend\n- **Node.js** — ..."
    },
    {
      "title": "Architecture",
      "content": "```mermaid\nflowchart TD\n  A[Client] --> B[API Gateway]\n```"
    },
    {
      "title": "Roadmap",
      "content": "### Phase 1: MVP\n- Cel: ..."
    },
    {
      "title": "Key Decisions",
      "content": "1. **PostgreSQL nad MongoDB** — ..."
    }
  ],
  "usage": { "promptTokens": 2000, "completionTokens": 3000 }
}
```

### Specyfikacje sekcji

| Sekcja | Tytuł | Wymagania |
|--------|-------|-----------|
| `overview` | Project Overview | 150–250 słów, jasne podsumowanie projektu |
| `tech_stack` | Technology Stack | Strukturalna lista z wyjaśnieniem DLACZEGO każda technologia |
| `architecture` | Architecture | Diagram Mermaid flowchart + opis komponentów |
| `roadmap` | Roadmap | Cele, deliverables i milestones per faza |
| `decisions` | Key Decisions | 5–8 kluczowych decyzji architektonicznych z uzasadnieniem |

### Sanityzacja Mermaid w Markdown

Sekcja `architecture` zawiera osadzony diagram Mermaid. System wyszukuje bloki ```` ```mermaid ``` ```` w markdown i stosuje `_sanitize_mermaid_code()` do każdego znalezionego bloku.

---

## 10. System dostawców AI

### Architektura dostawców

```
                    ┌─────────────────────────┐
                    │      AI Service         │
                    │   call_model(prompt)     │
                    └───────────┬─────────────┘
                                │
                    ┌───────────▼─────────────┐
                    │     Cache Check          │
                    │   (Redis SHA-256)        │
                    └───────────┬─────────────┘
                         HIT?  │
                    ┌──────────┴──────────┐
                    │                     │
                   YES                   NO
                    │                     │
              ┌─────▼──────┐    ┌─────────▼──────────┐
              │ Return      │    │  Provider Selection │
              │ Cached      │    │  (groq / gemini)    │
              └────────────┘    └─────────┬──────────┘
                                          │
                              ┌───────────▼──────────┐
                              │    GROQ (primary)     │
                              │                       │
                              │  smart (gpt-oss-120b) │
                              │     ↓ fallback        │
                              │  versatile (llama-3.3)│
                              │     ↓ fallback        │
                              │  smart_fallback (qwen)│
                              │     ↓ fallback        │
                              │  scout (llama-4)      │
                              │     ↓ fallback        │
                              │  fast (llama-3.1-8b)  │
                              └───────────┬──────────┘
                                          │ ALL FAILED?
                              ┌───────────▼──────────┐
                              │   GEMINI (fallback)   │
                              │   gemini-2.0-flash    │
                              └──────────────────────┘
```

### Konfiguracja dostawców

```python
# services/ai_service.py
AI_PROVIDER = os.getenv("AI_PROVIDER", "groq")

# Groq — zawsze dostępny
groq_client = GroqClient(api_key=os.getenv("GROQ_API_KEY"))

# Gemini — opcjonalny fallback
try:
    import google.generativeai as genai
    GEMINI_KEY = os.getenv("GEMINI_API_KEY")
    if GEMINI_KEY:
        genai.configure(api_key=GEMINI_KEY)
        gemini_model = genai.GenerativeModel("gemini-2.0-flash")
except ImportError:
    gemini_model = None
```

### Funkcja call_model()

```python
async def call_model(prompt: str, mode: str = "json") -> tuple[dict, dict]:
    # 1. Sprawdź cache Redis
    cached = await ai_cache.get(prompt)
    if cached:
        AI_CACHE_HITS.inc()
        return cached
    
    AI_CACHE_MISSES.inc()
    
    # 2. Wywołaj model (Groq lub Gemini)
    start = time.time()
    
    if AI_PROVIDER == "groq":
        result, usage = await groq_client.generate_json(prompt, model="smart")
    else:
        response = gemini_model.generate_content(prompt)
        result = parse_json_response(response.text)
        usage = {}
    
    # 3. Metryki
    duration = time.time() - start
    AI_MODEL_LATENCY.labels(model=usage.get("model", "unknown")).observe(duration)
    emit_token_metrics(usage, "call_model", usage.get("model", "unknown"))
    
    # 4. Cache store
    await ai_cache.set(prompt, result, usage)
    
    return result, usage
```

---

## 11. Klient Groq

**Plik:** `clients/groq_client.py` (468 linii)

### Konfiguracja klienta

- **Protokół:** HTTPX async (bezpośrednie HTTP, bez SDK)
- **Base URL:** `https://api.groq.com/openai/v1`
- **Timeout:** 60 sekund
- **Auth:** Bearer token (nagłówek `Authorization`)

### Mapa modeli (7 aliasów)

| Alias | Pełna nazwa modelu | Przeznaczenie |
|-------|-------------------|---------------|
| `smart` | `openai/gpt-oss-120b` | Główny model — najwyższa jakość |
| `fast` | `llama-3.1-8b-instant` | Szybkie odpowiedzi, niski koszt |
| `versatile` | `llama-3.3-70b-versatile` | Fallback #1 — dobra jakość/szybkość |
| `compound` | `groq/compound-mini` | Złożone zapytania |
| `smart_fallback` | `qwen/qwen3-32b` | Fallback #2 |
| `scout` | `meta-llama/llama-4-scout-17b-16e-instruct` | Fallback #3 |
| `kimi` | `moonshotai/kimi-k2-instruct` | Specjalistyczne zapytania |

### Łańcuch fallbacków

```python
FALLBACK_CHAIN = {
    "smart": ["versatile", "smart_fallback", "scout", "fast"],
    "versatile": ["smart_fallback", "scout", "fast"],
    "smart_fallback": ["scout", "fast"],
    "scout": ["fast"],
    # "fast" — brak fallbacków (końcowy)
}
```

### Metody klienta

#### generate(prompt, model, temperature, max_tokens)

Generowanie tekstu — pojedyncze wywołanie completion:

```python
async def generate(
    self,
    prompt: str,
    model: str = "smart",
    temperature: float = 0.7,
    max_tokens: int = 4096
) -> tuple[str, dict]:
    model_id = self.MODELS.get(model, model)
    payload = {
        "model": model_id,
        "messages": [{"role": "user", "content": prompt}],
        "temperature": temperature,
        "max_tokens": max_tokens,
    }
    response = await self._client.post("/chat/completions", json=payload)
    # ... obsługa odpowiedzi, usage extraction
    return text, usage_info
```

#### generate_json(prompt, model, max_retries=3)

Generowanie JSON z retries i fallback chain:

```python
async def generate_json(self, prompt: str, model: str = "smart", max_retries: int = 3):
    models_to_try = self._get_models_to_try(model)
    
    for current_model in models_to_try:
        for attempt in range(max_retries):
            try:
                text, usage = await self.generate(prompt, model=current_model)
                parsed = self._clean_and_parse_json(text)
                return parsed, usage
            except json.JSONDecodeError:
                if attempt == max_retries - 1:
                    break  # Przejdź do następnego modelu
                continue
            except GroqRateLimitError:
                AI_RATE_LIMITS.inc()
                if self._on_rate_limit:
                    self._on_rate_limit(current_model)
                break  # Przejdź do następnego modelu
    
    raise GroqJSONParseError(f"All models failed after {max_retries} retries each")
```

#### chat(messages, model, context, language)

Chat z historią wiadomości:

```python
async def chat(
    self,
    messages: list[dict],
    model: str = "smart",
    context: str | None = None,
    language: str = "en"
) -> tuple[str, dict]:
    system_msg = f"You are a helpful AI assistant. Respond in {language}."
    if context:
        system_msg += f"\n\nProject context:\n{context}"
    
    full_messages = [{"role": "system", "content": system_msg}] + messages
    # ... wywołanie API
```

#### chat_with_tools(messages, tools, model, tool_choice)

Chat z Function Calling (OpenAI format):

```python
async def chat_with_tools(
    self,
    messages: list[dict],
    tools: list[dict],
    model: str = "smart",
    tool_choice: str = "auto"
) -> tuple[str | None, list[dict] | None, dict]:
    payload = {
        "model": self.MODELS.get(model, model),
        "messages": messages,
        "tools": tools,
        "tool_choice": tool_choice,
        "temperature": 0.3,  # Niższa temperatura dla tools
    }
    # ... wywołanie, parsowanie tool_calls
    return text, parsed_tool_calls, usage
```

### Parsowanie JSON z odpowiedzi AI

```python
@staticmethod
def _clean_and_parse_json(text: str) -> dict:
    # 1. Usuń markdown wrappers: ```json ... ```
    text = re.sub(r'^```(?:json)?\s*\n?', '', text.strip())
    text = re.sub(r'\n?```\s*$', '', text)
    
    # 2. Spróbuj bezpośredni JSON.parse
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        pass
    
    # 3. Znajdź pierwszy { lub [ i ostatni } lub ]
    start = min(text.find('{'), text.find('['))
    end = max(text.rfind('}'), text.rfind(']'))
    if start != -1 and end != -1:
        return json.loads(text[start:end+1])
    
    raise json.JSONDecodeError("No JSON found", text, 0)
```

### Wyjątki

```python
class GroqRateLimitError(Exception):
    """Raised when Groq API returns 429 Too Many Requests."""
    pass

class GroqJSONParseError(Exception):
    """Raised when all models fail to produce valid JSON after retries."""
    pass
```

---

## 12. Serwis AI — logika biznesowa

**Plik:** `services/ai_service.py` (665 linii)

### Zarządzanie promptami

#### Ładowanie szablonów

```python
_prompt_cache: dict[tuple, str] = {}

def load_prompt(category: str, version: str = "v1", variant: str = "prompt") -> str:
    """Ładuje prompt z pliku prompts/{category}/{version}.{variant}.txt"""
    cache_key = (category, version, variant)
    if cache_key in _prompt_cache:
        return _prompt_cache[cache_key]
    
    path = Path(__file__).parent.parent / "prompts" / category / f"{version}.{variant}.txt"
    template = path.read_text(encoding="utf-8")
    _prompt_cache[cache_key] = template
    return template
```

#### Renderowanie szablonów

```python
def render_prompt(template: str, **kwargs) -> str:
    """Zamień {{placeholder}} na wartości z kwargs."""
    for key, value in kwargs.items():
        template = template.replace(f"{{{{{key}}}}}", str(value))
    return template
```

### Detekcja typu projektu

Automatyczna klasyfikacja na podstawie opisu projektu:

```python
PROJECT_TYPE_KEYWORDS = {
    "mobile": ["mobile", "ios", "android", "react native", "flutter", "мобильн"],
    "data-science": ["machine learning", "ml", "data science", "neural", "prediction"],
    "game": ["game", "unity", "unreal", "godot", "multiplayer game"],
    "desktop": ["desktop", "electron", "qt", "wpf", "gtk"],
    "iot": ["iot", "sensor", "arduino", "raspberry", "embedded"],
    "cli": ["cli", "command line", "terminal", "утилита", "парсинг"],
}

def _detect_project_type(idea: str | None) -> str:
    if not idea:
        return "web"
    idea_lower = idea.lower()
    for ptype, keywords in PROJECT_TYPE_KEYWORDS.items():
        if any(kw in idea_lower for kw in keywords):
            return ptype
    return "web"  # domyślnie
```

### Budowanie sekcji promptu

#### Goals Section

```python
GOAL_MAP = {
    "performance": "Performance — prioritize fast execution, low latency",
    "cost": "Cost efficiency — prefer free/low-cost solutions",
    "developerSpeed": "Developer speed — tools that maximize productivity",
    "scalability": "Scalability — design for growth",
    "security": "Security — protection against common threats",
    "maintainability": "Maintainability — clean code, good DX",
}

def build_goals_section(goals: TechStackGoals | None) -> str:
    if not goals:
        return ""
    active = [desc for flag, desc in GOAL_MAP.items() if getattr(goals, flag, False)]
    if not active:
        return ""
    return "\n\nIMPORTANT GOALS:\n" + "\n".join(f"- {g}" for g in active)
```

#### Constraints Section

```python
FREE_TIER_KEYWORDS = ["free", "бесплатн", "без бюджета", "no budget", "open source"]

def build_constraints_section(idea: str) -> str:
    idea_lower = idea.lower()
    if any(kw in idea_lower for kw in FREE_TIER_KEYWORDS):
        return "\n\nCONSTRAINTS:\n- User requires free/open-source tools only"
    return ""
```

#### Scale Section

```python
def build_scale_section(idea: str) -> str:
    word_count = len(idea.split())
    char_count = len(idea)
    
    if word_count > 200 or char_count > 1500:
        return ("\n\nSCALE: The idea is very detailed (200+ words). "
                "Decompose EVERY mentioned feature into subtasks.")
    elif word_count > 80:
        return ("\n\nSCALE: The idea is moderately detailed. "
                "Cover all mentioned features with good granularity.")
    else:
        return ("\n\nSCALE: The idea is brief. "
                "Infer common features and create a comprehensive plan.")
```

### Parsowanie JSON z odpowiedzi AI

```python
def parse_json_response(raw: str) -> dict:
    if not raw or not raw.strip():
        raise ValueError("Empty AI response")
    
    text = raw.strip()
    
    # Usuń markdown wrappers
    text = re.sub(r'^```(?:json)?\s*\n?', '', text)
    text = re.sub(r'\n?```\s*$', '', text)
    
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        pass
    
    # Znajdź zakres JSON
    start = text.find('{')
    end = text.rfind('}')
    if start != -1 and end > start:
        return json.loads(text[start:end+1])
    
    raise json.JSONDecodeError("No JSON found", text, 0)
```

### Funkcje generowania

#### generate_tech_stack_draft

```python
async def generate_tech_stack_draft(request: TechStackRequest) -> tuple[dict, dict]:
    template = load_prompt("techstack", "v1", "prompt")
    
    goals_section = build_goals_section(request.goals)
    constraints_section = build_constraints_section(request.idea)
    scale_section = build_scale_section(request.idea)
    project_type_section = build_project_type_section(request.idea, request.goals)
    
    prompt = render_prompt(
        template,
        idea=request.idea,
        goals_section=goals_section,
        constraints_section=constraints_section,
        scale_section=scale_section,
        project_type_section=project_type_section,
    )
    
    return await call_model(prompt)
```

#### generate_plan_draft

```python
async def generate_plan_draft(request: PlanRequest) -> tuple[dict, dict]:
    template = load_prompt("plan", "v1", "prompt")
    
    goals_section = build_goals_section(request.goals)
    custom_section = _build_custom_section(request.customTags, request.customRoles)
    scale_section = build_scale_section(request.idea)
    
    prompt = render_prompt(
        template,
        idea=request.idea,
        tech_stack=request.techStack,
        goals_section=goals_section,
        custom_section=custom_section,
        scale_section=scale_section,
    )
    
    return await call_model(prompt)
```

#### refine_plan_draft / refine_tech_stack_draft

Udoskonalanie istniejącego planu/stacku:

```python
async def refine_plan_draft(request: RefinePlanRequest) -> tuple[dict, dict]:
    template = load_prompt("plan", "v1", "refine")
    
    prompt = render_prompt(
        template,
        idea=request.idea,
        tech_stack=request.techStack,
        current_plan=json.dumps(request.currentPlan, indent=2),
        instructions=request.instructions,
    )
    
    return await call_model(prompt)
```

---

## 13. System promptów

**Katalog:** `prompts/`

### Architektura promptów

```
prompts/
├── plan/
│   ├── v1.prompt.txt        # Generowanie planu
│   └── v1.refine.prompt.txt # Udoskonalanie planu
└── techstack/
    ├── v1.prompt.txt        # Generowanie tech stacku
    └── v1.refine.prompt.txt # Udoskonalanie tech stacku
```

### Konwencja nazewnictwa

- **Format:** `{version}.{variant}.txt`
- **version:** `v1`, `v2`, ... (wersjonowanie promptów)
- **variant:** `prompt` (generowanie), `refine` (udoskonalanie)
- **Placeholders:** `{{variable_name}}` — zastępowane przez `render_prompt()`

### Plan Prompt (v1.prompt.txt)

**Długość:** 109 linii

**Kluczowe reguły:**

1. **Ekstrakcja cech** — AI analizuje 7 obszarów:
   - Core features (podstawowe funkcje)
   - User management (zarządzanie użytkownikami)
   - Data management (zarządzanie danymi)
   - Integration points (punkty integracji)
   - UI/UX requirements (wymagania interfejsu)
   - Testing & QA (testowanie)
   - Deployment & DevOps (wdrożenie)

2. **Granulacja zadań:**
   - Każde zadanie: 2–8 godzin pracy
   - Rozbijaj duże zadania
   - Minimum 30 zadań dla nietrywialnych projektów

3. **Nazewnictwo faz:**
   - Używaj czytelnych nazw (nie „Phase 1")
   - Np. „Foundation & Setup", „Core Features", „Polish & Launch"

4. **Zależności (dependsOn):**
   - Każde zadanie może zależeć od 0–N innych zadań
   - System respektuje graf zależności

5. **Świadomość celów:**
   - Prompt dynamicznie uwzględnia `{{goals_section}}`, `{{constraints_section}}`, `{{scale_section}}`

### Plan Refine Prompt (v1.refine.prompt.txt)

**Długość:** 51 linii

**Reguły udoskonalania:**

1. Zachowaj niezmienione części planu
2. Aktualizuj `dependsOn` jeśli zmienią się ID
3. Przydzielaj unikalne ID nowym zadaniom
4. Nigdy nie usuwaj zadań bez wyraźnej instrukcji

### Tech Stack Prompt (v1.prompt.txt)

**Długość:** 110 linii

**Kluczowe reguły:**

1. **Nazewnictwo:** Nazwa opcji MUSI zawierać technologię (nie ogólne „Full Stack")
2. **Wytyczne platform-specific:**
   - Mobile: React Native / Flutter / Swift / Kotlin
   - Game: Unity / Godot / Unreal
   - IoT: MQTT, low-power, embedded
   - CLI: argument parsing, piping
3. **Wytyczne bazodanowe:**
   - PostgreSQL preferowany na produkcji
   - MongoDB dopuszczalny dla prototypów
   - Redis dla cache/sessions
4. **Architektura:** monolith vs microservices z uzasadnieniem
5. **Dywersyfikacja:** 3 opcje muszą różnić się zarówno backendem, jak i frontendem
6. **Jakość:** pros/cons MUSZĄ być specyficzne dla projektu (nie generyczne)

### Tech Stack Refine Prompt (v1.refine.prompt.txt)

**Długość:** 52 linii

**Reguły:**
1. Zachowaj nienaruszone opcje
2. Sprawdź spójność po zmianach
3. Dostosuj pros/cons do nowego kontekstu

---

## 14. Chat agentowy z Function Calling

**Endpoint:** `POST /api/ai/chat-agent`

### Architektura agenta

```
┌────────────────────────────────────┐
│           Frontend                  │
│   AgentChatRequest                  │
│   (message + history + context +    │
│    toolData + projectId)            │
└──────────────────┬─────────────────┘
                   │
         ┌─────────▼─────────────────────────────────┐
         │           Agent Chat Handler               │
         │                                             │
         │  1. Zbuduj system prompt (_AGENT_*)          │
         │  2. Wybierz narzędzia (get_tools_for_context)│
         │  3. Wywołaj Groq chat_with_tools             │
         │                                             │
         │  ┌─────────────────────────────────────┐    │
         │  │     Tool Call Loop (max 3 iter)      │    │
         │  │                                       │    │
         │  │  Dla każdego tool_call:               │    │
         │  │    ├─ QUERY tool? → resolve() server  │    │
         │  │    │  → dodaj wynik do historii        │    │
         │  │    │  → ponowne wywołanie modelu       │    │
         │  │    │                                   │    │
         │  │    └─ ACTION tool? → zwróć do frontend │    │
         │  └─────────────────────────────────────┘    │
         │                                             │
         │  4. Sanityzacja treści (_sanitize_ai_content)│
         │  5. Return AgentChatResponse                │
         └─────────────────────────────────────────────┘
```

### System prompt agenta

Prompt agenta składa się z 5 komponentów:

#### 1. Capabilities (`_AGENT_CAPABILITIES`)

```
You have access to these tools:

**ACTION tools** (require frontend confirmation):
- create_task: Create a new task in a column
- update_task: Update task properties
- delete_task: Delete a task
- move_task: Move task to another column
- move_multiple_tasks: Move multiple tasks at once
- create_multiple_tasks: Create several tasks efficiently
- delete_multiple_tasks: Delete several tasks at once
- update_project: Update project properties
- update_looking_for: Update project roles/requirements
- generate_news_draft: Generate a news post draft

**QUERY tools** (auto-executed server-side):
- get_task_details: Get details of a specific task
- get_tasks_by_column: List tasks in a column
- get_project_summary: Get project overview
- suggest_next_task: Suggest the next best task to work on
```

#### 2. Task Rules (`_AGENT_TASK_RULES`)

- Nie wyświetlaj UUID/ID w tekście odpowiedzi
- Nie pokazuj surowego JSON
- Nie powołuj się na wewnętrzne identyfikatory

#### 3. Task Info (`_AGENT_TASK_INFO`)

Dynamicznie generowane z `toolData`:
- Lista kolumn z ilością zadań
- Mapa zadań (tytuł → ID)
- Mapa członków zespołu (nazwa → ID)

#### 4. Behavior (`_AGENT_BEHAVIOR`)

- Zachowuj się naturalnie i pomocnie
- Pytaj o brakujące informacje przed działaniem
- Preferuj akcje masowe (bulk) nad pojedyncze

#### 5. Examples (`_AGENT_EXAMPLES`)

Przykłady prawidłowych odpowiedzi z tool calls.

### Pętla tool call

```python
@router.post("/chat-agent")
async def agent_chat(request: AgentChatRequest):
    # Budowanie kontekstu (max 12000 znaków)
    context = request.context[:12000] if request.context else ""
    
    # System prompt
    system_prompt = build_agent_system_prompt(
        context, request.toolData, request.language
    )
    
    # Narzędzia do kontekstu
    tools = get_tools_for_context(has_project=bool(request.projectId))
    
    # Pętla (max 3 iteracje)
    for iteration in range(3):
        text, tool_calls, usage = await groq_client.chat_with_tools(
            messages=messages,
            tools=tools,
            model="smart",
            tool_choice="auto"
        )
        
        if not tool_calls:
            break  # Brak tool calls → odpowiedź tekstowa
        
        # Rozdziel query vs action tools
        query_calls = [tc for tc in tool_calls if is_query_tool(tc["function"]["name"])]
        action_calls = [tc for tc in tool_calls if not is_query_tool(tc["function"]["name"])]
        
        if query_calls:
            # Rozwiąż query tools server-side
            for qc in query_calls:
                result = resolve_tool_call(qc, request.toolData)
                messages.append({
                    "role": "tool",
                    "content": json.dumps(result),
                    "tool_call_id": qc["id"]
                })
            continue  # Ponowne wywołanie modelu z wynikami
        
        if action_calls:
            # Zwróć do frontend do potwierdzenia
            break
    
    # Sanityzacja odpowiedzi
    clean_text = _sanitize_ai_content(text) if text else None
    
    return AgentChatResponse(
        message=clean_text,
        toolCalls=action_calls or None,
        hasToolCalls=bool(action_calls)
    )
```

### Sanityzacja treści AI

```python
UUID_PATTERN = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')
JSON_BLOCK_PATTERN = re.compile(r'```json\s*\n[\s\S]*?\n```')
ID_REF_PATTERN = re.compile(r'\(ID:?\s*[^\)]+\)')

def _sanitize_ai_content(text: str) -> str:
    """Usuń UUID, bloki JSON i referencje ID z tekstu AI."""
    text = UUID_PATTERN.sub('', text)
    text = JSON_BLOCK_PATTERN.sub('', text)
    text = ID_REF_PATTERN.sub('', text)
    # Wyczyść wielokrotne spacje i puste linie
    text = re.sub(r'\n{3,}', '\n\n', text)
    return text.strip()
```

---

## 15. Definicje narzędzi (Tools)

**Plik:** `tools/definitions.py` (594 linie)

### Format definicji

Wszystkie narzędzia zdefiniowane w formacie OpenAI Function Calling:

```python
TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "tool_name",
            "description": "What this tool does",
            "parameters": {
                "type": "object",
                "properties": {
                    "param1": {
                        "type": "string",
                        "description": "Parameter description"
                    }
                },
                "required": ["param1"]
            }
        }
    }
]
```

### Lista narzędzi ACTION (10)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `create_task` | Utwórz zadanie w kolumnie | `title`, `description`, `column`, `priority`, `tags`, `assignee` |
| `update_task` | Zaktualizuj właściwości zadania | `taskId` + dowolne pola zadania |
| `delete_task` | Usuń zadanie | `taskId` |
| `move_task` | Przenieś zadanie do innej kolumny | `taskId`, `targetColumn` |
| `move_multiple_tasks` | Przenieś wiele zadań naraz | `taskIds[]`, `targetColumn` |
| `create_multiple_tasks` | Utwórz wiele zadań efektywnie | `tasks[]` (array of task objects) |
| `delete_multiple_tasks` | Usuń wiele zadań naraz | `taskIds[]` |
| `update_project` | Zaktualizuj dane projektu | `field`, `value` |
| `update_looking_for` | Zaktualizuj szukane role | `roles[]` |
| `generate_news_draft` | Generuj szkic posta newsowego | `topic`, `tone`, `length` |

### Lista narzędzi QUERY (4)

| Narzędzie | Opis | Auto-execute |
|-----------|------|-------------|
| `get_task_details` | Szczegóły konkretnego zadania | ✅ server-side |
| `get_tasks_by_column` | Lista zadań w kolumnie | ✅ server-side |
| `get_project_summary` | Podsumowanie projektu | ✅ server-side |
| `suggest_next_task` | Sugestia następnego zadania | ✅ server-side |

### Metadane wyświetlania

```python
TOOL_DISPLAY_INFO = {
    "create_task": {
        "icon": "➕",
        "label": {"en": "Create Task", "ru": "Создать задачу"},
        "category": "tasks"
    },
    "update_project": {
        "icon": "📝",
        "label": {"en": "Update Project", "ru": "Обновить проект"},
        "category": "project"
    },
    "generate_news_draft": {
        "icon": "📰",
        "label": {"en": "Generate News", "ru": "Создать новость"},
        "category": "content"
    },
    "get_task_details": {
        "icon": "🔍",
        "label": {"en": "Task Details", "ru": "Детали задачи"},
        "category": "query"
    },
    # ... dla wszystkich 14 narzędzi
}
```

### Filtrowanie narzędzi

```python
QUERY_TOOLS = {"get_task_details", "get_tasks_by_column", "get_project_summary", "suggest_next_task"}

def is_query_tool(tool_name: str) -> bool:
    return tool_name in QUERY_TOOLS

def get_tools_for_context(has_project: bool = True) -> list[dict]:
    """Filtruj narzędzia na podstawie kontekstu."""
    if not has_project:
        # Bez projektu — tylko narzędzia ogólne
        return [t for t in TOOLS if t["function"]["name"] not in PROJECT_ONLY_TOOLS]
    return TOOLS
```

---

## 16. Resolver narzędzi

**Plik:** `tools/resolver.py` (252 linie)

### Cel

Resolver wykonuje narzędzia QUERY po stronie serwera, korzystając z danych `toolData` przesłanych w żądaniu (zamiast wykonywać zapytania do bazy danych).

### Struktura toolData

```python
class ToolDataPayload(BaseModel):
    tasks: list[dict] | None = None          # Lista zadań projektu
    taskIdMap: dict[str, str] | None = None  # tytuł → UUID
    teamMemberIdMap: dict[str, str] | None = None  # nazwa → UUID
    columns: list[str] | None = None         # Lista kolumn
    passport: dict | None = None             # Sekcje paszportu projektu
```

### Funkcje resolvera

#### resolve_tool_call(tool_call, tool_data)

```python
def resolve_tool_call(tool_call: dict, tool_data: ToolDataPayload) -> dict:
    name = tool_call["function"]["name"]
    args = tool_call["function"]["arguments_parsed"] or {}
    
    resolvers = {
        "get_task_details": _resolve_task_details,
        "get_tasks_by_column": _resolve_tasks_by_column,
        "get_project_summary": _resolve_project_summary,
        "suggest_next_task": _resolve_suggest_next_task,
    }
    
    resolver = resolvers.get(name)
    if not resolver:
        return {"error": f"Unknown query tool: {name}"}
    
    return resolver(args, tool_data)
```

#### _resolve_task_details

```python
def _resolve_task_details(args: dict, data: ToolDataPayload) -> dict:
    task_name = args.get("taskName", "")
    
    # 1. Exact match
    for task in data.tasks or []:
        if task.get("title", "").lower() == task_name.lower():
            return {"task": task, "found": True}
    
    # 2. Partial match
    for task in data.tasks or []:
        if task_name.lower() in task.get("title", "").lower():
            return {"task": task, "found": True}
    
    # 3. Nie znaleziono — lista dostępnych
    available = [t.get("title") for t in (data.tasks or [])]
    return {"found": False, "available_tasks": available[:20]}
```

#### _resolve_tasks_by_column

```python
def _resolve_tasks_by_column(args: dict, data: ToolDataPayload) -> dict:
    column_name = args.get("column", "")
    
    # Fuzzy match na nazwie kolumny
    matched_column = None
    for col in data.columns or []:
        if column_name.lower() in col.lower() or col.lower() in column_name.lower():
            matched_column = col
            break
    
    if not matched_column:
        return {"error": f"Column not found", "available_columns": data.columns}
    
    # Filtruj zadania
    tasks = [t for t in (data.tasks or []) if t.get("column") == matched_column]
    return {"column": matched_column, "tasks": tasks, "count": len(tasks)}
```

#### _resolve_project_summary

```python
def _resolve_project_summary(args: dict, data: ToolDataPayload) -> dict:
    summary = {}
    
    # Zespół
    summary["team"] = data.teamMemberIdMap or {}
    
    # Dystrybucja zadań po kolumnach
    task_distribution = {}
    for task in data.tasks or []:
        col = task.get("column", "unknown")
        task_distribution[col] = task_distribution.get(col, 0) + 1
    summary["task_distribution"] = task_distribution
    
    # Sekcje paszportu (obcięte do 1500 znaków)
    if data.passport:
        passport_text = json.dumps(data.passport)[:1500]
        summary["passport_summary"] = passport_text
    
    return summary
```

#### _resolve_suggest_next_task

```python
def _resolve_suggest_next_task(args: dict, data: ToolDataPayload) -> dict:
    tasks = data.tasks or []
    
    # Filtruj aktywne (nie done, nie archived)
    active = [t for t in tasks if t.get("column") not in ("done", "archived")]
    
    # Sortowanie priorytetowe
    def sort_key(task):
        priority_order = {"critical": 0, "high": 1, "medium": 2, "low": 3}
        p = priority_order.get(task.get("priority", "medium"), 2)
        
        # Bonus za deadline
        deadline = task.get("dueDate")
        has_deadline = 0 if deadline else 1
        
        # Bonus za szybkie wygrane (krótkie zadania)
        is_quick = 0 if "quick" in str(task.get("tags", [])).lower() else 1
        
        return (p, has_deadline, is_quick)
    
    sorted_tasks = sorted(active, key=sort_key)
    top5 = sorted_tasks[:5]
    
    return {
        "suggestions": [
            {"title": t["title"], "priority": t.get("priority"), "column": t.get("column")}
            for t in top5
        ]
    }
```

---

## 17. Konsument zdarzeń RabbitMQ

**Plik:** `consumers/event_consumer.py` (261 linii)

### Konfiguracja

| Parametr | Wartość |
|----------|---------|
| Exchange | `devhunt.events` (TOPIC, durable) |
| Queue | `devhunt.ml.recommendations` (durable) |
| Routing keys | `profile.updated`, `project.completed`, `project.created`, `showcase.published` |

### Architektura konsumenta

```
┌─────────────────────────────────────────────────────────────┐
│                    RabbitMQ Exchange                          │
│                   devhunt.events (TOPIC)                     │
└──────────┬──────────┬──────────┬──────────┬─────────────────┘
           │          │          │          │
    profile.updated   │   project.created   │
           │   project.completed   showcase.published
           │          │          │          │
           ▼          ▼          ▼          ▼
┌──────────────────────────────────────────────────────────────┐
│           Queue: devhunt.ml.recommendations                   │
│                     (durable)                                 │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                    Event Consumer                             │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │  Routing Table                                        │    │
│  │                                                       │    │
│  │  profile.updated    → handle_profile_updated()        │    │
│  │  project.completed  → handle_project_completed()      │    │
│  │  project.created    → handle_project_created()        │    │
│  │  showcase.published → handle_showcase_published()     │    │
│  └──────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘
```

### Inicjalizacja konsumenta

```python
class EventConsumer:
    def __init__(self):
        self._connection = None
        self._channel = None
        self._running = False
    
    async def start(self):
        self._running = True
        while self._running:
            try:
                url = os.getenv("RABBITMQ_URL", "amqp://guest:guest@message-broker:5672/")
                self._connection = await aio_pika.connect_robust(url)
                self._channel = await self._connection.channel()
                
                exchange = await self._channel.declare_exchange(
                    os.getenv("RABBITMQ_EXCHANGE_NAME", "devhunt.events"),
                    aio_pika.ExchangeType.TOPIC,
                    durable=True
                )
                
                queue = await self._channel.declare_queue(
                    os.getenv("ML_QUEUE", "devhunt.ml.recommendations"),
                    durable=True
                )
                
                # Binding routing keys
                for key in ["profile.updated", "project.completed", 
                           "project.created", "showcase.published"]:
                    await queue.bind(exchange, routing_key=key)
                
                RABBIT_CONNECTION_STATE.set(1)  # Connected
                
                async for message in queue:
                    async with message.process():
                        await self._handle_message(message)
                        
            except Exception as e:
                RABBIT_CONNECTION_STATE.set(0)  # Disconnected
                logger.error(f"RabbitMQ error: {e}")
                await asyncio.sleep(10)  # Auto-reconnect po 10s
```

### Handlery zdarzeń

#### handle_profile_updated

```python
async def handle_profile_updated(data: dict):
    """Użytkownik zaktualizował profil → inwalidacja cache rekomendacji."""
    user_id = data.get("userId")
    if user_id:
        redis = await get_redis_client()
        if redis:
            # Usuń wszystkie klucze rekomendacji użytkownika
            pattern = f"recommendations:user:{user_id}:*"
            keys = await redis.keys(pattern)
            if keys:
                await redis.delete(*keys)
        logger.info(f"Cache invalidated for user {user_id}")
```

#### handle_project_completed

```python
async def handle_project_completed(data: dict):
    """Projekt ukończony → aktualizacja ratingu uczestników."""
    project_id = data.get("projectId")
    if not project_id:
        return
    
    pool = await get_db_pool()
    async with pool.acquire() as conn:
        # Pobierz członków zespołu
        members = await conn.fetch(
            'SELECT "UserId" FROM "TeamMembers" WHERE "ProjectId" = $1',
            project_id
        )
        # Inkrementuj CompletedProjects counter
        for member in members:
            await conn.execute(
                'UPDATE "Users" SET "CompletedProjects" = "CompletedProjects" + 1 '
                'WHERE "Id" = $1',
                member["UserId"]
            )
```

#### handle_project_created

```python
async def handle_project_created(data: dict):
    """Nowy projekt → weryfikacja istnienia, przygotowanie na embedding."""
    project_id = data.get("projectId")
    if not project_id:
        return
    
    pool = await get_db_pool()
    async with pool.acquire() as conn:
        exists = await conn.fetchval(
            'SELECT EXISTS(SELECT 1 FROM "Projects" WHERE "Id" = $1)',
            project_id
        )
        if exists:
            logger.info(f"Project {project_id} verified for future embedding")
```

#### handle_showcase_published

```python
async def handle_showcase_published(data: dict):
    """Showcase opublikowany → aktualizacja popularności projektu."""
    project_id = data.get("projectId")
    if not project_id:
        return
    
    pool = await get_db_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            'UPDATE "Projects" SET "PopularityScore" = "PopularityScore" + 1 '
            'WHERE "Id" = $1',
            project_id
        )
```

### Metryki RabbitMQ

- `RABBIT_CONNECTION_STATE` (Gauge) — 1 = connected, 0 = disconnected
- `EVENT_MESSAGES_TOTAL` (Counter, labels: `event_type`) — ilość przetworzonych wiadomości

---

## 18. Cache — Redis

### Dwupoziomowa strategia cache

| Warstwa | Przeznaczenie | TTL | Klucz |
|---------|---------------|-----|-------|
| **AI Cache** | Odpowiedzi modelu AI | 600s (10 min) | `ai:response:{SHA256_hash[:32]}` |
| **Recommendations** | Rekomendacje projektów | 3600s (1h) | `recommendations:user:{id}:limit:{n}` |

### AI Cache (services/ai_cache.py)

**Plik:** `services/ai_cache.py` (85 linii)

```python
CACHE_TTL = 600  # 10 minut
CACHE_PREFIX = "ai:response:"

_redis_client = None

def init(redis_client):
    """Inicjalizacja z klientem Redis (wywoływana na startup)."""
    global _redis_client
    _redis_client = redis_client
```

#### Hashowanie klucza

```python
def _hash_key(prompt: str) -> str:
    """SHA-256 hash promptu → 32-znakowy hex klucz z prefixem."""
    hash_hex = hashlib.sha256(prompt.encode()).hexdigest()[:32]
    return f"{CACHE_PREFIX}{hash_hex}"
    # Wynik: "ai:response:a1b2c3d4e5f6..." (44 znaki)
```

#### Get / Set z graceful degradation

```python
async def get(prompt: str) -> tuple[dict, dict] | None:
    if not _redis_client:
        return None
    try:
        key = _hash_key(prompt)
        data = await _redis_client.get(key)
        if data:
            parsed = json.loads(data)
            return parsed["result"], parsed["usage"]
        return None
    except Exception:
        return None  # Graceful degradation

async def set(prompt: str, result: dict, usage: dict):
    if not _redis_client:
        return
    try:
        key = _hash_key(prompt)
        data = json.dumps({"result": result, "usage": usage})
        await _redis_client.setex(key, CACHE_TTL, data)
    except Exception:
        pass  # Graceful degradation — nie blokuj na błędach Redis
```

### Metryki cache

- `AI_CACHE_HITS` (Counter) — trafienia w cache
- `AI_CACHE_MISSES` (Counter) — pudła w cache

---

## 19. Modele Pydantic

**Plik:** `models/ai_models.py` (396 linii)

### Klasy bazowe

```python
class StrictBaseModel(BaseModel):
    """Baza z extra='forbid' i strict=True — rejestruje nieznane pola."""
    model_config = ConfigDict(extra="forbid", strict=True)

class StrictAliasModel(StrictBaseModel):
    """Baza z populate_by_name=True — umożliwia aliasy w camelCase."""
    model_config = ConfigDict(extra="forbid", strict=True, populate_by_name=True)
```

### Modele Tech Stack

| Model | Pola | Opis |
|-------|------|------|
| `TechStackGoals` | `performance`, `cost`, `developerSpeed`, `scalability`, `security`, `maintainability` | 6 flag boolowskich celów |
| `TechStackRequest` | `idea`, `goals?` | Żądanie generowania |
| `TechStackOption` | `name`, `description`, `pros[]`, `cons[]`, `architecture`, `backend[]`, `frontend[]`, `database[]`, `components[]`, `why_this_fits` | Pojedyncza opcja stacku |
| `TechStackResponse` | `options[]` | Odpowiedź z 3 wariantami |

### Modele Plan

| Model | Pola | Opis |
|-------|------|------|
| `PlanRequest` | `idea`, `techStack`, `customTags?`, `customRoles?`, `goals?` | Żądanie generowania planu |
| `PlanTask` | `id`, `title`, `description`, `priority`, `tags[]`, `dependsOn[]` | Pojedyncze zadanie |
| `PlanPhase` | `name`, `description`, `tasks[]` | Faza planu |
| `PlanDraft` | `phases[]` | Kompletny plan |

### Modele Refinement

```python
class RefinePlanRequest(BaseModel):
    idea: str
    techStack: str
    currentPlan: dict          # Aktualny plan do udoskonalenia
    instructions: str          # Instrukcje użytkownika
```

### Modele Diagram

```python
class DiagramRequest(BaseModel):
    techStack: str
    idea: str
    format: str = "mermaid"               # "mermaid" | "plantuml"
    diagramType: str = "architecture"     # 6 typów
    projectContext: str | None = None
```

### Modele Passport

```python
class PassportRequest(BaseModel):
    idea: str
    techStack: str
    description: str | None = None
    phases: list[PassportPlanPhase] | None = None

class PassportSection(BaseModel):
    title: str
    content: str

class PassportResponse(BaseModel):
    sections: list[PassportSection]
```

### Modele Chat

```python
class ChatMessage(BaseModel):
    role: str       # "user" | "assistant" | "system"
    content: str

class ChatRequest(BaseModel):
    message: str
    history: list[ChatMessage] | None = None
    context: str | None = None

class ChatResponse(BaseModel):
    message: str
    usage: dict | None = None
```

### Modele Agent Chat

```python
class ToolCallFunction(BaseModel):
    name: str
    arguments: str
    arguments_parsed: dict | None = None

class ToolCall(BaseModel):
    id: str
    type: str = "function"
    function: ToolCallFunction

class ToolDataPayload(BaseModel):
    tasks: list[dict] | None = None
    taskIdMap: dict[str, str] | None = None
    teamMemberIdMap: dict[str, str] | None = None
    columns: list[str] | None = None
    passport: dict | None = None

class AgentChatRequest(BaseModel):
    message: str
    history: list[ChatMessage] | None = None
    context: str | None = None
    toolData: ToolDataPayload | None = None
    projectId: str | None = None
    enableTools: bool = True
    language: str = "en"

class AgentChatResponse(BaseModel):
    message: str | None = None
    toolCalls: list[dict] | None = None
    hasToolCalls: bool = False
```

---

## 20. Bezpieczeństwo — JWT

**Plik:** `security.py` (73 linie)

### Konfiguracja

| Parametr | Wartość |
|----------|---------|
| Algorytm | HS256 |
| Issuer | `DevHunt.AuthService` |
| Klucz | `JWT_SECRET` z env (min. 32 znaki w produkcji) |

### SEC-014 — wymóg silnego klucza w produkcji

```python
JWT_SECRET = os.getenv("JWT_SECRET", "")

if not JWT_SECRET:
    JWT_SECRET = "dev-secret-key-change-in-production"
    logger.warning("⚠️ JWT_SECRET not set! Using insecure default.")

# Produkcja wymaga min. 32 znaków
if os.getenv("ENVIRONMENT") == "production":
    if len(JWT_SECRET) < 32:
        raise ValueError("SEC-014: JWT_SECRET must be at least 32 characters in production")
```

### Dekodowanie tokenu

```python
def decode_jwt_token(token: str) -> dict:
    """Dekoduj i zweryfikuj JWT token."""
    try:
        payload = jwt.decode(
            token,
            JWT_SECRET,
            algorithms=["HS256"],
            issuer="DevHunt.AuthService",
            options={
                "verify_signature": True,
                "verify_iss": True,
                "verify_exp": True,
            }
        )
        return payload
    except jwt.ExpiredSignatureError:
        raise HTTPException(401, "Token expired")
    except jwt.InvalidTokenError as e:
        raise HTTPException(401, f"Invalid token: {e}")
```

### FastAPI Dependency

```python
async def verify_token(request: Request) -> dict:
    """FastAPI dependency — weryfikacja JWT z nagłówka Authorization."""
    auth_header = request.headers.get("Authorization", "")
    
    if not auth_header.startswith("Bearer "):
        raise HTTPException(401, "Missing or invalid Authorization header")
    
    token = auth_header[7:]  # Usuń "Bearer "
    payload = decode_jwt_token(token)
    
    user_id = payload.get("nameid") or payload.get("sub")
    role = payload.get("role", "user")
    
    if not user_id:
        raise HTTPException(401, "Token missing user identifier")
    
    return {"user_id": user_id, "role": role}
```

---

## 21. Obserwowalność — metryki i tracing

**Plik:** `metrics.py` (107 linii)

### Prometheus — 11 metryk

#### HTTP Metrics

| Metryka | Typ | Labels | Opis |
|---------|-----|--------|------|
| `ml_http_requests_total` | Counter | `method`, `endpoint`, `status` | Łączna liczba żądań HTTP |
| `ml_http_request_duration_seconds` | Histogram | `method`, `endpoint` | Czas trwania żądań (buckets: 50ms, 100ms, 250ms, 500ms, 1s, 2.5s, 5s) |

#### RabbitMQ Metrics

| Metryka | Typ | Labels | Opis |
|---------|-----|--------|------|
| `ml_rabbitmq_connection_state` | Gauge | — | Stan połączenia (1=ok, 0=down) |
| `ml_event_messages_total` | Counter | `event_type` | Liczba przetworzonych zdarzeń |

#### AI Metrics

| Metryka | Typ | Labels | Opis |
|---------|-----|--------|------|
| `ml_ai_validation_failures_total` | Counter | `endpoint` | Błędy walidacji odpowiedzi AI |
| `ml_ai_fallback_triggers_total` | Counter | `from_model`, `to_model` | Przełączenia fallback między modelami |
| `ml_ai_tokens_total` | Counter | `endpoint`, `model`, `type` | Zużycie tokenów (prompt/completion) |
| `ml_ai_model_latency_seconds` | Histogram | `model` | Czas odpowiedzi modelu (buckets: 0.5s, 1s, 2.5s, 5s, 10s, 30s, 60s) |
| `ml_ai_rate_limits_total` | Counter | `model` | Liczba rate limit (429) odpowiedzi |
| `ml_ai_cache_hits_total` | Counter | — | Trafienia w AI cache Redis |
| `ml_ai_cache_misses_total` | Counter | — | Pudła w AI cache Redis |

### Wspólny rejestr

```python
from prometheus_client import CollectorRegistry, Counter, Histogram, Gauge

REGISTRY = CollectorRegistry()

# HTTP
REQUEST_COUNTER = Counter(
    'ml_http_requests_total', 
    'Total HTTP requests',
    ['method', 'endpoint', 'status'],
    registry=REGISTRY
)
REQUEST_LATENCY = Histogram(
    'ml_http_request_duration_seconds',
    'HTTP request duration',
    ['method', 'endpoint'],
    buckets=[0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0],
    registry=REGISTRY
)

# RabbitMQ
RABBIT_CONNECTION_STATE = Gauge(
    'ml_rabbitmq_connection_state',
    'RabbitMQ connection state',
    registry=REGISTRY
)
```

### OpenTelemetry Tracing

Konfiguracja w `main.py`:

```python
# Resource
resource = Resource.create({
    "service.name": os.getenv("OTEL_SERVICE_NAME", "ml-service")
})

# Tracing Provider
trace_provider = TracerProvider(resource=resource)
trace_provider.add_span_processor(
    BatchSpanProcessor(
        OTLPSpanExporter(
            endpoint=f"{OTEL_ENDPOINT}/v1/traces",
            headers={"Authorization": f"Basic {base64_credentials}"}
        )
    )
)
trace.set_tracer_provider(trace_provider)

# Logging Provider
log_provider = LoggerProvider(resource=resource)
log_provider.add_log_record_processor(
    BatchLogRecordProcessor(
        OTLPLogExporter(
            endpoint=f"{OTEL_ENDPOINT}/v1/logs",
            headers={"Authorization": f"Basic {base64_credentials}"}
        )
    )
)
```

### Endpoint /metrics

```python
@app.get("/metrics")
async def metrics():
    return Response(
        content=generate_latest(REGISTRY),
        media_type="text/plain; version=0.0.4; charset=utf-8"
    )
```

---

## 22. Docker

**Plik:** `Dockerfile` (45 linii)

### Konfiguracja obrazu

| Parametr | Wartość |
|----------|---------|
| Base image | `python:3.11-slim-bookworm` |
| Working dir | `/app` |
| User | `appuser` (UID 1000, non-root) |
| Port | 8000 |
| Health check | `curl -f http://localhost:8000/health` |

### Dockerfile

```dockerfile
FROM python:3.11-slim-bookworm

# Zależności systemowe
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    libpq-dev \
    postgresql-client \
    curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Instalacja zależności (Poetry lub pip fallback)
RUN pip install --no-cache-dir poetry==1.7.1

COPY pyproject.toml poetry.lock* ./

RUN if [ -f "poetry.lock" ]; then \
        poetry config virtualenvs.create false && \
        poetry install --no-interaction --no-ansi --no-root; \
    else \
        pip install --no-cache-dir -r requirements.txt; \
    fi

# Kopiowanie kodu
COPY . .

# Non-root user
RUN useradd --create-home --uid 1000 appuser
USER appuser

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
```

### Docker Compose

W `docker-compose.yml` serwis ML jest skonfigurowany jako:

```yaml
ml-service:
  build:
    context: ./ml-service
    dockerfile: Dockerfile
  ports:
    - "8000:8000"
  environment:
    - DATABASE_URL=postgresql://postgres:${POSTGRES_PASSWORD}@db:5432/devhunt_db
    - REDIS_URL=redis://cache-service:6379/0
    - RABBITMQ_URL=amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@message-broker:5672/
    - GROQ_API_KEY=${GROQ_API_KEY}
    - JWT_SECRET=${JWT_KEY}
  depends_on:
    - db
    - cache-service
    - message-broker
```

---

## 23. Testy

### Konfiguracja testów

| Parametr | Wartość |
|----------|---------|
| Framework | pytest 8.0 + pytest-asyncio 0.23 |
| Formatter | black 25.11 (line-length 100) |
| Linter | ruff 0.1 (line-length 100) |
| Katalog | `tests/` |

### Fixtury (conftest.py)

```python
# tests/conftest.py
import sys
from pathlib import Path

ML_ROOT = Path(__file__).resolve().parents[1]
if str(ML_ROOT) not in sys.path:
    sys.path.insert(0, str(ML_ROOT))
```

### Pokrycie testami

#### test_ai_service.py (340 linii, 34 testy)

8 klas testowych pokrywających:

| Klasa | Testuje | Ilość testów |
|-------|---------|-------------|
| `TestRenderPrompt` | Renderowanie szablonów `{{placeholder}}` | 6 |
| `TestParseJsonResponse` | Parsowanie JSON z surowego tekstu AI | 8 |
| `TestBuildGoalsSection` | Budowanie sekcji celów | 5 |
| `TestBuildScaleSection` | Detekcja skali projektu (brief/moderate/detailed) | 4 |
| `TestBuildConstraintsSection` | Detekcja ograniczeń (free-tier, open source) | 4 |
| `TestDetectProjectType` | Klasyfikacja projektu (web/mobile/game/...) | 6 |
| `TestBuildProjectTypeSection` | Sekcja platform-specific | 2 |
| `TestBuildCustomSection` | Sekcja custom tags/roles | 4 |
| `TestIsRateLimitError` | Detekcja rate limit w błędach | 4 |
| `TestEmitTokenMetrics` | Emisja metryk Prometheus | 2 |

#### test_ai_cache.py (130 linii, 10 testów)

| Klasa | Testuje | Ilość testów |
|-------|---------|-------------|
| `TestHashKey` | SHA-256 hashing z prefixem, deterministyczność | 4 |
| `TestCacheGet` | Cache hit, miss, brak Redis, błąd Redis | 4 |
| `TestCacheSet` | Store z TTL, brak Redis, błąd Redis | 3 |
| `TestInit` | Inicjalizacja z/bez klienta Redis | 2 |

#### test_groq_client.py (239 linii, 15 testów)

| Klasa | Testuje | Ilość testów |
|-------|---------|-------------|
| `TestCleanAndParseJson` | Parsowanie JSON (markdown, whitespace, nested) | 8 |
| `TestGetModelsToTry` | Łańcuch fallbacków modeli | 4 |
| `TestExtractUsageInfo` | Normalizacja informacji o użyciu tokenów | 3 |
| `TestBuildHeaders` | Budowanie nagłówków auth | 1 |
| `TestParseToolCalls` | Parsowanie tool calls z odpowiedzi | 4 |
| `TestExceptions` | Klasy wyjątków | 2 |
| `TestModelsConfig` | Walidacja konfiguracji modeli i fallbacków | 3 |

### Uruchamianie testów

```bash
# Wszystkie testy
cd ml-service
poetry run pytest

# Verbose z coverage
poetry run pytest -v --tb=short

# Konkretny plik
poetry run pytest tests/test_ai_service.py

# Konkretna klasa
poetry run pytest tests/test_ai_service.py::TestRenderPrompt

# Linting
poetry run black --check .
poetry run ruff .
```

---

## 24. Zależności

### Zależności runtime (pyproject.toml)

| Pakiet | Wersja | Przeznaczenie |
|--------|--------|---------------|
| `fastapi` | ^0.115.0 | Framework webowy |
| `uvicorn` | ^0.38.0 | Serwer ASGI |
| `pydantic` | ^2.9.2 | Walidacja danych |
| `asyncpg` | ^0.30.0 | Async PostgreSQL driver |
| `redis` | ^7.1.0 | Async Redis client |
| `aio-pika` | ^9.4.1 | Async RabbitMQ (AMQP) |
| `httpx` | ^0.27.0 | Async HTTP client (Groq API) |
| `google-generativeai` | ^0.8.3 | Google Gemini SDK |
| `pyjwt` | ^2.10.0 | JWT encoding/decoding |
| `prometheus-client` | ^0.21.0 | Metryki Prometheus |
| `opentelemetry-api` | ^1.27.0 | OpenTelemetry API |
| `opentelemetry-sdk` | ^1.27.0 | OpenTelemetry SDK |
| `opentelemetry-exporter-otlp-proto-http` | ^1.27.0 | OTLP HTTP exporter |

### Zależności deweloperskie

| Pakiet | Wersja | Przeznaczenie |
|--------|--------|---------------|
| `pytest` | ^8.0 | Framework testowy |
| `pytest-asyncio` | ^0.23 | Async testy |
| `black` | ^25.1.0 | Formatter kodu |
| `ruff` | ^0.1.0 | Linter |

### Zarezerwowane na przyszłość (requirements.txt — zakomentowane)

```txt
# Zarezerwowane na przyszłe rozszerzenia ML
# scikit-learn==1.5.0
# numpy==1.26.4
# pandas==2.2.2
```

---

## 25. Diagramy przepływu

### Przepływ generowania tech stacku

```
[Użytkownik]
    │
    ▼ POST /api/ai/generate-tech-stack
    │
┌───▼─────────────────────────────┐
│  Walidacja TechStackRequest      │
│  (Pydantic strict mode)         │
└───┬─────────────────────────────┘
    │
┌───▼─────────────────────────────┐
│  Budowanie promptu              │
│  - load_prompt("techstack/v1")  │
│  - build_goals_section()        │
│  - build_constraints_section()  │
│  - build_scale_section()        │
│  - build_project_type_section() │
│  - render_prompt(template, ...) │
└───┬─────────────────────────────┘
    │
┌───▼─────────────────────────────┐
│  call_model(prompt)             │
│  ┌───────────────────────────┐  │
│  │ 1. Cache check (Redis)    │  │
│  │    HIT? → return cached   │  │
│  │                           │  │
│  │ 2. Groq generate_json()   │  │
│  │    smart → retry 3x       │  │
│  │    fallback → versatile   │  │
│  │    fallback → qwen3-32b   │  │
│  │    fallback → scout       │  │
│  │    fallback → fast        │  │
│  │                           │  │
│  │ 3. Metryki (latency,      │  │
│  │    tokens, model)         │  │
│  │                           │  │
│  │ 4. Cache store (TTL 10m)  │  │
│  └───────────────────────────┘  │
└───┬─────────────────────────────┘
    │
┌───▼─────────────────────────────┐
│  Parsowanie → TechStackResponse │
│  (3 warianty tech stacku)       │
└───┬─────────────────────────────┘
    │
    ▼ Response JSON
[Użytkownik]
```

### Przepływ chatu agentowego

```
[Frontend] ─── AgentChatRequest ──►
                                    │
                            ┌───────▼────────────────────────────┐
                            │  Build Agent System Prompt          │
                            │  - _AGENT_CAPABILITIES (14 tools)  │
                            │  - _AGENT_TASK_RULES                │
                            │  - _AGENT_TASK_INFO (from toolData) │
                            │  - _AGENT_BEHAVIOR                  │
                            │  - Context (max 12000 chars)        │
                            └───────┬────────────────────────────┘
                                    │
                            ┌───────▼────────────────────────────┐
                            │  Iteration 1/3                      │
                            │  Groq chat_with_tools()             │
                            │  model: smart, temp: 0.3            │
                            └───────┬────────────────────────────┘
                                    │
                         ┌──────────▼──────────┐
                         │ Tool calls returned? │
                         └──────┬───────┬──────┘
                               NO      YES
                                │       │
                                │  ┌────▼──────────────┐
                                │  │ Classify tools:    │
                                │  │ QUERY vs ACTION    │
                                │  └──┬──────────┬─────┘
                                │     │          │
                                │   QUERY      ACTION
                                │     │          │
                                │  ┌──▼────────┐ │
                                │  │ Resolve    │ │
                                │  │ server-    │ │
                                │  │ side via   │ │
                                │  │ toolData   │ │
                                │  └──┬────────┘ │
                                │     │          │
                                │     │ Add results to messages
                                │     │ → Continue loop (iter 2/3)
                                │     │          │
                                │     │     ┌────▼────────────────┐
                                │     │     │ Return to frontend   │
                                │     │     │ for user confirmation │
                                │     │     └─────────────────────┘
                                │     │
                         ┌──────▼─────▼────────────────────┐
                         │  _sanitize_ai_content(text)      │
                         │  - Remove UUIDs                  │
                         │  - Remove JSON blocks            │
                         │  - Remove ID references          │
                         │  - Clean whitespace              │
                         └──────────────┬──────────────────┘
                                        │
                                ◄── AgentChatResponse ──────
                                    (message + toolCalls)
```

### Przepływ rekomendacji z cache

```
[Frontend]
    │
    ▼ POST /api/recommendations/generate
    │
┌───▼──────────────────────┐
│  JWT verify_token()       │
│  → user_id, role          │
└───┬──────────────────────┘
    │
┌───▼──────────────────────┐
│  Redis cache check        │
│  key: recommendations:    │
│  user:{id}:limit:{n}     │
└───┬──────────┬───────────┘
   HIT        MISS
    │           │
    │    ┌──────▼──────────────────┐
    │    │  PostgreSQL queries      │
    │    │  - USER_PROFILE_QUERY   │
    │    │  - PROJECTS_QUERY       │
    │    │    (max 100 kandydatów)  │
    │    └──────┬──────────────────┘
    │           │
    │    ┌──────▼──────────────────┐
    │    │  Scoring (per project)   │
    │    │  skill_match × 0.4      │
    │    │  + difficulty × 0.2     │
    │    │  + rating × 0.2         │
    │    │  + team_size × 0.1      │
    │    │  + featured × 0.1       │
    │    └──────┬──────────────────┘
    │           │
    │    ┌──────▼──────────────────┐
    │    │  Sort + Limit            │
    │    │  Redis cache store       │
    │    │  TTL: 3600s (1h)        │
    │    └──────┬──────────────────┘
    │           │
    ▼           ▼
┌──────────────────────────┐
│  Response JSON            │
│  recommendations: [...]   │
└──────────────────────────┘
```

### Przepływ zdarzeń RabbitMQ

```
[Core API]                    [Frontend]
    │                              │
    │  Event: profile.updated      │  Event: showcase.published
    │  Event: project.completed    │
    │  Event: project.created      │
    │                              │
    ▼                              ▼
┌──────────────────────────────────────────┐
│  RabbitMQ Exchange: devhunt.events       │
│  Type: TOPIC, Durable                    │
└──────────────────┬───────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────┐
│  Queue: devhunt.ml.recommendations       │
│  Bindings:                                │
│  - profile.updated                        │
│  - project.completed                      │
│  - project.created                        │
│  - showcase.published                     │
└──────────────────┬───────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────┐
│  Event Consumer Handlers                  │
│                                           │
│  profile.updated                          │
│    → Redis: DELETE recommendations:user:* │
│                                           │
│  project.completed                        │
│    → PostgreSQL: UPDATE Users SET         │
│      CompletedProjects += 1               │
│      (for each team member)               │
│                                           │
│  project.created                          │
│    → PostgreSQL: Verify project exists    │
│    → Prepare for future embedding         │
│                                           │
│  showcase.published                       │
│    → PostgreSQL: UPDATE Projects SET      │
│      PopularityScore += 1                 │
└──────────────────────────────────────────┘
```

---

## Podsumowanie

ML Service to centralny komponent AI platformy DevHunt, integrujący:

- **7 modeli AI** przez Groq API z inteligentnym łańcuchem fallbacków
- **14 narzędzi agentowych** z dwufazowym wykonaniem (query server-side, action frontend-side)
- **5-czynnikowy algorytm rekomendacji** z dwupoziomowym cache Redis
- **4 szablony promptów** z wersjonowaniem i dynamicznym renderowaniem
- **11 metryk Prometheus** + distributed tracing przez OpenTelemetry
- **4 handlery zdarzeń** RabbitMQ z auto-reconnect
- **59 testów jednostkowych** pokrywających kluczowe funkcje

Serwis jest zaprojektowany jako niezależny mikroserwis z graceful degradation — brak Redis nie blokuje działania, brak jednego modelu AI nie zatrzymuje generowania.
