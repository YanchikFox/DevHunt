import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';
import path from 'path';

function loadSidebar(relPath: string, label: string): any {
  try {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const m = require(relPath + '.js');
    return m?.apisidebar ?? m?.default?.apisidebar ?? null;
  } catch {
    try {
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const m = require(relPath);
      return m?.apisidebar ?? m?.default?.apisidebar ?? null;
    } catch {
      console.warn(
        `[sidebars] ${label} sidebar not found at ${path.resolve(__dirname, relPath + '.*')} - skipping`,
      );
      return null;
    }
  }
}

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

function makeApiCategory(label: string, items: any[]) {
  return {type: 'category' as const, label, items};
}

const generatedSwaggerSidebar = loadSidebar('./docs/api/swagger/sidebar', 'Core API');
const swaggerCategory =
  generatedSwaggerSidebar != null && generatedSwaggerSidebar.length > 0
    ? [makeApiCategory('Core API (OpenAPI)', generatedSwaggerSidebar)]
    : [];

const generatedAuthSidebar = loadSidebar('./docs/api/auth-swagger/sidebar', 'Auth API');
const authSwaggerCategory =
  generatedAuthSidebar != null && generatedAuthSidebar.length > 0
    ? [makeApiCategory('Auth Service API (OpenAPI)', generatedAuthSidebar)]
    : [];

const generatedMlSidebar = loadSidebar('./docs/api/ml-swagger/sidebar', 'ML API');
const mlSwaggerCategory =
  generatedMlSidebar != null && generatedMlSidebar.length > 0
    ? [makeApiCategory('ML Service API (OpenAPI)', generatedMlSidebar)]
    : [];

const sidebars: SidebarsConfig = {
  // Guides sidebar - getting started, architecture, best practices
  tutorialSidebar: [
    'intro',
    {
      type: 'link',
      label: '🎨 Frontend Docs',
      href: '/docs/frontend/intro',
    },
    {
      type: 'link',
      label: '⚙️ Backend Docs',
      href: '/docs/backend/intro',
    },
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'getting-started/quickstart',
        'getting-started/overview',
        'getting-started/repo-overview',
        'getting-started/setup',
        'getting-started/run-stack',
        'getting-started/configuration',
      ],
    },
    {
      type: 'category',
      label: 'Operations',
      items: [
        'operations/overview',
        'operations/security-checklist',
        'operations/incident-response',
        'operations/backup-recovery',
      ],
    },
    {
      type: 'category',
      label: 'Architecture',
      items: [
        'architecture/overview',
        'architecture/system-overview',
        'architecture/high-level-architecture',
        'architecture/chat-system',
        'architecture/security',
        {
          type: 'category',
          label: 'Diagrams',
          items: [
            'architecture/generated/service-topology',
            'architecture/generated/database-erd',
            'architecture/generated/event-flow',
            'architecture/generated/frontend-routes',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      items: [
        'reference/glossary',
      ],
    },
  ],

  // API Reference sidebar - REST API documentation
  apiSidebar: [
    'api/intro',
    {
      type: 'link',
      label: 'Swagger UI (Try API)',
      href: '/swagger-ui/',
    },
    ...swaggerCategory,
    ...authSwaggerCategory,
    ...mlSwaggerCategory,
  ],

  // Frontend SDK sidebar - React components, hooks, utilities
  frontendSidebar: [
    'frontend/overview',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'frontend/how-to-run',
        'frontend/how-to-debug',
        'frontend/troubleshooting',
      ],
    },
    {
      type: 'category',
      label: 'API Reference (TypeDoc)',
      link: {
        type: 'doc',
        id: 'frontend/api/README',
      },
      items: [
        'frontend/api/README',
        {
          type: 'category',
          label: 'Hooks',
          items: [
            'frontend/api/hooks/use-current-user/README',
            'frontend/api/hooks/use-debounced-value/README',
            'frontend/api/hooks/use-gallery-lightbox/README',
            'frontend/api/hooks/use-notifications/README',
            'frontend/api/hooks/use-project-dialogs/README',
            'frontend/api/hooks/use-project-status-actions/README',
            'frontend/api/hooks/use-project-task-board/README',
            'frontend/api/hooks/use-project-task-handlers/README',
            'frontend/api/hooks/use-toast/README',
            'frontend/api/hooks/useChat/README',
          ],
        },
        {
          type: 'category',
          label: 'API Client',
          items: [
            'frontend/api/lib/api/agent-types/README',
            'frontend/api/lib/api/ai-types/README',
            'frontend/api/lib/api/client/README',
            'frontend/api/lib/api/csrf/README',
            'frontend/api/lib/api/task-board-types/README',
          ],
        },
        'frontend/api/lib/realtime/client/README',
        'frontend/api/lib/signalr/README',
        'frontend/api/lib/store/chatStore/README',
        'frontend/api/lib/utils/README',
        'frontend/api/schema/README',
      ],
    },
    'frontend/onboarding',
  ],

  // Backend Core sidebar - .NET services, database models
  backendSidebar: [
    'backend/overview',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'backend/how-to-run',
        'backend/how-to-debug',
        'backend/troubleshooting',
      ],
    },
    {
      type: 'category',
      label: 'API Reference',
      items: [
        'backend/api/versioning-strategy',
        {
          type: 'link',
          label: 'Full API (DocFX)',
          href: '/docfx/index.html',
        },
        {
          type: 'link',
          label: 'Core API (OpenAPI)',
          href: '/docs/api/intro',
        },
      ],
    },
    {
      type: 'category',
      label: 'Services',
      items: [
        'backend/services/core',
        'backend/services/auth',
      ],
    },
    {
      type: 'category',
      label: 'Database',
      items: [
        'backend/database/overview',
        'backend/database/migrations',
      ],
    },
  ],
};

export default sidebars;
