const {existsSync, readFileSync, writeFileSync} = require('fs');
const {resolve} = require('path');

const sidebarTs = resolve(__dirname, '..', 'docs', 'api', 'swagger', 'sidebar.ts');
const sidebarJs = resolve(__dirname, '..', 'docs', 'api', 'swagger', 'sidebar.js');

function main() {
  if (!existsSync(sidebarTs)) {
    console.warn('[postprocess-openapi] sidebar.ts not found, skipping conversion');
    return;
  }

  const source = readFileSync(sidebarTs, 'utf8');
  const withoutTypeImports = source.replace(/import type[^\\n]*\\n/, '');
  const withoutTypeAnnotations = withoutTypeImports.replace(/: \\s*SidebarsConfig/g, '');
  const cjsExport = withoutTypeAnnotations.replace(/export default sidebar;/, 'module.exports = sidebar;');

  writeFileSync(sidebarJs, cjsExport);
  console.log(`[postprocess-openapi] Wrote ${sidebarJs}`);
}

main();
