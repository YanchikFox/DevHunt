const {existsSync, mkdirSync, writeFileSync} = require('fs');
const {spawnSync} = require('child_process');
const {resolve} = require('path');

// __dirname is documentation/scripts, so:
// .. = documentation
// ../.. = repo root
const docsRoot = resolve(__dirname, '..');
const repoRoot = resolve(docsRoot, '..');
const docfxConfig = resolve(docsRoot, 'docfx', 'docfx.json');
// Intermediate output — postprocess-docfx.js strips prose and copies to static/docfx/
const docfxOutputDir = resolve(docsRoot, '.docfx-output');
const finalOutputIndex = resolve(docsRoot, 'static', 'docfx', 'index.html');

function run(cmd, args, options) {
  return spawnSync(cmd, args, {stdio: 'inherit', ...options});
}

function allowFallback() {
  return process.env.ALLOW_DOCS_FALLBACK === 'true';
}

function exitWithFallback(reason, code) {
  if (!allowFallback()) {
    console.error(`[gen-docfx] ${reason}. Refusing to publish placeholder DocFX output.`);
    process.exit(code);
  }

  if (existsSync(finalOutputIndex)) {
    console.warn(`[gen-docfx] ${reason}. Keeping existing DocFX output because ALLOW_DOCS_FALLBACK=true.`);
    process.exit(0);
  }
  console.warn(
    `[gen-docfx] ${reason}. Generating placeholder DocFX output because ALLOW_DOCS_FALLBACK=true.`,
  );
  mkdirSync(resolve(docsRoot, 'static', 'docfx'), {recursive: true});

  writeFileSync(
    finalOutputIndex,
    `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>DevHunt Backend API Reference</title>
    <style>
      * { margin: 0; padding: 0; box-sizing: border-box; }
      body {
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        line-height: 1.6; color: #333; background: #f5f5f5; padding: 2rem;
      }
      .container {
        max-width: 900px; margin: 0 auto; background: white;
        border-radius: 12px; padding: 3rem; box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      }
      h1 { color: #2e8555; margin-bottom: 1rem; font-size: 2rem; }
      .subtitle { color: #666; margin-bottom: 2rem; font-size: 1.1rem; }
      .info-box {
        background: #e8f5e9; border-left: 4px solid #2e8555;
        padding: 1.5rem; margin: 2rem 0; border-radius: 4px;
      }
      .info-box h2 { color: #2e8555; margin-bottom: 0.5rem; font-size: 1.3rem; }
      code {
        background: #f5f5f5; padding: 0.2rem 0.4rem; border-radius: 4px;
        font-family: 'Courier New', monospace; font-size: 0.9em; color: #c7254e;
      }
      .command {
        background: #2d2d2d; color: #f8f8f2; padding: 1rem; border-radius: 6px;
        margin: 1rem 0; overflow-x: auto;
      }
      .command code { background: transparent; color: #f8f8f2; padding: 0; }
      .links { margin-top: 2rem; padding-top: 2rem; border-top: 1px solid #e0e0e0; }
      .links a { color: #2e8555; text-decoration: none; margin-right: 1.5rem; font-weight: 500; }
      .links a:hover { text-decoration: underline; }
      ul { margin-left: 1.5rem; margin-top: 0.5rem; }
      li { margin-bottom: 0.5rem; }
    </style>
  </head>
  <body>
    <div class="container">
      <h1>DevHunt Backend API Reference</h1>
      <p class="subtitle">This site is generated from C# XML documentation comments via DocFX.</p>
      <div class="info-box">
        <h2>📦 Included Projects</h2>
        <ul>
          <li><strong>DevHunt.CoreApi</strong> - Main API controllers and services</li>
          <li><strong>DevHunt.AuthService</strong> - Authentication and authorization</li>
          <li><strong>DevHunt.Infrastructure</strong> - Data models and database context</li>
        </ul>
      </div>
      <div class="info-box">
        <h2>⚙️ DocFX Generation</h2>
        <p>DocFX output is not available in this build. This could happen if:</p>
        <ul>
          <li>DocFX generation was skipped (fast build mode)</li>
          <li>Network access to <code>api.nuget.org</code> was unavailable during build</li>
          <li>DocFX tool installation failed</li>
        </ul>
      </div>
      <div class="info-box">
        <h2>🔧 Regeneration</h2>
        <p>To generate the full API reference, run from the <code>documentation/</code> directory:</p>
        <div class="command"><code>npm run gen:docfx</code></div>
        <p>Or use the full documentation preparation:</p>
        <div class="command"><code>npm run prepare:docs</code></div>
      </div>
      <div class="links">
        <strong>Alternative Documentation:</strong><br/>
        <a href="/portal/docs/api/intro">Interactive OpenAPI Documentation →</a>
        <a href="/portal/docs/backend/intro">Backend Documentation →</a>
      </div>
    </div>
  </body>
</html>
`,
  );
  process.exit(0);
}

