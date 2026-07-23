---
sidebar_position: 1
title: Welcome to DevHunt Documentation
description: Complete technical documentation for the DevHunt developer collaboration platform
sidebar_label: Welcome to DevHunt Documentation
---

import Link from '@docusaurus/Link';

# Welcome to DevHunt Documentation

**DevHunt** is a comprehensive developer collaboration platform designed specifically for hackathons and showcase projects. It enables developers to form teams, launch projects, manage collaborative workflows, and showcase their work to the community.

## 👉 Start Here

:::info New to DevHunt?
**Start with the Quickstart Guide** → [Get up and running in 5 minutes](/docs/getting-started/quickstart)

Already familiar? Jump directly to what you need:

- **First-time setup** → [Setup](/docs/getting-started/setup)
- **Understanding the system** → [System Overview](/docs/architecture/system-overview)
- **API development** → [Backend Overview](/docs/backend/overview)
- **Frontend development** → [Frontend Overview](/docs/frontend/overview)
  :::

## 🚀 Quick Start

Get up and running in minutes:

```bash
# Clone the repository
git clone https://github.com/YanchikFox/DevHunt.git
cd DevHunt

# Start all services
docker compose up -d

# Access the application
open http://localhost:3000
```

## 📚 Documentation Overview

<div className="row margin-top--lg margin-bottom--lg">
  <div className="col col--4 margin-bottom--lg">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>🎯 Getting Started</h3>
      </div>
      <div className="card__body">
        <p>New to DevHunt? Start here to understand the platform and get set up quickly.</p>
        <ul>
          <li>Platform overview and concepts</li>
          <li>Local development setup</li>
          <li>Configuration and environment</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/getting-started/quickstart">
          Start Here →
        </Link>
      </div>
    </div>
  </div>

  <div className="col col--4 margin-bottom--lg">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>🏗️ Architecture</h3>
      </div>
      <div className="card__body">
        <p>Understand DevHunt's system architecture, components, and how everything fits together.</p>
        <ul>
          <li>System overview and design principles</li>
          <li>High-level architecture diagrams</li>
          <li>Component relationships and data flow</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/architecture/system-overview">
          Explore Architecture →
        </Link>
      </div>
    </div>
  </div>

  <div className="col col--4 margin-bottom--lg">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>⚙️ Backend Services</h3>
      </div>
      <div className="card__body">
        <p>Dive deep into DevHunt's backend services, APIs, and data models.</p>
        <ul>
          <li>.NET Core API services</li>
          <li>Database models and migrations</li>
          <li>REST API documentation</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/backend/overview">
          Backend Docs →
        </Link>
      </div>
    </div>
  </div>
</div>

<div className="row margin-bottom--lg">
  <div className="col col--6 margin-bottom--lg">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>🎨 Frontend Development</h3>
      </div>
      <div className="card__body">
        <p>Learn about the Next.js frontend application, components, and development workflow.</p>
        <ul>
          <li>React components and UI libraries</li>
          <li>Custom hooks and state management</li>
          <li>TypeScript API reference</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/frontend/overview">
          Frontend Docs →
        </Link>
      </div>
    </div>
  </div>

  <div className="col col--6 margin-bottom--lg">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>🔧 Operations & Deployment</h3>
      </div>
      <div className="card__body">
        <p>Deploy, monitor, and maintain DevHunt in production environments.</p>
        <ul>
          <li>Production deployment guides</li>
          <li>Monitoring and observability</li>
          <li>Security and maintenance</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/operations/overview">
          Operations Guide →
        </Link>
      </div>
    </div>
  </div>
</div>

## 🌟 Key Features

<div className="row">
  <div className="col col--3 margin-bottom--lg">
    <div className="card text--center">
      <div className="card__header">
        <h4>👥 Team Formation</h4>
      </div>
      <div className="card__body">
        <p>Quick team assembly with skill-based matching and AI recommendations.</p>
      </div>
    </div>
  </div>

  <div className="col col--3 margin-bottom--lg">
    <div className="card text--center">
      <div className="card__header">
        <h4>🚀 Real-time Collaboration</h4>
      </div>
      <div className="card__body">
        <p>Live chat, task boards, and file sharing for seamless teamwork.</p>
      </div>
    </div>
  </div>

  <div className="col col--3 margin-bottom--lg">
    <div className="card text--center">
      <div className="card__header">
        <h4>📊 Project Showcase</h4>
      </div>
      <div className="card__body">
        <p>Professional portfolios with community feedback and recognition.</p>
      </div>
    </div>
  </div>

  <div className="col col--3 margin-bottom--lg">
    <div className="card text--center">
      <div className="card__header">
        <h4>🔗 GitHub/GitLab Integration</h4>
      </div>
      <div className="card__body">
        <p>Seamless integration with popular development platforms.</p>
      </div>
    </div>
  </div>
</div>

## 🛠️ Technology Stack

### Frontend

- **Next.js 16 / React 19** - React framework with App Router
- **TypeScript** - Type safety and developer experience
- **Tailwind CSS** - Utility-first styling
- **shadcn/ui** - Modern component library
- **SignalR** - Real-time WebSocket communication

### Backend Services

- **.NET 10** - Cross-platform development framework
- **ASP.NET Core** - High-performance web APIs
- **Entity Framework Core** - ORM for data access
- **PostgreSQL** - Primary relational database
- **Redis** - Caching and session management

### Supporting Services

- **Node.js** - Integration and notification services
- **Python/FastAPI** - Machine learning and recommendations
- **RabbitMQ** - Message queuing and event streaming
- **SeaweedFS** - Distributed object storage

### Infrastructure

- **Docker & Docker Compose** - Containerization and orchestration
- **Prometheus & OpenObserve** - Monitoring and observability
- **NGINX** - API gateway and load balancing
- **Kubernetes** - Production container orchestration

## 🔌 Development Environment

| Service           | Local Port               | Description             |
| ----------------- | ------------------------ | ----------------------- |
| **Documentation** | `http://localhost`       | This Docusaurus portal  |
| **Frontend**      | `http://localhost:3000`  | Next.js web application |
| **Core API**      | `http://localhost:7002`  | Main business logic API |
| **Auth Service**  | `http://localhost:7001`  | Authentication & JWT    |
| **ML Service**    | `http://localhost:8000`  | AI recommendations      |
| **PostgreSQL**    | `localhost:5432`         | Primary database        |
| **Redis**         | `localhost:6379`         | Cache & sessions        |
| **RabbitMQ**      | `http://localhost:15672` | Message broker UI       |

## 📞 Need Help?

<div className="row">
  <div className="col col--6">
    <h4>🆘 Common Issues</h4>
    <ul>
      <li><a href="/docs/backend/troubleshooting">Backend Troubleshooting</a></li>
      <li><a href="/docs/frontend/troubleshooting">Frontend Troubleshooting</a></li>
      <li><a href="/docs/operations/overview">Operations Guide</a></li>
    </ul>
  </div>
  <div className="col col--6">
    <h4>📖 Deep Dives</h4>
    <ul>
      <li><a href="/docs/architecture/high-level-architecture">System Architecture</a></li>
      <li><a href="/docs/api/intro">API Reference</a></li>
      <li><a href="/docs/architecture/security">Security Guide</a></li>
    </ul>
  </div>
</div>

---

**Ready to build something amazing?** Start with our [Quickstart Guide](/docs/getting-started/quickstart) to get DevHunt running locally, or explore the [System Overview](/docs/architecture/system-overview) to understand the platform architecture.
