const {copyFileSync, existsSync, writeFileSync} = require('fs');
const {spawnSync} = require('child_process');
const {resolve} = require('path');

const repoRoot = resolve(__dirname, '..', '..');
const staticApiDir = resolve(repoRoot, 'documentation', 'static', 'api');

const coreApiDir = resolve(repoRoot, 'DevHunt.CoreApi');
const coreApiProject = resolve(coreApiDir, 'DevHunt.CoreApi.csproj');
const coreApiAssembly = resolve(coreApiDir, 'bin', 'Release', 'net10.0', 'DevHunt.CoreApi.dll');
const swaggerOut = resolve(repoRoot, 'swagger.json');

const authServiceDir = resolve(repoRoot, 'DevHunt.AuthService');
const authServiceProject = resolve(authServiceDir, 'DevHunt.AuthService.csproj');
const authServiceAssembly = resolve(authServiceDir, 'bin', 'Release', 'net10.0', 'DevHunt.AuthService.dll');
const authSwaggerOut = resolve(repoRoot, 'auth-swagger.json');

function run(cmd, args, options) {
  return spawnSync(cmd, args, {stdio: 'inherit', ...options});
}

function fallbackPath(filename) {
  return resolve(staticApiDir, filename);
}

function allowFallback() {
  return process.env.ALLOW_DOCS_FALLBACK === 'true';
}

function exitWithFallback(label, outputPath, filename, stub) {
  if (!allowFallback()) {
    console.error(`[gen-swagger] ${label}. Refusing to publish fallback OpenAPI output.`);
    process.exit(1);
  }

  const fb = fallbackPath(filename);
  if (existsSync(outputPath)) {
    console.warn(`[gen-swagger] ${label}: continuing with existing output.`);
    return;
  }
  if (existsSync(fb)) {
    copyFileSync(fb, outputPath);
    console.warn(`[gen-swagger] ${label}: using static fallback ${fb}.`);
    return;
  }
  console.warn(`[gen-swagger] ${label}: no fallback found; writing stub spec.`);
  writeFileSync(outputPath, JSON.stringify(stub, null, 2));
}

const swaggerEnv = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development',
  DOTNET_ROLL_FORWARD: process.env.DOTNET_ROLL_FORWARD ?? 'Major',
  Documentation__OpenApiExport: 'true',
  ObjectStorage__InitializeBucketsOnStartup: 'false',
  Features__Redis__Enabled: 'false',
};

// ─── dotnet tool restore (once, shared by both swagger exports) ──────────────
const toolRestore = run('dotnet', ['tool', 'restore'], {cwd: repoRoot});
if (toolRestore.status !== 0) {
  exitWithFallback('dotnet tool restore failed', swaggerOut, 'swagger.json', {
    openapi: '3.0.1', info: {title: 'DevHunt Core API', version: 'v1'}, paths: {},
  });
  exitWithFallback('dotnet tool restore failed', authSwaggerOut, 'auth-swagger.json', {
    openapi: '3.0.1', info: {title: 'DevHunt Auth API', version: 'v1'}, paths: {},
  });
  process.exit(0);
}

// ─── Core API ────────────────────────────────────────────────────────────────
console.log('[gen-swagger] Building DevHunt.CoreApi...');
const coreRestore = run('dotnet', ['restore', coreApiProject], {cwd: repoRoot});
if (coreRestore.status !== 0) {
  exitWithFallback('CoreApi dotnet restore failed', swaggerOut, 'swagger.json', {
    openapi: '3.0.1', info: {title: 'DevHunt Core API', version: 'v1'}, paths: {},
  });
} else {
  const coreBuild = run('dotnet', ['build', coreApiProject, '-c', 'Release', '--no-restore'], {cwd: repoRoot});
  if (coreBuild.status !== 0) {
    exitWithFallback('CoreApi build failed', swaggerOut, 'swagger.json', {
      openapi: '3.0.1', info: {title: 'DevHunt Core API', version: 'v1'}, paths: {},
    });
  } else {
    const coreSwagger = run('dotnet', ['tool', 'run', 'swagger', 'tofile', '--output', swaggerOut, coreApiAssembly, 'v1'], {cwd: coreApiDir, env: swaggerEnv});
    if (coreSwagger.status !== 0) {
      exitWithFallback('CoreApi swagger tofile failed', swaggerOut, 'swagger.json', {
        openapi: '3.0.1', info: {title: 'DevHunt Core API', version: 'v1'}, paths: {},
      });
    } else {
      console.log(`[gen-swagger] CoreApi spec written to ${swaggerOut}`);
    }
  }
}

// ─── Auth Service ─────────────────────────────────────────────────────────────
console.log('[gen-swagger] Building DevHunt.AuthService...');
const authRestore = run('dotnet', ['restore', authServiceProject], {cwd: repoRoot});
if (authRestore.status !== 0) {
  exitWithFallback('AuthService dotnet restore failed', authSwaggerOut, 'auth-swagger.json', {
    openapi: '3.0.1', info: {title: 'DevHunt Auth API', version: 'v1'}, paths: {},
  });
} else {
  const authBuild = run('dotnet', ['build', authServiceProject, '-c', 'Release', '--no-restore'], {cwd: repoRoot});
  if (authBuild.status !== 0) {
    exitWithFallback('AuthService build failed', authSwaggerOut, 'auth-swagger.json', {
      openapi: '3.0.1', info: {title: 'DevHunt Auth API', version: 'v1'}, paths: {},
    });
  } else {
    const authSwagger = run('dotnet', ['tool', 'run', 'swagger', 'tofile', '--output', authSwaggerOut, authServiceAssembly, 'v1'], {cwd: authServiceDir, env: swaggerEnv});
    if (authSwagger.status !== 0) {
      exitWithFallback('AuthService swagger tofile failed', authSwaggerOut, 'auth-swagger.json', {
        openapi: '3.0.1', info: {title: 'DevHunt Auth API', version: 'v1'}, paths: {},
      });
    } else {
      console.log(`[gen-swagger] AuthService spec written to ${authSwaggerOut}`);
    }
  }
}
