const {copyFileSync, existsSync, writeFileSync} = require('fs');
const {spawnSync} = require('child_process');
const {resolve} = require('path');

const repoRoot = resolve(__dirname, '..', '..');
const mlServiceDir = resolve(repoRoot, 'ml-service');
const outputPath = resolve(repoRoot, 'ml-openapi.json');
const fallbackPath = resolve(repoRoot, 'documentation', 'static', 'api', 'ml-openapi.json');

const STUB = {
  openapi: '3.1.0',
  info: {title: 'DevHunt ML Service', version: '1.0.0'},
  paths: {},
};

function exitWithFallback(reason) {
  if (process.env.ALLOW_DOCS_FALLBACK !== 'true') {
    console.error(`[gen-mlapi] ${reason}. Refusing to publish fallback OpenAPI output.`);
    process.exit(1);
  }

  if (existsSync(outputPath)) {
    console.warn(`[gen-mlapi] ${reason}. Keeping existing output.`);
    process.exit(0);
  }
  if (existsSync(fallbackPath)) {
    copyFileSync(fallbackPath, outputPath);
    console.warn(`[gen-mlapi] ${reason}. Using static fallback.`);
    process.exit(0);
  }
  console.warn(`[gen-mlapi] ${reason}. Writing stub spec.`);
  writeFileSync(outputPath, JSON.stringify(STUB, null, 2));
  process.exit(0);
}

if (!existsSync(mlServiceDir)) {
  exitWithFallback('ml-service directory not found');
}

// deps.py lines 17-22 raise ValueError at module import if DATABASE_URL is absent.
// The check is presence-only — FastAPI never opens a DB connection at import time.
// If this script starts failing after a deps.py change, check whether the
// validation became stricter (e.g. started parsing or connecting on import).
const env = {
  ...process.env,
  DATABASE_URL: process.env.DATABASE_URL || 'postgresql://localhost/dummy',
  ENVIRONMENT: process.env.ENVIRONMENT || 'development',
};

console.log('[gen-mlapi] Exporting FastAPI OpenAPI spec...');
const result = spawnSync(
  'python3',
  ['-c', 'import json; from main import app; print(json.dumps(app.openapi()))'],
  {cwd: mlServiceDir, env, encoding: 'utf8', stdio: ['inherit', 'pipe', 'inherit']},
);

if (result.status !== 0 || !result.stdout?.trim()) {
  exitWithFallback('FastAPI OpenAPI export failed');
}

let spec;
try {
  spec = JSON.parse(result.stdout.trim());
} catch (e) {
  exitWithFallback(`Failed to parse app.openapi() output: ${e.message}`);
}

writeFileSync(outputPath, JSON.stringify(spec, null, 2));
console.log(`[gen-mlapi] ML Service spec written to ${outputPath}`);
