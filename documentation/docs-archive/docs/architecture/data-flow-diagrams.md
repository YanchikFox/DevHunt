---
sidebar_position: 3
title: Data Flow Diagrams
description: Detailed data flow and communication patterns in DevHunt
---

# Data Flow Diagrams

This document provides detailed visualizations of data flows, communication patterns, and interaction sequences within the DevHunt platform.

## System Architecture Overview

```mermaid
graph TB
    %% External Systems
    subgraph "External Systems"
        GitHub[🐙 GitHub/GitLab<br/>Webhooks & OAuth]
        SMTP[📧 Email Providers<br/>SendGrid, Gmail]
        SMS[📱 SMS Services<br/>Twilio]
        Push[🔔 Push Services<br/>FCM]
    end

    %% User Interfaces
    subgraph "User Interfaces"
        Web[🌐 Web Frontend<br/>Next.js SPA]
        Mobile[📱 Mobile Apps<br/>Future]
    end

    %% API Gateway
    subgraph "API Gateway"
        Nginx[🚪 NGINX<br/>Load Balancer<br/>Rate Limiting<br/>TLS Termination]
    end

    %% Application Layer
    subgraph "Application Layer"
        FrontendSvc[🎨 Frontend Service<br/>SSR & API Routes]
        CoreAPI[⚙️ Core API<br/>.NET<br/>Business Logic]
        AuthSvc[🔐 Auth Service<br/>.NET<br/>JWT & Sessions]
    end

    %% Supporting Services
    subgraph "Supporting Services"
        IntegrationGW[🔗 Integration Gateway<br/>Node.js<br/>OAuth & Webhooks]
        NotificationSvc[📧 Notification Service<br/>Node.js<br/>Multi-channel]
        MLSvc[🤖 ML Service<br/>Python<br/>Recommendations]
    end

    %% Data Layer
    subgraph "Data Layer"
        PostgreSQL[(📊 PostgreSQL<br/>Users, Projects, Teams)]
        Redis[(⚡ Redis<br/>Cache, Sessions, PubSub)]
        RabbitMQ[(📨 RabbitMQ<br/>Events & Queues)]
        SeaweedFS[(📁 SeaweedFS<br/>Files & Assets)]
    end

    %% Monitoring
    subgraph "Observability"
        Prometheus[📈 Prometheus<br/>Metrics]
        OpenObserve[📊 OpenObserve<br/>Logs & Traces]
        Jaeger[🔍 Jaeger<br/>Distributed Tracing]
    end

    %% Connections
    GitHub --> IntegrationGW
    IntegrationGW --> CoreAPI
    IntegrationGW --> RabbitMQ

    Web --> Nginx
    Nginx --> FrontendSvc
    Nginx --> CoreAPI
    Nginx --> AuthSvc

    FrontendSvc --> CoreAPI
    FrontendSvc --> AuthSvc

    CoreAPI --> AuthSvc
    CoreAPI --> PostgreSQL
    CoreAPI --> Redis
    CoreAPI --> RabbitMQ
    CoreAPI --> SeaweedFS

    AuthSvc --> PostgreSQL
    AuthSvc --> Redis

    RabbitMQ --> NotificationSvc
    RabbitMQ --> MLSvc

    NotificationSvc --> SMTP
    NotificationSvc --> SMS
    NotificationSvc --> Push

    CoreAPI --> Prometheus
    AuthSvc --> Prometheus
    IntegrationGW --> Prometheus
    NotificationSvc --> Prometheus
    MLSvc --> Prometheus

    CoreAPI --> OpenObserve
    AuthSvc --> OpenObserve
    IntegrationGW --> OpenObserve
    NotificationSvc --> OpenObserve
    MLSvc --> OpenObserve

    CoreAPI --> Jaeger
    AuthSvc --> Jaeger
    IntegrationGW --> Jaeger
    NotificationSvc --> Jaeger
    MLSvc --> Jaeger

    %% Styling
    classDef external fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef ui fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef gateway fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    classDef app fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef support fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    classDef data fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef monitoring fill:#f5f5f5,stroke:#616161,stroke-width:1px

    class GitHub,SMTP,SMS,Push external
    class Web,Mobile ui
    class Nginx gateway
    class FrontendSvc,CoreAPI,AuthSvc app
    class IntegrationGW,NotificationSvc,MLSvc support
    class PostgreSQL,Redis,RabbitMQ,SeaweedFS data
    class Prometheus,OpenObserve,Jaeger monitoring
```

