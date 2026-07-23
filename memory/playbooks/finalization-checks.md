---
title: Memory finalization checks — link integrity, frontmatter, backlinks, coverage
type: playbook
status: verified
sources:
  - memory/INDEX.md
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## When to run

After any batch of memory writes that touch more than one file —
especially at the end of a bootstrap session or after a large
domain/cross-cutting update. Takes < 30 s to run.

## Step 1 — Link and source integrity (automated)

Run from the repo root:

```python
python3 - <<'PYEOF'
import os, re, sys
from pathlib import Path

memory_root = Path("memory")
repo_root = Path(".")
forbidden_source_patterns = ['README', '/docs/', '/documentation/', '/guides/']
required_fields = ['title', 'type', 'status', 'sources',
                   'verified_at', 'verified_against_commit', 'last_user_review']

mem_files = {p for p in memory_root.rglob("*.md") if ".obsidian" not in str(p)}
errors = []

for md_file in sorted(mem_files):
    text = md_file.read_text()
    lines = text.splitlines()

    # --- Markdown link targets ---
    for m in re.finditer(r'\[([^\]]+)\]\(([^)]+)\)', text):
        raw_target = m.group(2)
        target = raw_target.split('#')[0]
        if not target or target.startswith('http'):
            continue
        resolved = (md_file.parent / target).resolve()
        if not resolved.exists():
            repo_resolved = (repo_root / target).resolve()
            if not repo_resolved.exists():
                errors.append(f"BROKEN LINK  {md_file}: [{m.group(1)}]({raw_target})")

    # --- Frontmatter ---
    if not lines or lines[0].strip() != '---':
        if md_file.name != 'INDEX.md':
            errors.append(f"NO FRONTMATTER: {md_file}")
        continue
    fm_lines, end = [], -1
    for i, l in enumerate(lines[1:], 1):
        if l.strip() == '---':
            end = i; break
        fm_lines.append(l)
    if end < 0:
        errors.append(f"UNCLOSED FRONTMATTER: {md_file}"); continue
    fm_text = '\n'.join(fm_lines)
    if md_file.name != 'INDEX.md':
        for f in required_fields:
            if not re.search(rf'^{f}:', fm_text, re.MULTILINE):
                errors.append(f"MISSING FIELD '{f}': {md_file}")
        lur = re.search(r'^last_user_review:\s*(.+)$', fm_text, re.MULTILINE)
        if lur and lur.group(1).strip() not in ('null', ''):
            errors.append(f"last_user_review NOT NULL: {md_file} → {lur.group(1).strip()}")
    for line in fm_lines:
        if line.strip().startswith('- '):
            src = line.strip().lstrip('- ')
            for pat in forbidden_source_patterns:
                if pat in src:
                    errors.append(f"FORBIDDEN SOURCE {md_file}: {src}")
            # Verify sources exist in repo
            resolved_src = (repo_root / src).resolve()
            if not resolved_src.exists():
                errors.append(f"MISSING SOURCE FILE {md_file}: {src}")

print('\n'.join(errors) if errors else 'ALL OK')
PYEOF
```

**What to look for:**
- `BROKEN LINK` — fix the relative path or remove the link
- `NO FRONTMATTER` / `MISSING FIELD` — add the missing field
- `last_user_review NOT NULL` — reset to `null` (never set by Claude)
- `FORBIDDEN SOURCE` — remove; only code files are valid sources
- `MISSING SOURCE FILE` — file was renamed/deleted; update path or mark `stale-suspected`

`ALL OK` means the check passed.

## Step 2 — Scale-out backlink symmetry (manual, 2 commands)

```bash
# Should list the same set of gotchas in both outputs
echo "=== scale-out-readiness → gotchas ==="
grep -o "gotchas/[^)]*" memory/cross-cutting/scale-out-readiness.md | sort

echo "=== gotchas → scale-out-readiness ==="
grep -l "scale-out-readiness" memory/gotchas/*.md | xargs -I{} basename {} | sort
```

Both lists must be identical. If a new scale-out gotcha was added, it
must appear in both. Fix: add the missing backlink to whichever side
is shorter.

## Step 3 — INDEX.md completeness (eyeball)

```bash
# Count entries per section
for dir in systems domains cross-cutting data gotchas; do
    count=$(find memory/$dir -name "*.md" 2>/dev/null | wc -l)
    indexed=$(grep -c "\[$dir" memory/INDEX.md 2>/dev/null || echo 0)
    echo "$dir: $count files, $indexed in INDEX"
done
```

If `files > indexed` for any section, there is an unindexed entry —
add it to INDEX.md.

## Step 4 — Obsidian artifact cleanup

```bash
git status --short memory/ | grep -v ".obsidian"
```

Watch for:
- Files under `memory/` whose path mirrors a code path
  (e.g. `memory/DevHunt.Infrastructure/SomeClass.cs.md`) — these are
  Obsidian vault artifacts (empty notes auto-created by the graph view).
  Confirm empty, then `git rm`.
- `memory/Welcome.md` or similar Obsidian template files —
  also `git rm`.
- Legitimate untracked files (new memory entries not yet staged) —
  `git add` those.

## Step 5 — Stale-suspected entries

```bash
# For each verified entry, check if its sources changed since verified_against_commit
python3 - <<'PYEOF'
import subprocess, re
from pathlib import Path

for md_file in sorted(Path("memory").rglob("*.md")):
    if ".obsidian" in str(md_file) or md_file.name == "INDEX.md":
        continue
    text = md_file.read_text()
    commit_m = re.search(r'^verified_against_commit:\s*(\S+)', text, re.MULTILINE)
    if not commit_m:
        continue
    commit = commit_m.group(1)
    sources = re.findall(r'^\s+- (.+)$',
        (re.search(r'^sources:(.*?)^[a-z]', text, re.DOTALL | re.MULTILINE) or
         type('', (), {'group': lambda s, n: ''})()).group(1) if
        re.search(r'^sources:', text, re.MULTILINE) else '',
        re.MULTILINE)
    for src in sources:
        src = src.strip()
        result = subprocess.run(
            ['git', 'log', f'{commit}..HEAD', '--', src],
            capture_output=True, text=True)
        if result.stdout.strip():
            print(f"STALE-SUSPECTED {md_file}: {src} changed since {commit}")
PYEOF
```

Any output means the source file changed since the entry was verified.
Open the entry and either re-verify against current code or set
`status: stale-suspected` in the frontmatter.

## Expected outcome

All five steps clean = memory is consistent and ready for the next
session. Total time under normal conditions: < 2 minutes.
