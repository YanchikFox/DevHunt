/**
 * gen-diagrams.js
 *
 * Generates 4 Mermaid architecture diagrams from source code:
 *   1. service-topology.md  — docker-compose + nginx routing
 *   2. database-erd.md      — DbContext entities + FK relationships
 *   3. event-flow.md        — Outbox events published + consumed
 *   4. frontend-routes.md   — Next.js [locale] route tree
 *
 * Output: documentation/docs/architecture/generated/*.md
 * Each file is a Docusaurus MDX page with a Mermaid fenced code block.
 * Requires @docusaurus/theme-mermaid configured in docusaurus.config.ts.
 */

const {readFileSync, writeFileSync, mkdirSync, readdirSync, statSync, existsSync} = require('fs');
const {resolve, relative} = require('path');

const repoRoot = resolve(__dirname, '..', '..');
const outDir = resolve(__dirname, '..', 'docs', 'architecture', 'generated');
mkdirSync(outDir, {recursive: true});

// ─── helpers ─────────────────────────────────────────────────────────────────

function readFile(relPath) {
  const abs = resolve(repoRoot, relPath);
  if (!existsSync(abs)) return null;
  return readFileSync(abs, 'utf8');
}

function writeDoc(filename, frontmatter, title, notice, mermaidKind, lines) {
  const body = [
    frontmatter,
    '',
    `# ${title}`,
    '',
    `> ${notice}`,
    '',
    '```mermaid',
    mermaidKind,
    '',
    ...lines,
    '```',
    '',
  ].join('\n');
  writeFileSync(resolve(outDir, filename), body);
  console.log(`[gen-diagrams] ${filename} written`);
}

// ─── 1. SERVICE TOPOLOGY ─────────────────────────────────────────────────────