## Request Flow Patterns

### Synchronous API Call Flow

```mermaid
sequenceDiagram
    participant U as User
    participant B as Browser
    participant N as API Gateway
    participant F as Frontend Service
    participant C as Core API
    participant D as Database

    Note over U,D: User creates a new project

    U->>B: Click "Create Project"
    B->>F: POST /api/projects (client-side)
    F->>N: Forward request with JWT
    N->>C: POST /api/projects
    C->>C: Validate JWT token
    C->>C: Validate request data
    C->>D: INSERT project record
    D-->>C: Project ID
    C->>C: Generate project response
    C-->>N: JSON response
    N-->>F: JSON response
    F-->>B: Success + redirect
    B-->>U: Project created page

    Note over C: Publish project.created event<br/>to RabbitMQ for async processing
```

### Real-time Communication Flow

```mermaid
sequenceDiagram
    participant U1 as User 1
    participant U2 as User 2
    participant B1 as Browser 1
    participant B2 as Browser 2
    participant F as Frontend
    participant C as Core API
    participant R as Redis
    participant RMQ as RabbitMQ
    participant N as Notification Service

    Note over U1,N: Team chat scenario

    U1->>B1: Type message
    B1->>F: Send via SignalR
    F->>C: SignalR message
    C->>C: Process & store message
    C->>R: Publish to team channel
    R->>C: Broadcast to subscribers
    C->>F: Real-time delivery
    F->>B2: Update chat UI
    B2->>U2: Show new message

    Note over C: Async notification processing
    C->>RMQ: Publish chat.message event
    RMQ->>N: Consume notification event
    N->>N: Check user preferences
    N->>U2: Send push notification
```

### Event-Driven Processing Flow

```mermaid
sequenceDiagram
    participant C as Core API
    participant RMQ as RabbitMQ
    participant IGW as Integration Gateway
    participant NS as Notification Service
    participant MS as ML Service
    participant DB as Database

    Note over C,DB: Project completion scenario

    C->>DB: Update project status to 'completed'
    C->>RMQ: Publish "project.completed" event
    RMQ->>NS: Consume for notifications
    NS->>NS: Generate completion email
    NS->>NS: Send to team members

    RMQ->>IGW: Consume for integrations
    IGW->>IGW: Check webhook configs
    IGW->>IGW: Send webhook to GitHub/GitLab

    RMQ->>MS: Consume for recommendations
    MS->>DB: Fetch project data
    MS->>MS: Update team success metrics
    MS->>DB: Store updated recommendations

    Note over NS,IGW,MS: All processing happens asynchronously<br/>without blocking the main API response
```

## Authentication & Authorization Flow

```mermaid
flowchart TD
    A[User visits protected page] --> B{Valid session?}
    B -->|No| C[Redirect to login]
    C --> D[User enters credentials]
    D --> E[Frontend calls Auth Service]
    E --> F[Validate username/password]
    F --> G[Generate JWT + Refresh token]
    G --> H[Return tokens to frontend]
    H --> I[Store tokens securely]
    I --> J[Redirect to original page]

    B -->|Yes| K[Extract JWT from storage]
    K --> L[Attach to API request]
    L --> M[API Gateway validates JWT]
    M --> N{Core API validates JWT}
    N --> O[Process request]
    N --> P{JWT expired?}
    P -->|Yes| Q[Use refresh token]
    Q --> R[Get new JWT]
    R --> S[Retry original request]
    P -->|No| T[Return 401 Unauthorized]
```

