from __future__ import annotations

from pathlib import Path

import click

# Force rule registration
import devhunt_analyzer.rules  # noqa: F401

from devhunt_analyzer.engine.models import Language, Severity
from devhunt_analyzer.engine.report import to_console, to_json
from devhunt_analyzer.engine.rule_engine import run_analysis
from devhunt_analyzer.engine.rule_registry import list_rules


@click.group()
@click.version_option(package_name="devhunt-analyzer")
def main():
    """DevHunt Static Code Analyzer"""


@main.command()
@click.argument("path", type=click.Path(exists=True))
@click.option("--output", "-o", type=click.Path(), default=None, help="JSON output file path")
@click.option("--category", type=str, default=None, help="Filter by category (security, quality, ...)")
@click.option("--severity", type=str, default=None, help="Minimum severity (critical, high, medium, low, info)")
@click.option("--language", type=str, default=None, help="Filter: csharp or typescript")
@click.option("--workers", "-w", type=int, default=4, help="Parallel workers")
@click.option("--json", "json_output", is_flag=True, help="Output as JSON to stdout")
@click.option("--disable", multiple=True, help="Disable specific rule IDs (repeatable)")
def analyze(path, output, category, severity, language, workers, json_output, disable):
    """Analyze source code at PATH for issues."""
    lang_filter = None
    if language:
        try:
            lang_filter = {Language(language)}
        except ValueError:
            click.echo(f"Unknown language: {language}. Use 'csharp' or 'typescript'.", err=True)
            raise SystemExit(1)

    disabled = set(disable) if disable else None

    report = run_analysis(
        target=Path(path),
        workers=workers,
        language_filter=lang_filter,
        disabled_rules=disabled,
    )

    # Post-filter by category
    if category:
        report.issues = [i for i in report.issues if i.category.value == category.lower()]
        report.total_issues = len(report.issues)

    # Post-filter by minimum severity
    if severity:
        try:
            min_sev = Severity(severity.lower())
        except ValueError:
            click.echo(f"Unknown severity: {severity}.", err=True)
            raise SystemExit(1)
        report.issues = [i for i in report.issues if not (i.severity < min_sev)]
        report.total_issues = len(report.issues)

    if json_output or output:
        result = to_json(report, Path(output) if output else None)
        if json_output:
            import sys
            sys.stdout.buffer.write(result.encode("utf-8"))
            sys.stdout.buffer.write(b"\n")
    else:
        to_console(report)


@main.command("list-rules")
def list_rules_cmd():
    """List all available analysis rules."""
    rules = list_rules()
    for r in rules:
        sev = r["severity"].upper()
        langs = ", ".join(r["languages"])
        click.echo(f"[{sev:8s}] {r['rule_id']:8s} {r['name']} ({langs})")
        click.echo(f"           {r['description']}")
        if r["cwe_id"]:
            click.echo(f"           CWE: {r['cwe_id']}")
        click.echo()


if __name__ == "__main__":
    main()