// Skip DocFX generation if SKIP_DOCFX env var is set (for faster builds)
if (process.env.SKIP_DOCFX === 'true') {
  console.log('[gen-docfx] Skipping DocFX generation (SKIP_DOCFX=true).');
  mkdirSync(resolve(docsRoot, 'static', 'docfx'), {recursive: true});
  exitWithFallback('DocFX generation skipped', 0);
}

// Build projects to generate XML documentation files needed by DocFX
console.log('[gen-docfx] Building projects to generate XML documentation...');
const slnxFile = resolve(repoRoot, 'DevHunt.slnx');
const slnFile = resolve(repoRoot, 'DevHunt.sln');

let buildResult;
if (existsSync(slnxFile)) {
  buildResult = run('dotnet', ['build', slnxFile, '--configuration', 'Release', '--no-incremental'], {cwd: repoRoot});
} else if (existsSync(slnFile)) {
  buildResult = run('dotnet', ['build', slnFile, '--configuration', 'Release', '--no-incremental'], {cwd: repoRoot});
} else {
  exitWithFallback('No solution file found at repo root', 1);
}

if (buildResult.status !== 0) {
  exitWithFallback('Project build failed', buildResult.status ?? 1);
}

// Restore local tools (includes docfx 2.77.0 from .config/dotnet-tools.json)
// Version is pinned — postprocess-docfx.js depends on DocFX modern template HTML structure.
// If upgrading docfx, verify postprocess-docfx.js still strips the right class names.
console.log('[gen-docfx] Restoring dotnet tools (pinned docfx 2.77.0)...');
const toolRestore = run('dotnet', ['tool', 'restore'], {cwd: repoRoot});
if (toolRestore.status !== 0) {
  exitWithFallback('dotnet tool restore failed', toolRestore.status ?? 1);
}

// Run DocFX — outputs to .docfx-output/ (intermediate, see docfx/docfx.json)
console.log('[gen-docfx] Running DocFX...');
const docfxDir = resolve(docsRoot, 'docfx');
const docfx = run('dotnet', ['tool', 'run', 'docfx', 'docfx.json'], {cwd: docfxDir});
if (docfx.status !== 0) {
  exitWithFallback('docfx build failed', docfx.status ?? 1);
}

if (!existsSync(docfxOutputDir)) {
  exitWithFallback('DocFX ran but .docfx-output/ was not created', 1);
}

// Strip XML doc-comment prose and copy to static/docfx/
console.log('[gen-docfx] Running postprocess-docfx.js to strip prose...');
const postprocess = run('node', [resolve(__dirname, 'postprocess-docfx.js')], {cwd: docsRoot});
if (postprocess.status !== 0) {
  exitWithFallback('postprocess-docfx.js failed', postprocess.status ?? 1);
}

if (!existsSync(finalOutputIndex)) {
  exitWithFallback('postprocess-docfx.js ran but static/docfx/index.html not found', 1);
}
