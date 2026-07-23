---
title: Memory coverage report — missing sources, uncovered files, hot files
type: playbook
status: verified
sources:
  - memory/INDEX.md
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## When to run

Once a month, or after a sprint that added/renamed source files.
Also useful before writing a new domain/system entry — shows which
files in the target area are already mentioned and which are blank spots.

Takes < 60 s on a full repo checkout.

## The script

Run from the repo root:

```python
python3 - <<'PYEOF'
import re, sys
from pathlib import Path
from collections import Counter

# ── 1. Collect every path mentioned in memory ───────────────────────────────

memory_root = Path("memory")
repo_root   = Path(".")

mentioned: Counter = Counter()   # path-string → mention count

for md_file in memory_root.rglob("*.md"):
    if ".obsidian" in str(md_file):
        continue
    text = md_file.read_text()

    # sources: frontmatter lines
    in_sources = False
    for line in text.splitlines():
        if re.match(r'^sources:', line):
            in_sources = True
            continue
        if in_sources:
            if line.startswith("  - "):
                src = line.strip().lstrip("- ").strip()
                mentioned[src] += 1
            elif line and not line.startswith(" "):
                in_sources = False

    # markdown links [text](path) — skip http and anchor-only
    for m in re.finditer(r'\[([^\]]*)\]\(([^)]+)\)', text):
        raw = m.group(2).split("#")[0].strip()
        if not raw or raw.startswith("http"):
            continue
        # resolve relative to the .md file's directory
        resolved = (md_file.parent / raw).resolve()
        try:
            rel = resolved.relative_to(repo_root.resolve())
            path_str = str(rel)
        except ValueError:
            continue
        # only count references that look like source code paths
        # (skip memory-internal links)
        if not path_str.startswith("memory/"):
            mentioned[path_str] += 1

# ── 2. Collect all source files in the repo ─────────────────────────────────

SOURCE_DIRS = [
    "DevHunt.AuthService",
    "DevHunt.CoreApi",
    "DevHunt.Infrastructure",
    "DevHunt.DatabaseMigrator",
    "DevHunt.Analyzer",
    "frontend/src",
    "ml-service",
    "integration-gateway",
    "notification-service",
]
SOURCE_EXTS = {".cs", ".ts", ".tsx", ".py", ".sql", ".js", ".mjs"}
EXCLUDE_DIRS = {"node_modules", ".next", "bin", "obj", "__pycache__", ".git", "migrations"}

all_sources: set[str] = set()
for src_dir in SOURCE_DIRS:
    p = repo_root / src_dir
    if not p.exists():
        continue
    for f in p.rglob("*"):
        if f.suffix not in SOURCE_EXTS:
            continue
        if any(excl in f.parts for excl in EXCLUDE_DIRS):
            continue
        all_sources.add(str(f.relative_to(repo_root)))

# ── 3. Compute the three lists ───────────────────────────────────────────────

mentioned_code = {p: c for p, c in mentioned.items()
                  if not p.startswith("memory/")}

# A: mentioned in memory but not on disk
broken = {p: c for p, c in mentioned_code.items()
          if not (repo_root / p).exists()}

# B: source files never mentioned anywhere in memory
uncovered = all_sources - set(mentioned_code.keys())

# C: mentioned more than 5 times
hot = {p: c for p, c in mentioned_code.items() if c > 5}

# ── 4. Output ────────────────────────────────────────────────────────────────

SEP = "─" * 70

print(f"\n{SEP}")
print(f"BROKEN REFERENCES  ({len(broken)} paths mentioned in memory but missing on disk)")
print(SEP)
if broken:
    for path, count in sorted(broken.items(), key=lambda x: -x[1]):
        print(f"  {count:3}×  {path}")
else:
    print("  (none)")

print(f"\n{SEP}")
print(f"TIER-2 COVERAGE GAP  ({len(uncovered)} source files never mentioned in memory)")
print(SEP)
# Group by top-level directory for readability
from collections import defaultdict
by_dir: dict[str, list[str]] = defaultdict(list)
for p in sorted(uncovered):
    top = p.split("/")[0]
    by_dir[top].append(p)
for top_dir in sorted(by_dir):
    files = by_dir[top_dir]
    print(f"\n  [{top_dir}]  {len(files)} uncovered")
    for f in files[:20]:          # cap at 20 per directory
        print(f"    {f}")
    if len(files) > 20:
        print(f"    … and {len(files) - 20} more")

print(f"\n{SEP}")
print(f"HOT FILES  ({len(hot)} files mentioned > 5 times in memory)")
print(SEP)
if hot:
    for path, count in sorted(hot.items(), key=lambda x: -x[1]):
        print(f"  {count:3}×  {path}")
else:
    print("  (none — no file mentioned more than 5 times)")

print(f"\n{SEP}")
print(f"SUMMARY")
print(SEP)
total_mentioned = len(mentioned_code)
total_sources   = len(all_sources)
coverage_pct    = 100 * (total_mentioned - len(broken)) / total_sources if total_sources else 0
print(f"  Source files in repo  : {total_sources}")
print(f"  Mentioned in memory   : {total_mentioned}  ({len(broken)} broken)")
print(f"  Coverage (approx)     : {coverage_pct:.1f}%")
print(f"  Uncovered             : {len(uncovered)}")
print(f"  Hot files (>5 refs)   : {len(hot)}")
print()
PYEOF
```

## Reading the output

### BROKEN REFERENCES

A source path appears in `sources:` frontmatter or a markdown link but
the file does not exist at that path on disk. Causes:

- File was renamed or moved since the memory entry was written.
- Typo in a path that was never verified.

**Action:** for each broken path, `git log --all --follow -- <path>` to
find the rename, then update the memory entry and mark it re-verified.
High mention count = higher priority.

### TIER-2 COVERAGE GAP

Source files that exist in the repo but are never mentioned anywhere in
memory. Not necessarily a problem — test files, generated code, and thin
controllers that delegate to a service may never need a memory entry.
Use this list as a prompt, not an obligation.

**When to act:** if an uncovered file belongs to a domain you are about
to modify, read it and decide whether it changes the domain entry. If it
reveals a new pattern or trap, write the entry. If it is genuinely thin
(a DTO, a config model), ignore it.

**Directories with many uncovered files** signal areas where the memory
is shallow — candidates for a future mapping pass.

### HOT FILES

Files mentioned more than 5 times across all memory entries. These are
the load-bearing reference points of the codebase — the files whose
content shapes the most domain and cross-cutting behaviour.

**Use this list to:**
- Prioritise re-verification when those files change (`git log` on each
  after a sprint to catch drift before it becomes stale-suspected).
- Identify files that probably deserve their own `systems/` or
  `cross-cutting/` entry if they don't already have one.

### SUMMARY line

`Coverage (approx)` counts only files reachable by path (broken refs
excluded). It is a rough lower bound — many source files are legitimately
not worth documenting individually. Track the trend month-over-month
rather than optimising the absolute number.

## Saving a snapshot

To compare runs over time, redirect to a dated file (not committed):

```bash
python3 coverage-report.py > /tmp/coverage-$(date +%Y-%m-%d).txt
diff /tmp/coverage-2026-04-01.txt /tmp/coverage-2026-05-01.txt
```

## Interaction with finalization-checks.md

`finalization-checks.md` Step 1 already catches broken sources in
frontmatter. This script is broader: it also catches broken paths in
markdown prose links, and it adds the coverage gap and hot-file views
that finalization-checks does not produce. The two playbooks complement
each other — run finalization-checks after every batch of memory writes,
run this one monthly.
