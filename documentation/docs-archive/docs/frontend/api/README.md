---
sidebar_position: 1
title: Frontend API Reference
description: TypeScript API reference for DevHunt frontend
---

# Frontend API Reference

This section contains the TypeScript API reference for the DevHunt frontend application, generated using TypeDoc.

## 📚 Generated Documentation

The API documentation is automatically generated from TypeScript source code using TypeDoc. It includes:

- **Type Definitions** - All TypeScript interfaces, types, and enums
- **Function Signatures** - Parameter types and return types
- **Class Documentation** - Component classes and utilities
- **Hooks Documentation** - Custom React hooks with examples

## 🔗 Links

- **[Full TypeDoc Documentation](/frontend/api/)** - Complete API reference
- **[Interactive API Explorer](/docs/api/interactive)** - Test API endpoints
- **[Swagger Documentation](/docs/api/swagger-ui)** - REST API documentation

## 📖 How to Use

### Finding Documentation

1. **Navigate to the API reference** using the links above
2. **Search for specific types** using the search functionality
3. **Browse by category** (Components, Hooks, Types, Utils)

### Understanding the Documentation

- **Properties marked with ?** are optional
- **Union types (A | B)** indicate multiple possible types
- **Generic types \<T\>** indicate parameterized types
- **Function overloads** show different ways to call the same function

### Example Usage

```typescript
// Using documented types
import type { Project, User } from '@/types/api';

interface ProjectCardProps {
  project: Project;
  owner: User;
  onEdit?: (project: Project) => void;
}

function ProjectCard({ project, owner, onEdit }: ProjectCardProps) {
  return (
    <div>
      <h3>{project.name}</h3>
      <p>By {owner.name}</p>
      {onEdit && (
        <button onClick={() => onEdit(project)}>
          Edit
        </button>
      )}
    </div>
  );
}
```

## 🔧 Development Workflow

### Regenerating Documentation

The API documentation is automatically regenerated during the build process. To manually regenerate:

```bash
# Generate TypeDoc documentation
npm run gen:typedoc

# Build the full site including API docs
npm run build:site
```

### Adding Documentation Comments

Use JSDoc-style comments for better generated documentation:

```typescript
/**
 * Represents a project in the system
 */
export interface Project {
  /** Unique identifier for the project */
  id: string;

  /** Display name of the project */
  name: string;

  /** Detailed description */
  description?: string;

  /** When the project was created */
  createdAt: Date;

  /** Current project status */
  status: ProjectStatus;
}

/**
 * Creates a new project with the given parameters
 * @param name - The name of the new project
 * @param description - Optional description for the project
 * @returns Promise resolving to the created project
 * @throws {ValidationError} When project data is invalid
 * @throws {AuthorizationError} When user lacks permissions
 */
export async function createProject(
  name: string,
  description?: string
): Promise<Project> {
  // Implementation here
}
```

## 📋 Documented Modules

### Core Types
- **API Types** - REST API request/response types
- **Entity Types** - Domain entities (Project, User, Team)
- **UI Types** - Component prop types and configurations

### Components
- **Layout Components** - Page layouts and navigation
- **Feature Components** - Business logic components
- **UI Components** - Reusable UI elements

### Hooks
- **Data Hooks** - API data fetching and caching
- **State Hooks** - Local component state management
- **Utility Hooks** - Common functionality (auth, routing, etc.)

### Utilities
- **API Client** - HTTP request utilities
- **Formatters** - Data formatting functions
- **Validators** - Input validation functions
- **Constants** - Application constants and enums

## 🔍 Troubleshooting

### Documentation Not Found
If the API documentation is not accessible:

1. **Check if it was generated** - Run `npm run gen:typedoc`
2. **Verify build process** - Ensure `npm run build:site` completes successfully
3. **Check file paths** - Confirm static files are in the correct location

### Outdated Documentation
If the documentation doesn't reflect recent changes:

1. **Regenerate docs** - Run the generation scripts
2. **Clear cache** - Run `npm run clear` to clear Docusaurus cache
3. **Rebuild** - Run `npm run build` to rebuild the site

### Type Errors in Documentation
If there are TypeScript errors affecting documentation generation:

1. **Fix type errors** - Resolve all TypeScript compilation errors
2. **Check TypeDoc config** - Verify `typedoc.json` configuration
3. **Update dependencies** - Ensure TypeDoc and related packages are up to date

## 📖 Related Documentation

- **[Architecture Overview](/docs/architecture/architecture-overview)** - System architecture
- **[How to Debug](/docs/frontend/how-to-debug)** - Debugging techniques
- **[Components Guide](/docs/frontend/components/layout)** - Component usage