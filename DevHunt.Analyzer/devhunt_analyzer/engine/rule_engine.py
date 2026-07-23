from __future__ import annotations

import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

from devhunt_analyzer.engine.models import (
    AnalysisReport,
    Category,
    FileContext,
    Issue,
    Language,
    Severity,
)
from devhunt_analyzer.engine.file_collector import collect_files
from devhunt_analyzer.engine.rule_registry import get_all_rules
from devhunt_analyzer.engine.semgrep_runner import run_semgrep
from devhunt_analyzer.rules.base import BaseRule

# Lazy-init tree-sitter parsers
_parsers: dict[Language, object] = {}


def _get_parser(lang: Language):
    if lang in _parsers:
        return _parsers[lang]
    if lang == Language.ANY:
        return None
    try:
        import tree_sitter

        grammar_map = {
            Language.CSHARP: ("tree_sitter_c_sharp", "language"),
            Language.TYPESCRIPT: ("tree_sitter_typescript", "language_typescript"),
            Language.PYTHON: ("tree_sitter_python", "language"),
            Language.JAVA: ("tree_sitter_java", "language"),
            Language.GO: ("tree_sitter_go", "language"),
            Language.RUST: ("tree_sitter_rust", "language"),
            Language.PHP: ("tree_sitter_php", "language_php"),
            Language.RUBY: ("tree_sitter_ruby", "language"),
            Language.KOTLIN: ("tree_sitter_kotlin", "language"),
            Language.SWIFT: ("tree_sitter_swift", "language"),
            Language.C: ("tree_sitter_c", "language"),
            Language.CPP: ("tree_sitter_cpp", "language"),
        }

        if lang not in grammar_map:
            return None

        module_name, func_name = grammar_map[lang]
        import importlib
        mod = importlib.import_module(module_name)
        lang_func = getattr(mod, func_name)

        parser = tree_sitter.Parser()
        parser.language = tree_sitter.Language(lang_func())
        _parsers[lang] = parser
    except (ImportError, AttributeError, OSError):
        _parsers[lang] = None
        return None
    return _parsers.get(lang)


def analyze_file(
    path: Path,
    lang: Language,
    rules: list[BaseRule],
) -> list[Issue]:
    """Analyze a single file with applicable rules."""
    try:
        content = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    lines = content.splitlines()
    ctx = FileContext(path=path, content=content, lines=lines, language=lang)

    # Parse AST only if needed
    needs_ast = any(
        r.needs_ast for r in rules
        if lang in r.languages or Language.ANY in r.languages
    )
    if needs_ast:
        parser = _get_parser(lang)
        if parser:
            ctx.tree = parser.parse(content.encode("utf-8"))

    issues: list[Issue] = []
    for rule in rules:
        if lang not in rule.languages and Language.ANY not in rule.languages:
            continue
        try:
            found = rule.check(ctx)
            issues.extend(found)
        except Exception:
            pass

    return issues


# Custom rule prefixes that Semgrep replaces (security scanning)
_SEMGREP_REPLACES = frozenset({"SEC-"})


def _is_replaced_by_semgrep(rule_id: str) -> bool:
    return any(rule_id.startswith(prefix) for prefix in _SEMGREP_REPLACES)


def run_analysis(
    target: Path,
    workers: int = 4,
    language_filter: set[Language] | None = None,
    exclude_patterns: list[str] | None = None,
    max_file_size_kb: int = 500,
    disabled_rules: set[str] | None = None,
    use_semgrep: bool = True,
) -> AnalysisReport:
    """Run full analysis on a directory or single file."""
    start = time.monotonic()

    files = collect_files(
        target,
        languages=language_filter,
        exclude_patterns=exclude_patterns,
        max_file_size_kb=max_file_size_kb,
    )

    # --- Semgrep pass (community rules) ---
    semgrep_issues: list[Issue] = []
    semgrep_active = False
    semgrep_scanned = 0
    if use_semgrep:
        sg = run_semgrep(target, exclude_patterns=exclude_patterns)
        if sg.available:
            semgrep_active = True
            semgrep_issues = sg.issues
            semgrep_scanned = sg.scanned_files
            if sg.error:
                print(f"[rule-engine] Semgrep warning: {sg.error}")
        else:
            print(f"[rule-engine] Semgrep unavailable ({sg.error}), using custom rules only")

    # --- Custom rules pass ---
    all_rules = get_all_rules()
    if disabled_rules:
        all_rules = [r for r in all_rules if r.rule_id not in disabled_rules]
    # When Semgrep is active, disable custom rules it replaces (SEC-*)
    if semgrep_active:
        all_rules = [r for r in all_rules if not _is_replaced_by_semgrep(r.rule_id)]

    custom_issues: list[Issue] = []

    if len(files) > 50 and workers > 1:
        with ThreadPoolExecutor(max_workers=workers) as pool:
            futures = {
                pool.submit(analyze_file, path, lang, all_rules): path
                for path, lang in files
            }
            for future in as_completed(futures):
                try:
                    custom_issues.extend(future.result())
                except Exception:
                    pass
    else:
        for path, lang in files:
            custom_issues.extend(analyze_file(path, lang, all_rules))

    # Merge Semgrep + custom rule issues with deduplication.
    # Key = (file_path, line, category) — keep the issue with richer detail.
    seen: dict[tuple[str, int, str], Issue] = {}
    for issue in semgrep_issues + custom_issues:
        key = (issue.file_path, issue.line, issue.category.value)
        existing = seen.get(key)
        if existing is None:
            seen[key] = issue
        else:
            # Prefer the issue with more information (snippet, suggestion, cwe_id)
            def _richness(i: Issue) -> int:
                return (
                    (1 if i.snippet else 0)
                    + (1 if i.suggestion else 0)
                    + (1 if i.cwe_id else 0)
                    + (1 if i.end_line else 0)
                    + len(i.message)
                )
            if _richness(issue) > _richness(existing):
                seen[key] = issue
    issues = list(seen.values())

    elapsed = time.monotonic() - start

    report = AnalysisReport(
        target_path=str(target),
        files_analyzed=max(len(files), semgrep_scanned),
        total_issues=len(issues),
        issues=sorted(issues, key=lambda i: (i.severity, i.file_path, i.line), reverse=True),
        duration_seconds=elapsed,
    )

    for sev in Severity:
        report.issues_by_severity[sev.value] = sum(1 for i in issues if i.severity == sev)
    for cat in Category:
        report.issues_by_category[cat.value] = sum(1 for i in issues if i.category == cat)

    return report
