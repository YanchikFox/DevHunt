---
sidebar_position: 2
title: Swagger UI
---

# Interactive API Documentation

Explore the complete DevHunt Core API using interactive Swagger UI.

## Swagger UI

The interactive API documentation allows you to:
- Browse all available endpoints
- View request/response schemas
- Try API calls directly from the browser
- See example responses
- Understand authentication requirements

### Access Swagger UI

**In the documentation portal:**
- Interactive page: [/docs/api/interactive](/docs/api/interactive)
- Full screen: [/swagger-ui.html](/swagger-ui.html)

**Swagger JSON:**
- [Download swagger.json](/api/swagger.json)

## Quick Links

- [API Introduction](./intro) - API overview and getting started
- [Authentication](./auth/overview) - Authentication guide
- [API Introduction](./intro) - Getting started with the API

## Using Swagger UI Locally

1. **Start the Core API service:**

```bash
cd DevHunt.CoreApi
dotnet run
```

2. **Open Swagger UI in your browser:**

```
http://localhost:7002/swagger
```

3. **Authenticate (if needed):**
   - Click "Authorize" button
   - Enter your JWT token
   - Click "Authorize" again

4. **Test endpoints:**
   - Click on any endpoint
   - Click "Try it out"
   - Fill in parameters
   - Click "Execute"

## API Categories

The Core API is organized into the following categories:

- **Admin** - Administrative operations
- **Auth** - Authentication and authorization
- **Projects** - Project management
- **Teams** - Team operations
- **Users** - User profiles and management
- **Tasks** - Task management
- **Documents** - Document handling
- **Invitations** - Team invitations
- **Notifications** - Notification system
- **Recommendations** - ML-powered recommendations
- **Reviews** - Project reviews
- **Skills** - User skills management
- **Achievements** - User achievements

## OpenAPI Specification

The API follows OpenAPI 3.0 specification. You can import the `swagger.json` file into tools like:

- **Postman** - For API testing
- **Insomnia** - Alternative API client
- **OpenAPI Generator** - Generate client SDKs
- **Stoplight** - API design and documentation

