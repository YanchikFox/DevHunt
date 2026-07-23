from __future__ import annotations

import json
from pathlib import Path

from rich.console import Console
from rich.table import Table

from devhunt_analyzer.engine.models import AnalysisReport, Severity

SEVERITY_COLORS = {
    Severity.CRITICAL: "bold red",
    Severity.HIGH: "red",
    Severity.MEDIUM: "yellow",
    Severity.LOW: "cyan",
    Severity.INFO: "dim",
}

SEVERITY_ICONS = {
    Severity.CRITICAL: "!!!",
    Severity.HIGH: "!! ",
    Severity.MEDIUM: "!  ",
    Severity.LOW: ".  ",
    Severity.INFO: "   ",
}


def to_json(report: AnalysisReport, output_path: Path | None = None) -> str:
    data = json.dumps(report.to_dict(), indent=2, ensure_ascii=False)
    if output_path:
        output_path.write_text(data, encoding="utf-8")
    return data


def to_console(report: AnalysisReport) -> None:
    console = Console()

    console.print()
    console.print("[bold]DevHunt Analyzer Report[/bold]")
    console.print(f"  Target:   {report.target_path}")
    console.print(f"  Files:    {report.files_analyzed}")
    console.print(f"  Issues:   {report.total_issues}")
    console.print(f"  Duration: {report.duration_seconds}s")
    console.print()

    # Summary by severity
    for sev in [Severity.CRITICAL, Severity.HIGH, Severity.MEDIUM, Severity.LOW, Severity.INFO]:
        count = report.issues_by_severity.get(sev.value, 0)
        if count > 0:
            style = SEVERITY_COLORS[sev]
            console.print(f"  [{style}]{sev.value.upper():10s} {count}[/{style}]")

    if not report.issues:
        console.print("\n[green]No issues found![/green]")
        return

    console.print()

    # Issues table
    table = Table(show_lines=False, pad_edge=False, box=None)
    table.add_column("Sev", width=10, no_wrap=True)
    table.add_column("Rule", width=10, no_wrap=True)
    table.add_column("Location", width=55)
    table.add_column("Message", width=65)

    for issue in report.issues:
        style = SEVERITY_COLORS.get(issue.severity, "")
        icon = SEVERITY_ICONS.get(issue.severity, "")
        table.add_row(
            f"[{style}]{icon}{issue.severity.value.upper()}[/{style}]",
            issue.rule_id,
            f"{issue.file_path}:{issue.line}",
            issue.message,
        )

    console.print(table)
    console.print()
