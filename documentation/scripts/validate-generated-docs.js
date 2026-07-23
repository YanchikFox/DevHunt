const {existsSync, readFileSync} = require('fs');
const {resolve} = require('path');

const docsRoot = resolve(__dirname, '..');

const requiredFiles = [
  'static/api/swagger.json',
  'static/api/auth-swagger.json',
  'static/api/ml-openapi.json',
  'docs/api/swagger/get-api-projects.api.mdx',
  'docs/api/auth-swagger/post-api-auth-login.api.mdx',
  'docs/frontend/api/README.md',
  'docs/architecture/generated/service-topology.md',
  'static/docfx/index.html',
  'static/swagger-ui/index.html',
  'static/swagger-ui/swagger-ui.css',
  'static/swagger-ui/swagger-ui-bundle.js',
  'static/swagger-ui/swagger-ui-standalone-preset.js',
];

const specs = [
  ['Core API', 'static/api/swagger.json'],
  ['Auth API', 'static/api/auth-swagger.json'],
  ['ML API', 'static/api/ml-openapi.json'],
];

const forbiddenGeneratedText = [
  ['docs/frontend/api/README.md', 'TypeDoc output is not available'],
  ['static/docfx/index.html', 'DocFX output is not available'],
];

let failed = false;

for (const relPath of requiredFiles) {
  const absPath = resolve(docsRoot, relPath);
  if (!existsSync(absPath)) {
    console.error(`[validate-generated-docs] Missing ${relPath}`);
    failed = true;
  }
}

for (const [label, relPath] of specs) {
  const absPath = resolve(docsRoot, relPath);
  if (!existsSync(absPath)) {
    continue;
  }

  const spec = JSON.parse(readFileSync(absPath, 'utf8'));
  const pathCount = Object.keys(spec.paths ?? {}).length;
  if (pathCount === 0) {
    if (process.env.ALLOW_DOCS_FALLBACK === 'true') {
      console.warn(`[validate-generated-docs] ${label} spec has no paths (fallback mode): ${relPath}`);
    } else {
      console.error(`[validate-generated-docs] ${label} spec has no paths: ${relPath}`);
      failed = true;
    }
  } else {
    console.log(`[validate-generated-docs] ${label}: ${pathCount} path(s)`);
  }
}

for (const [relPath, forbiddenText] of forbiddenGeneratedText) {
  const absPath = resolve(docsRoot, relPath);
  if (!existsSync(absPath)) {
    continue;
  }

  const content = readFileSync(absPath, 'utf8');
  if (content.includes(forbiddenText)) {
    if (process.env.ALLOW_DOCS_FALLBACK === 'true') {
      console.warn(`[validate-generated-docs] ${relPath} is placeholder output (fallback mode)`);
    } else {
      console.error(`[validate-generated-docs] ${relPath} is placeholder output`);
      failed = true;
    }
  }
}

if (failed) {
  process.exit(1);
}

console.log('[validate-generated-docs] Generated documentation looks complete.');
