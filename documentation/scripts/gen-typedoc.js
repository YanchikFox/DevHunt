const {existsSync, mkdirSync, writeFileSync} = require('fs');
const {spawnSync} = require('child_process');
const {resolve} = require('path');

const repoRoot = resolve(__dirname, '..', '..');
const docsRoot = resolve(repoRoot, 'documentation');
const typedocRoot = resolve(docsRoot, 'docs', 'frontend', 'api');
const typedocReadme = resolve(typedocRoot, 'README.md');
const postprocessTypedoc = resolve(docsRoot, 'scripts', 'postprocess-typedoc.js');

function run(cmd, args, options) {
  return spawnSync(cmd, args, {stdio: 'inherit', ...options});
}

function allowFallback() {
  return process.env.ALLOW_DOCS_FALLBACK === 'true';
}

function ensurePlaceholder() {
  if (existsSync(typedocReadme)) {
    return;
  }
  mkdirSync(typedocRoot, {recursive: true});
  writeFileSync(
    typedocReadme,
    `---
title: Frontend API Reference
description: Placeholder for TypeDoc output.
---

# Frontend API Reference

TypeDoc output is not available in this build.

Rebuild without \`SKIP_TYPEDOC\` to generate the full API reference.
`,
  );
}

function exitWithFallback(reason, code) {
  if (!allowFallback()) {
    console.error(`[gen-typedoc] ${reason}. Refusing to publish placeholder TypeDoc output.`);
    process.exit(code);
  }

  console.warn(`[gen-typedoc] ${reason}. Creating placeholder because ALLOW_DOCS_FALLBACK=true.`);
  ensurePlaceholder();
  process.exit(0);
}

if (process.env.SKIP_TYPEDOC === 'true') {
  exitWithFallback('SKIP_TYPEDOC=true', 0);
}

const result = run('npx', ['typedoc', '--options', 'typedoc.json'], {cwd: docsRoot});

if (result.status !== 0) {
  exitWithFallback('TypeDoc failed', result.status ?? 1);
}

const postprocess = run('node', [postprocessTypedoc], {cwd: docsRoot});
if (postprocess.status !== 0) {
  console.warn('[gen-typedoc] TypeDoc postprocess failed.');
  process.exit(postprocess.status ?? 1);
}

process.exit(result.status ?? 0);
