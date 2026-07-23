const {existsSync, readdirSync, readFileSync, statSync, writeFileSync} = require('fs');
const {extname, resolve} = require('path');

const docsRoot = resolve(__dirname, '..');
const typedocRoot = resolve(docsRoot, 'docs', 'frontend', 'api');

function escapeMdxText(line) {
  let result = '';
  let inInlineCode = false;

  for (let i = 0; i < line.length; i++) {
    const char = line[i];

    if (char === '`') {
      let end = i;
      while (end < line.length && line[end] === '`') {
        end++;
      }
      result += line.slice(i, end);
      inInlineCode = !inInlineCode;
      i = end - 1;
      continue;
    }

    if (!inInlineCode && char === '{' && line[i - 1] !== '\\') {
      result += '\\{';
      continue;
    }

    if (!inInlineCode && char === '}' && line[i - 1] !== '\\') {
      result += '\\}';
      continue;
    }

    result += char;
  }

  return result;
}

function processMarkdown(raw) {
  const lines = raw.split('\n');
  let inFence = false;
  let changed = false;

  const processed = lines.map((line) => {
    if (/^\s*(```|~~~)/.test(line)) {
      inFence = !inFence;
      return line;
    }

    if (inFence) {
      return line;
    }

    const next = escapeMdxText(line);
    if (next !== line) {
      changed = true;
    }
    return next;
  });

  return {changed, content: processed.join('\n')};
}

function walk(dir) {
  const entries = readdirSync(dir);
  let changedFiles = 0;

  for (const entry of entries) {
    const filePath = resolve(dir, entry);
    const stat = statSync(filePath);

    if (stat.isDirectory()) {
      changedFiles += walk(filePath);
      continue;
    }

    if (extname(entry) !== '.md') {
      continue;
    }

    const raw = readFileSync(filePath, 'utf8');
    const {changed, content} = processMarkdown(raw);
    if (changed) {
      writeFileSync(filePath, content, 'utf8');
      changedFiles++;
    }
  }

  return changedFiles;
}

if (!existsSync(typedocRoot)) {
  console.warn('[postprocess-typedoc] TypeDoc output directory not found:', typedocRoot);
  process.exit(1);
}

const changedFiles = walk(typedocRoot);
console.log(`[postprocess-typedoc] Escaped MDX braces in ${changedFiles} TypeDoc file(s).`);
