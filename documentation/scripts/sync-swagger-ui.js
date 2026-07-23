const {copyFileSync, mkdirSync} = require('fs');
const {resolve} = require('path');

const docsRoot = resolve(__dirname, '..');
const swaggerDist = resolve(docsRoot, 'node_modules', 'swagger-ui-dist');
const swaggerStatic = resolve(docsRoot, 'static', 'swagger-ui');

const assets = [
  'swagger-ui.css',
  'swagger-ui-bundle.js',
  'swagger-ui-standalone-preset.js',
];

mkdirSync(swaggerStatic, {recursive: true});

for (const asset of assets) {
  copyFileSync(resolve(swaggerDist, asset), resolve(swaggerStatic, asset));
  console.log(`[sync-swagger-ui] Copied ${asset}`);
}
