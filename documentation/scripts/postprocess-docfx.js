/**
 * postprocess-docfx.js
 *
 * Reads DocFX HTML output from ../.docfx-output/ and copies it to
 * ../static/docfx/.
 *
 * XML doc-comment prose (summary, remarks, param/returns/exception descriptions)
 * is now shown. The audit in commit 65192d1 verified that all doc-comments in
 * CoreApi controllers and services are accurate and no longer reference fictional
 * spec documents.
 *
 * If DocFX is upgraded, verify the modern template HTML structure still matches
 * what the portal expects.
 */

const {existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync, statSync} = require('fs');
const {resolve, relative, dirname, extname} = require('path');

const docsRoot = resolve(__dirname, '..');
const inputDir = resolve(docsRoot, '.docfx-output');
const outputDir = resolve(docsRoot, 'static', 'docfx');

function stripProse(html) {
  return {html, stripped: 0};
}

function copyRecursive(src, dest) {
  if (!existsSync(src)) return;
  mkdirSync(dest, {recursive: true});
  const entries = readdirSync(src);
  let totalStripped = 0;

  for (const entry of entries) {
    const srcPath = resolve(src, entry);
    const destPath = resolve(dest, entry);
    const stat = statSync(srcPath);

    if (stat.isDirectory()) {
      totalStripped += copyRecursive(srcPath, destPath);
    } else if (extname(entry) === '.html') {
      const raw = readFileSync(srcPath, 'utf8');
      const {html, stripped} = stripProse(raw);
      writeFileSync(destPath, html, 'utf8');
      totalStripped += stripped;
    } else {
      // Non-HTML files (CSS, JS, images) — copy as-is
      writeFileSync(destPath, readFileSync(srcPath));
    }
  }
  return totalStripped;
}

if (!existsSync(inputDir)) {
  console.warn('[postprocess-docfx] Input directory not found:', inputDir);
  console.warn('[postprocess-docfx] Run gen:docfx first to generate DocFX output.');
  process.exit(1);
}

console.log('[postprocess-docfx] Copying DocFX output (prose enabled)...');
const totalStripped = copyRecursive(inputDir, outputDir);
console.log(`[postprocess-docfx] Done → ${outputDir}`);
