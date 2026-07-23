"""
Semgrep integration — run community rules and convert output to DevHunt Issue format.

Uses Semgrep OSS with registry packs (no login required).
Configurable via SEMGREP_CONFIG env var (default: "p/default").
"""
from __future__ import annotations

import json
import os
import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path

from devhunt_analyzer.engine.models import Category, Issue, Severity

# Env-configurable: "p/default", "auto", "p/default,p/security-audit", etc.
SEMGREP_CONFIG = os.environ.get("SEMGREP_CONFIG", "p/default")

_CWE_RE = re.compile(r"(CWE-\d+)")

# Semgrep severity × confidence → DevHunt severity
_SEVERITY_MATRIX: dict[tuple[str, str], Severity] = {
    ("ERROR", "HIGH"): Severity.CRITICAL,
    ("ERROR", "MEDIUM"): Severity.HIGH,
    ("ERROR", "LOW"): Severity.MEDIUM,
    ("WARNING", "HIGH"): Severity.HIGH,
    ("WARNING", "MEDIUM"): Severity.MEDIUM,
    ("WARNING", "LOW"): Severity.LOW,
    ("INFO", "HIGH"): Severity.LOW,
    ("INFO", "MEDIUM"): Severity.INFO,
    ("INFO", "LOW"): Severity.INFO,
}

_CATEGORY_MAP: dict[str, Category] = {
    "security": Category.SECURITY,
    "performance": Category.PERFORMANCE,
    "correctness": Category.RELIABILITY,
    "best-practice": Category.QUALITY,
    "maintainability": Category.MAINTAINABILITY,
    "portability": Category.QUALITY,
}


@dataclass
class SemgrepResult:
    """Outcome of a Semgrep scan."""

    issues: list[Issue] = field(default_factory=list)
    available: bool = False  # True if Semgrep binary was found & ran
    scanned_files: int = 0
    error: str | None = None


def _extract_cwe(cwe_list: list[str]) -> str | None:
    """Extract first CWE-NNN from strings like 'CWE-79: Improper …'."""
    for item in cwe_list:
        m = _CWE_RE.search(item)
        if m:
            return m.group(1)
    return None


def _build_command(repo_path: Path, exclude_patterns: list[str] | None = None) -> list[str]:
    """Build the semgrep CLI command."""
    configs = [c.strip() for c in SEMGREP_CONFIG.split(",") if c.strip()]
    cmd = ["semgrep", "scan"]
    for cfg in configs:
        cmd.extend(["--config", cfg])
    for pattern in (exclude_patterns or []):
        cmd.extend(["--exclude", pattern])
    cmd.extend([
        "--json",
        "--metrics=off",
        "--timeout", "60",
        "--timeout-threshold", "3",
        "--max-target-bytes", "500000",
        "--quiet",
        str(repo_path),
    ])
    return cmd


def _parse_result(raw: dict, repo_path: Path) -> SemgrepResult:
    """Convert Semgrep JSON output to SemgrepResult."""
    scanned = raw.get("paths", {}).get("scanned", [])
    issues: list[Issue] = []

    for r in raw.get("results", []):
        extra = r.get("extra", {})
        metadata = extra.get("metadata", {})

        # Severity + confidence → DevHunt severity
        sev_str = extra.get("severity", "WARNING")
        confidence = metadata.get("confidence", "MEDIUM")
        severity = _SEVERITY_MATRIX.get(
            (sev_str, confidence),
            Severity.MEDIUM,
        )

        # Category
        cat_str = metadata.get("category", "").lower()
        category = _CATEGORY_MAP.get(cat_str, Category.QUALITY)

        # CWE
        cwe_id = _extract_cwe(metadata.get("cwe", []))

        # Rule identification
        check_id = r.get("check_id", "semgrep.unknown")
        rule_name = check_id.rsplit(".", 1)[-1] if "." in check_id else check_id

        # File path — make absolute for consistency with custom rules
        file_path = r.get("path", "")
        if not Path(file_path).is_absolute():
            file_path = str(repo_path / file_path)

        # Snippet
        lines_str = extra.get("lines", "").strip()
        snippet = lines_str[:200] if lines_str else None

        # Suggestion from fix or references
        suggestion = extra.get("fix", "") or ""
        if not suggestion:
            refs = metadata.get("references", [])
            if refs:
                suggestion = f"See: {refs[0]}"

        issues.append(Issue(
            rule_id=check_id,
            rule_name=rule_name,
            severity=severity,
            category=category,
            file_path=file_path,
            line=r.get("start", {}).get("line", 0),
            column=r.get("start", {}).get("col", 0),
            end_line=r.get("end", {}).get("line"),
            message=extra.get("message", ""),
            suggestion=suggestion,
            cwe_id=cwe_id,
            snippet=snippet,
        ))

    return SemgrepResult(
        issues=issues,
        available=True,
        scanned_files=len(scanned),
    )


def run_semgrep(repo_path: Path, timeout: int = 300, exclude_patterns: list[str] | None = None) -> SemgrepResult:
    """
    Run Semgrep with community rules on the given repository.

    Returns SemgrepResult with `available=False` if Semgrep is not installed
    (allows graceful fallback to custom rules).
    """
    cmd = _build_command(repo_path, exclude_patterns)

    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            cwd=str(repo_path),
        )
    except FileNotFoundError:
        return SemgrepResult(available=False, error="Semgrep binary not found")
    except subprocess.TimeoutExpired:
        return SemgrepResult(available=True, error="Semgrep scan timed out")

    # Semgrep returns exit code 0 for success, 1 for findings, >1 for errors
    if not proc.stdout:
        stderr = proc.stderr[:500] if proc.stderr else ""
        return SemgrepResult(available=True, error=f"No output from Semgrep. stderr: {stderr}")

    try:
        data = json.loads(proc.stdout)
    except json.JSONDecodeError:
        return SemgrepResult(available=True, error="Failed to parse Semgrep JSON output")

    result = _parse_result(data, repo_path)

    # Log errors from Semgrep (file parse errors, etc.) — don't fail the scan
    errors = data.get("errors", [])
    if errors:
        result.error = f"{len(errors)} files had parse errors"

    return result