function genTopology() {
  const composeRaw = readFile('docker-compose.yml');
  if (!composeRaw) { console.warn('[gen-diagrams] docker-compose.yml not found — skipping topology'); return; }

  // Parse depends_on relationships
  const composeLines = composeRaw.split('\n');
  let currentSvc = null;
  let inDepends = false;
  const deps = {};
  const KNOWN_SERVICES = new Set([
    'db', 'cache-service', 'message-broker', 'object-storage',
    'db-migrator', 'auth-service', 'core-api', 'ml-service',
    'notification-service', 'integration-gateway', 'api-gateway',
    'frontend', 'openobserve', 'openobserve-init', 'backup-service', 'code-analyzer',
  ]);

  for (const line of composeLines) {
    const svcMatch = line.match(/^  ([a-z][a-z0-9_-]+):\s*$/);
    const depHeader = /^    depends_on:/.test(line);
    const depItem = line.match(/^      ([a-z][a-z0-9_-]+):/);
    const otherFourIndent = /^    [a-z]/.test(line) && !depHeader;

    if (svcMatch) { currentSvc = svcMatch[1]; inDepends = false; deps[currentSvc] = deps[currentSvc] ?? []; }
    else if (depHeader && currentSvc) { inDepends = true; }
    else if (inDepends && depItem) { deps[currentSvc].push(depItem[1]); }
    else if (otherFourIndent) { inDepends = false; }
  }

  // Parse nginx upstream → service mappings:  set $upstream_X  service:port
  const nginxRaw = readFile('nginx/nginx.conf');
  const nginxRoutes = []; // [{path, service}]
  if (nginxRaw) {
    // Collect upstream variable → service name
    const upstreamMap = {};
    for (const m of nginxRaw.matchAll(/set\s+\$upstream_(\w+)\s+([a-z][a-z0-9_-]+):/g)) {
      upstreamMap[m[1]] = m[2];
    }
    // Collect location path → $upstream_var
    for (const m of nginxRaw.matchAll(/location\s+(\/[^\s{]*)\s*\{[^}]*\$upstream_(\w+)/gs)) {
      const svc = upstreamMap[m[2]];
      if (svc) nginxRoutes.push({path: m[1].replace(/\*/g, '…'), service: svc});
    }
  }

  // Deduplicate nginx routes by service (keep first path per service)
  const seenNginx = new Set();
  const dedupedNginx = nginxRoutes.filter(r => {
    if (seenNginx.has(r.service)) return false;
    seenNginx.add(r.service);
    return true;
  });

  const out = [
    '  %% ── External traffic ──',
    '  Client([Browser / Client])',
    '  Client --> GW["api-gateway\\n(Nginx TLS)"]',
    '',
    '  %% ── Nginx routing ──',
  ];

  for (const r of dedupedNginx) {
    const nodeId = r.service.replace(/-/g, '_');
    out.push(`  GW -->|"${r.path}"| ${nodeId}`);
  }

  out.push('', '  %% ── Startup dependencies (docker-compose depends_on) ──');
  for (const [svc, depList] of Object.entries(deps)) {
    if (!KNOWN_SERVICES.has(svc)) continue;
    const svcId = svc.replace(/-/g, '_');
    for (const dep of depList) {
      if (!KNOWN_SERVICES.has(dep)) continue;
      const depId = dep.replace(/-/g, '_');
      out.push(`  ${depId} -.->|"waits for"| ${svcId}`);
    }
  }

  out.push('', '  %% ── Cross-service calls (from code) ──');
  out.push('  core_api -->|"AMQP publish"| message_broker');
  out.push('  notification_service -->|"AMQP consume"| message_broker');
  out.push('  core_api -->|"POST /results"| code_analyzer');
  out.push('  ml_service -->|"asyncpg SQL"| db');
  out.push('  backup_service -->|"pg_dump"| db');

  out.push('', '  %% ── Styling ──');
  out.push('  classDef infra fill:#e8f5e9,stroke:#388e3c,color:#1b5e20');
  out.push('  classDef app fill:#e3f2fd,stroke:#1976d2,color:#0d47a1');
  out.push('  classDef gw fill:#fff3e0,stroke:#f57c00,color:#e65100');
  out.push('  classDef obs fill:#f3e5f5,stroke:#7b1fa2,color:#4a148c');
  out.push('  class db,cache_service,message_broker,object_storage infra');
  out.push('  class auth_service,core_api,ml_service,notification_service,integration_gateway,frontend,code_analyzer,db_migrator app');
  out.push('  class GW gw');
  out.push('  class openobserve,openobserve_init,backup_service obs');

  writeDoc(
    'service-topology.md',
    '---\nsidebar_position: 1\nhide_table_of_contents: true\n---',
    'Service Topology',
    'Auto-generated from `docker-compose.yml` and `nginx/nginx.conf`. Do not edit manually.',
    'graph TD',
    out,
  );
}

// ─── 2. DATABASE ERD ─────────────────────────────────────────────────────────

function genErd() {
  const dbContextRaw = readFile('DevHunt.Infrastructure/DevHuntDbContext.cs');
  if (!dbContextRaw) { console.warn('[gen-diagrams] DevHuntDbContext.cs not found — skipping ERD'); return; }

  // Extract entity class names from DbSet<T> declarations
  const entityNames = new Set();
  for (const m of dbContextRaw.matchAll(/DbSet<([A-Z][A-Za-z]+)>/g)) {
    entityNames.add(m[1]);
  }

  // Scan Infrastructure .cs files for FK properties: public Guid {EntityName}Id
  // to build a relationship map
  const infraDir = resolve(repoRoot, 'DevHunt.Infrastructure');
  const entityFiles = [];
  function scanDir(dir) {
    for (const entry of readdirSync(dir)) {
      const full = resolve(dir, entry);
      if (statSync(full).isDirectory() && entry !== 'bin' && entry !== 'obj' && entry !== 'Migrations') {
        scanDir(full);
      } else if (entry.endsWith('.cs') && entry !== 'DevHuntDbContext.cs') {
        entityFiles.push(full);
      }
    }
  }
  scanDir(infraDir);

  // Domain groupings — used for the plain-text entity inventory below the diagram
  const domainGroups = {
    'Core': ['User', 'Project', 'ProjectBoost', 'TeamMember', 'Invitation', 'Review', 'Notification'],
    'Task': ['TaskItem', 'TaskColumn', 'TaskLink', 'TaskAttachment', 'TaskBoardSettings'],
    'Chat': ['Conversation', 'ConversationParticipant', 'Message', 'AiMessageDetails', 'MessageReaction', 'ChannelRoleDefinition'],
    'AI': ['AiPlan', 'AiOperationLog', 'ProjectArtifact', 'UserApiKey', 'LlmModel'],
    'Showcase': ['ShowcaseProject', 'ShowcaseComment'],
    'Skills': ['Skill', 'SkillAlias', 'UserSkill', 'UserSkillEntry', 'ProjectTechStack', 'ProjectRole'],
    'Files': ['ProjectFile', 'ProjectDocument'],
    'Activity': ['ActivityRecord', 'ProjectNewsPost', 'ProjectSubscription', 'UserFollow', 'NewsPostLike', 'NewsPostComment'],
    'Moderation': ['ModerationReport', 'SupportTicket', 'TicketMessage', 'TicketHistory', 'ProjectIssue'],
    'Community': ['FeedbackItem', 'FeedbackVote', 'FeedbackComment'],
    'Integrations': ['Integration', 'Recommendation', 'CodeAnalysisResult', 'CodeAnalysisEmbedding'],
    'Admin': ['Achievement', 'UserAchievement', 'AuditLog', 'AdminNote', 'PlatformSetting', 'FeatureFlag', 'UserPrivacySettings'],
    'Auth': ['RefreshToken', 'OutboxEvent'],
  };

  // Core model whitelist: the architecturally central entities.
  // Full entity list (45 total) is in the inventory section below the diagram.
  const CORE = new Set([
    'User', 'Project', 'TeamMember', 'TaskItem', 'TaskColumn',
    'Invitation', 'Conversation', 'Message', 'Notification',
    'Integration', 'Recommendation', 'Achievement', 'UserAchievement',
  ]);

  // Collect FK edges between CORE entities only
  const coreEdges = new Set();
  for (const filePath of entityFiles) {
    const className = filePath.replace(/.*\//, '').replace('.cs', '');
    if (!CORE.has(className)) continue;
    const content = readFileSync(filePath, 'utf8');
    for (const m of content.matchAll(/public\s+Guid\s+([A-Z][A-Za-z]+)Id\b/g)) {
      const refName = m[1];
      if (CORE.has(refName) && refName !== className) {
        coreEdges.add(`  ${refName} ||--o{ ${className} : ""`);
      }
    }
  }

  // Entity definitions (minimal — just PK shown)
  const entityDefs = [];
  for (const e of [...CORE].sort()) {
    entityDefs.push(`  ${e} { Guid id }`);
  }

  const diagramLines = [
    ...entityDefs,
    '',
    '  %% FK relationships (auto-detected from public Guid {Entity}Id properties)',
    ...[...coreEdges].sort(),
  ];

  // Build plain-text entity inventory grouped by domain
  const totalEntities = entityNames.size;
  const inventoryLines = [
    '',
    `## Full Entity Inventory (${totalEntities} entities)`,
    '',
    '> Auto-detected from `DbSet<T>` declarations in `DevHuntDbContext.cs`. Source of truth is the code.',
    '',
  ];
  for (const [domain, entities] of Object.entries(domainGroups)) {
    const present = entities.filter(e => entityNames.has(e));
    if (present.length === 0) continue;
    inventoryLines.push(`**${domain}** — ${present.join(', ')}`);
    inventoryLines.push('');
  }

  // Write the file manually (writeDoc only handles single-diagram pages)
  const frontmatter = '---\nsidebar_position: 2\nhide_table_of_contents: true\n---';
  const body = [
    frontmatter,
    '',
    '# Database Entity Relationships',
    '',
    '> Auto-generated from `DevHunt.Infrastructure/` entity FK properties.',
    '> Shows the 13 architecturally central entities. Do not edit manually.',
    '',
    '```mermaid',
    'erDiagram',
    '',
    ...diagramLines,
    '```',
    '',
    ...inventoryLines,
  ].join('\n');
  writeFileSync(resolve(outDir, 'database-erd.md'), body);
  console.log('[gen-diagrams] database-erd.md written');
}

// ─── 3. EVENT FLOW ───────────────────────────────────────────────────────────

function genEventFlow() {
  const eventBusRaw = readFile('DevHunt.CoreApi/Services/EventBusService.cs');
  if (!eventBusRaw) { console.warn('[gen-diagrams] EventBusService.cs not found — skipping event flow'); return; }

  // Extract event factory methods: static DomainEvent CreateXxx(...) → new("event.type", ...)
  const eventTypes = new Set();
  for (const m of eventBusRaw.matchAll(/new\s*\(\s*"([a-z][a-z0-9.]+)"/g)) {
    eventTypes.add(m[1]);
  }

  // Extract exchange name
  const exchangeMatch = eventBusRaw.match(/const string ExchangeName\s*=\s*"([^"]+)"/);
  const exchangeName = exchangeMatch ? exchangeMatch[1] : 'devhunt.events';

  // Read notification-service consumer bindings
  const consumerRaw = readFile('notification-service/src/services/rabbitmqConsumer.js');
  const consumerKeys = [];
  if (consumerRaw) {
    for (const m of consumerRaw.matchAll(/"([a-z][a-z0-9.*]+)"/g)) {
      if (m[1].includes('.')) consumerKeys.push(m[1]);
    }
  }

  // Group event types by entity prefix
  const eventGroups = {};
  for (const et of [...eventTypes].sort()) {
    const prefix = et.split('.')[0];
    (eventGroups[prefix] = eventGroups[prefix] ?? []).push(et);
  }

  // Separate consumer binding keys (may use wildcards) from published event types
  // Consumer binds: static strings with wildcards e.g. "project.*"
  // Publisher emits: specific e.g. "project.created", "project.updated"
  const consumerBindings = [...new Set(consumerKeys)].filter(k => k.includes('.')).sort();
  const publishedTypes = [...eventTypes].sort();

  const out = [
    '  CoreApi["core-api\\n(OutboxEventService)"]',
    `  DB[("PostgreSQL\\nOutboxEvents")]`,
    `  Worker["core-api\\n(OutboxWorker)"]`,
    `  Exchange[("RabbitMQ\\n${exchangeName}\\ntopic exchange")]`,
    '  NotifSvc["notification-service\\n(RabbitMQ consumer)"]',
    `  Note["⚠️ Consumer binds with wildcards\\ne.g. project.* matches project.created,\\nproject.updated, project.archived …\\nPublished types listed below subgraph."]`,
    '',
    '  CoreApi -->|"INSERT OutboxEvent"| DB',
    '  Worker -->|"SELECT WHERE Status=Pending"| DB',
    `  Worker -->|"AMQP publish\\nrouting key: {entity}.{eventType}"| Exchange`,
    '',
    '  %% Consumer wildcard bindings (from rabbitmqConsumer.js routingKeys array)',
  ];

  for (const key of consumerBindings) {
    out.push(`  Exchange -->|"${key}"| NotifSvc`);
  }

  out.push('  Exchange -.-> Note');

  // Build a published-types summary node
  const typesByGroup = [];
  for (const [group, types] of Object.entries(eventGroups)) {
    typesByGroup.push(`${group}: ${types.map(t => t.split('.')[1]).join(', ')}`);
  }
  out.push('');
  out.push(`  Published["📤 Published event types\\n${typesByGroup.join('\\n')}"]`);
  out.push('  Worker -->|"emits"| Published');

  out.push('', '  %% Styling');
  out.push('  classDef svc fill:#e3f2fd,stroke:#1976d2,color:#0d47a1');
  out.push('  classDef store fill:#e8f5e9,stroke:#388e3c,color:#1b5e20');
  out.push('  classDef note fill:#fff9c4,stroke:#f9a825,color:#333');
  out.push('  classDef pub fill:#e8f5e9,stroke:#2e7d32,color:#1b5e20');
  out.push('  class CoreApi,Worker,NotifSvc svc');
  out.push('  class DB,Exchange store');
  out.push('  class Note note');
  out.push('  class Published pub');

  writeDoc(
    'event-flow.md',
    '---\nsidebar_position: 3\nhide_table_of_contents: true\n---',
    'Event Flow (Outbox → RabbitMQ)',
    'Auto-generated from `DevHunt.CoreApi/Services/EventBusService.cs` and `notification-service/src/services/rabbitmqConsumer.js`. Do not edit manually.',
    'flowchart LR',
    out,
  );
}

// ─── 4. FRONTEND ROUTES ──────────────────────────────────────────────────────

function genFrontendRoutes() {
  const localeDir = resolve(repoRoot, 'frontend', 'src', 'app', '[locale]');
  if (!existsSync(localeDir)) { console.warn('[gen-diagrams] frontend/src/app/[locale]/ not found — skipping routes'); return; }

  // Recursively collect page.tsx paths relative to localeDir
  const pages = [];
  function scanPages(dir, relBase) {
    for (const entry of readdirSync(dir)) {
      const full = resolve(dir, entry);
      const rel = relBase ? `${relBase}/${entry}` : entry;
      if (statSync(full).isDirectory()) {
        scanPages(full, rel);
      } else if (entry === 'page.tsx') {
        pages.push(relBase || '/');
      }
    }
  }
  scanPages(localeDir, '');

  // Group routes by route group prefix
  const groups = {};
  for (const p of pages) {
    // route group = first segment if it starts with (
    const match = p.match(/^\(([^)]+)\)(\/.*)?$/);
    const group = match ? match[1] : (p.startsWith('dashboard') ? 'dashboard' : 'root');
    (groups[group] = groups[group] ?? []).push(p || '/');
  }

  // Use graph LR (left→right) so the tree grows vertically rather than
  // spreading into an unreadable wide strip under graph TD.
  const out = ['  Root["app/[locale]"]'];
  const nodeIds = new Map();
  let counter = 0;

  function nodeId(path) {
    if (!nodeIds.has(path)) nodeIds.set(path, `N${counter++}`);
    return nodeIds.get(path);
  }

  const groupNodeIds = [];
  for (const [group, paths] of Object.entries(groups)) {
    const groupId = `G_${group.replace(/[^a-z0-9]/gi, '_')}`;
    groupNodeIds.push(groupId);
    out.push(`  ${groupId}["(${group})"]`);
    out.push(`  Root --> ${groupId}`);
    for (const p of paths.sort()) {
      const label = p.replace(/^\([^)]+\)\//, '') || '/';
      const id = nodeId(p);
      out.push(`  ${groupId} --> ${id}["/${label}"]`);
    }
    out.push('');
  }

  out.push('  %% Styling');
  out.push('  classDef grp fill:#fff3e0,stroke:#f57c00,color:#e65100,font-weight:bold');
  out.push('  classDef page fill:#e3f2fd,stroke:#1976d2,color:#0d47a1');
  out.push('  classDef root fill:#e8f5e9,stroke:#388e3c,color:#1b5e20,font-weight:bold');
  out.push('  class Root root');
  out.push(`  class ${groupNodeIds.join(',')} grp`);
  if (nodeIds.size > 0) out.push(`  class ${[...nodeIds.values()].join(',')} page`);

  writeDoc(
    'frontend-routes.md',
    '---\nsidebar_position: 4\nhide_table_of_contents: true\n---',
    'Frontend Route Tree',
    'Auto-generated from `frontend/src/app/[locale]/` directory structure. Do not edit manually.',
    'graph LR',
    out,
  );
}

// ─── Run all ─────────────────────────────────────────────────────────────────
genTopology();
genErd();
genEventFlow();
genFrontendRoutes();