## Data Persistence Patterns

### Write Path (Project Creation)

```mermaid
flowchart TD
    A[Create Project Request] --> B[Validate Input]
    B --> C[Start Transaction]
    C --> D[Insert Project Record]
    D --> E[Insert Team Members]
    E --> F[Insert Initial Tasks]
    F --> G[Commit Transaction]
    G --> H[Publish Domain Events]
    H --> I[Return Success Response]

    style A fill:#e3f2fd
    style I fill:#c8e6c9

    I --> J[Async Processing]
    J --> K[Update Search Index]
    J --> L[Send Notifications]
    J --> M[Update Recommendations]
```

### Read Path (Project Dashboard)

```mermaid
flowchart TD
    A[Dashboard Request] --> B{Cache Hit?}
    B -->|Yes| C[Return Cached Data]
    C --> D[Return Response]

    B -->|No| E[Query Primary DB]
    E --> F[Aggregate Related Data]
    F --> G[Apply Business Logic]
    G --> H[Store in Cache]
    H --> I[Return Response]

    I --> J[Background Refresh]
    J --> K{Cache TTL Expired?}
    K -->|Yes| L[Async Cache Update]
    K -->|No| M[Continue Normal Flow]
```

## Integration Patterns

### OAuth Flow (GitHub/GitLab)

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant IGW as Integration Gateway
    participant GH as GitHub
    participant C as Core API

    U->>F: Click "Connect GitHub"
    F->>IGW: GET /oauth/github/url
    IGW->>GH: Request OAuth URL
    GH-->>IGW: Authorization URL
    IGW-->>F: OAuth URL
    F-->>U: Redirect to GitHub

    U->>GH: Authorize application
    GH-->>F: Callback with code
    F->>IGW: POST /oauth/github/callback
    IGW->>GH: Exchange code for token
    GH-->>IGW: Access token
    IGW->>C: Store integration
    C-->>IGW: Success
    IGW-->>F: Redirect to success page
```

### Webhook Processing Flow

```mermaid
sequenceDiagram
    participant GH as GitHub
    participant IGW as Integration Gateway
    participant RMQ as RabbitMQ
    participant C as Core API
    participant NS as Notification Service

    GH->>IGW: POST webhook (push event)
    IGW->>IGW: Validate signature
    IGW->>IGW: Transform payload
    IGW->>RMQ: Publish integration event
    RMQ->>C: Consume webhook event
    C->>C: Process repository update
    C->>C: Update project status
    C->>RMQ: Publish status change event
    RMQ->>NS: Consume notification event
    NS->>NS: Send team notifications
```

## Error Handling & Resilience

### Circuit Breaker Pattern

```mermaid
stateDiagram-v2
    [*] --> Closed: Service healthy
    Closed --> Open: Error threshold exceeded
    Open --> HalfOpen: Timeout period passed
    HalfOpen --> Closed: Success response
    HalfOpen --> Open: Failure response

    note right of Closed
        Normal operation
        All requests pass through
    end note

    note right of Open
        Service unavailable
        Requests fail fast
    end note

    note right of HalfOpen
        Testing recovery
        Limited requests allowed
    end note
```

### Retry & Dead Letter Queue Pattern

```mermaid
flowchart TD
    A[Message Received] --> B{Processing Success?}
    B -->|Yes| C[Complete Message]
    B -->|No| D{Retry Count < Max?}
    D -->|Yes| E[Delay & Retry]
    E --> B
    D -->|No| F[Move to Dead Letter Queue]
    F --> G[Alert Operations Team]
    G --> H[Manual Investigation]
```

This comprehensive set of diagrams provides a complete view of how data flows through the DevHunt platform, from user interactions to background processing and external integrations.