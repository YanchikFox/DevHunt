---
sidebar_position: 2
title: Backend API Reference
description: Complete C# API reference documentation for DevHunt backend
---

import Link from '@docusaurus/Link';

# Backend API Reference

The DevHunt Backend API Reference provides complete documentation for all C# classes, methods, and types in the backend services.

## 📡 API Documentation

<div className="row margin-top--lg margin-bottom--lg">
  <div className="col col--6">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>📘 C# API Reference (DocFX)</h3>
      </div>
      <div className="card__body">
        <p>
          Complete C# API reference generated from XML documentation comments using DocFX.
          Browse all classes, methods, properties, and types with full IntelliSense-style documentation.
        </p>
        <p><strong>Includes:</strong></p>
        <ul>
          <li>All controllers and endpoints</li>
          <li>Service classes and business logic</li>
          <li>Data models and entities</li>
          <li>Extensions and utilities</li>
          <li>Middleware and filters</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docfx"
          target="_blank"
          rel="noopener noreferrer">
          Open Full API Reference →
        </Link>
      </div>
    </div>
  </div>
  
  <div className="col col--6">
    <div className="card card--full-height">
      <div className="card__header">
        <h3>🔌 Interactive API (OpenAPI)</h3>
      </div>
      <div className="card__body">
        <p>
          Interactive REST API documentation with "Try it out" functionality.
          Test API endpoints directly from your browser.
        </p>
        <p><strong>Features:</strong></p>
        <ul>
          <li>Interactive endpoint testing</li>
          <li>Request/response schemas</li>
          <li>Authentication support</li>
          <li>Example requests</li>
        </ul>
      </div>
      <div className="card__footer">
        <Link
          className="button button--primary button--block"
          to="/docs/api/interactive">
          Open Interactive API →
        </Link>
      </div>
    </div>
  </div>
</div>

## 📦 Included Projects

The backend API reference includes documentation for:

### DevHunt.CoreApi
Main API service with controllers, services, and business logic:
- **Controllers** - All REST API endpoints
- **Services** - Business logic layer
- **Middleware** - Request pipeline components
- **Hubs** - SignalR real-time communication
- **Filters** - Action filters and attributes

### DevHunt.AuthService
Authentication and authorization service:
- **Auth Controllers** - Login, registration, token management
- **JWT Services** - Token generation and validation
- **Security** - Authentication middleware

### DevHunt.Infrastructure
Data layer and infrastructure:
- **Models** - Entity models and DTOs
- **DbContext** - Entity Framework Core context
- **Configurations** - EF Core model configurations
- **Repositories** - Data access patterns

## 🚀 Quick Access

<div className="row">
  <div className="col col--4">
    <Link className="button button--primary button--block" to="/api-reference/index.html" target="_blank">
      Full C# API Reference
    </Link>
  </div>
  <div className="col col--4">
    <Link className="button button--secondary button--block" to="/docs/api/interactive">
      Interactive OpenAPI
    </Link>
  </div>
  <div className="col col--4">
    <Link className="button button--secondary button--block" to="/docs/backend/intro">
      Backend Overview
    </Link>
  </div>
</div>

## 🔧 Regeneration

To regenerate the API reference from C# XML comments:

```bash
cd documentation
npm run gen:docfx
```

Or use the full documentation preparation:

```bash
npm run prepare:docs
```

## 📚 Related Documentation

- [Backend Core Overview](./intro) - Backend architecture and setup
- [Database Models](./database/models) - Entity models and relationships
- [Services Documentation](./services/core) - Service implementation details
- [Interactive API](/docs/api/interactive) - REST API with Try it out
