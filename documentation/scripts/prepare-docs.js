const {existsSync, mkdirSync, copyFileSync, rmSync, readFileSync, writeFileSync} = require('fs');
const {dirname, resolve} = require('path');

const rootDir = resolve(__dirname, '..');
const docsDir = resolve(rootDir, 'docs');

const swaggerSources = [
  resolve(rootDir, '..', 'swagger.json'),
  resolve(rootDir, '..', 'DevHunt.CoreApi', 'swagger.json'),
];
const swaggerUrl =
  process.env.SWAGGER_URL || 'http://localhost:7002/swagger/v1/swagger.json';

function cleanGenerated() {
  const skipTypedoc = process.env.SKIP_TYPEDOC === 'true';
  const generatedDirs = [
    resolve(docsDir, 'api', 'swagger'),
    resolve(docsDir, 'api', 'auth-swagger'),
    resolve(docsDir, 'api', 'ml-swagger'),
  ];

  if (!skipTypedoc) {
    generatedDirs.push(resolve(docsDir, 'frontend', 'api'));
  }

  generatedDirs.forEach(dir => {
    rmSync(dir, {recursive: true, force: true});
    console.log(`[prepare-docs] Removed generated directory: ${dir}`);
  });
}

function syncSwagger() {
  const target = resolve(rootDir, 'static', 'api', 'swagger.json');
  mkdirSync(dirname(target), {recursive: true});

  const localSwagger = swaggerSources.find(source => existsSync(source));

  if (!process.env.SWAGGER_URL && localSwagger) {
    copyFileSync(localSwagger, target);
    console.log(`[prepare-docs] Synced swagger.json from ${localSwagger} -> ${target}`);
    normalizeSwagger(target);
    return;
  }

  fetchSwagger(target)
    .then(() => normalizeSwagger(target))
    .catch(err => {
      console.warn(`[prepare-docs] Fetch from ${swaggerUrl} failed (${err.message}), falling back to local file...`);
      if (!localSwagger) {
        throw new Error(
          'swagger.json not found. Expected it at repo root or DevHunt.CoreApi/swagger.json. Please generate the OpenAPI spec first.',
        );
      }
      copyFileSync(localSwagger, target);
      console.log(`[prepare-docs] Synced swagger.json from ${localSwagger} -> ${target}`);
      normalizeSwagger(target);
    });
}

function syncAuthSwagger() {
  const target = resolve(rootDir, 'static', 'api', 'auth-swagger.json');
  mkdirSync(dirname(target), {recursive: true});
  const source = resolve(rootDir, '..', 'auth-swagger.json');
  if (existsSync(source)) {
    copyFileSync(source, target);
    console.log(`[prepare-docs] Synced auth-swagger.json -> ${target}`);
    normalizeSwagger(target);
  } else {
    assertGeneratedSourceExists('auth-swagger.json', target);
  }
}

function syncMlOpenApi() {
  const target = resolve(rootDir, 'static', 'api', 'ml-openapi.json');
  mkdirSync(dirname(target), {recursive: true});
  const source = resolve(rootDir, '..', 'ml-openapi.json');
  if (existsSync(source)) {
    copyFileSync(source, target);
    console.log(`[prepare-docs] Synced ml-openapi.json -> ${target}`);
  } else {
    assertGeneratedSourceExists('ml-openapi.json', target);
  }
}

function assertGeneratedSourceExists(filename, fallbackPath) {
  if (process.env.ALLOW_DOCS_FALLBACK === 'true' && existsSync(fallbackPath)) {
    console.warn(`[prepare-docs] ${filename} not found at repo root — keeping existing static fallback because ALLOW_DOCS_FALLBACK=true`);
    return;
  }

  throw new Error(
    `${filename} not found at repo root. Generate it before preparing docs; refusing to publish stale fallback output.`,
  );
}

function normalizeSwagger(target) {
  const spec = JSON.parse(readFileSync(target, 'utf8'));

  if (!spec.paths) {
    console.warn('[prepare-docs] swagger.json has no paths - nothing to normalize');
    return;
  }

  let patched = 0;
  for (const [route, methods] of Object.entries(spec.paths)) {
    if (!methods || typeof methods !== 'object') continue;
    for (const [method, operation] of Object.entries(methods)) {
      if (!operation || typeof operation !== 'object') continue;
      if (!operation.operationId) {
        const clean = `${method}-${route}`.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '');
        operation.operationId = clean || `${method}-${patched}`;
        patched++;
      }
      if (!operation.summary) {
        operation.summary = `${method.toUpperCase()} ${route}`;
        patched++;
      }
    }
  }

  writeFileSync(target, JSON.stringify(spec, null, 2));
  console.log(`[prepare-docs] Normalized swagger.json (${patched} updates)`);
}

async function fetchSwagger(target) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 5000);
  const res = await fetch(swaggerUrl, {signal: controller.signal});
  clearTimeout(timeout);

  if (!res.ok) {
    throw new Error(`status ${res.status}`);
  }

  const body = await res.text();
  writeFileSync(target, body);
  console.log(`[prepare-docs] Downloaded swagger.json from ${swaggerUrl} -> ${target}`);
}

function main() {
  cleanGenerated();
  syncSwagger();
  syncAuthSwagger();
  syncMlOpenApi();
}

main();
