from __future__ import annotations

from pathlib import Path

from devhunt_analyzer.engine.models import Language

SKIP_DIRS = {
    "node_modules", "bin", "obj", ".git", ".next", ".vs",
    "dist", "build", "coverage", "__pycache__", ".nuget",
    "packages", "TestResults", "wwwroot", ".claude",
    "Migrations", ".vercel", ".turbo", "vendor", "target",
    ".gradle", ".idea", ".vscode", "venv", ".venv", "env",
    ".mypy_cache", ".pytest_cache", ".tox", "site-packages",
    "Pods", ".build", ".swiftpm", "cmake-build",
}

LANGUAGE_MAP = {
    ".cs": Language.CSHARP,
    ".ts": Language.TYPESCRIPT,
    ".tsx": Language.TYPESCRIPT,
    ".py": Language.PYTHON,
    ".java": Language.JAVA,
    ".go": Language.GO,
    ".rs": Language.RUST,
    ".php": Language.PHP,
    ".rb": Language.RUBY,
    ".kt": Language.KOTLIN,
    ".kts": Language.KOTLIN,
    ".swift": Language.SWIFT,
    ".c": Language.C,
    ".h": Language.C,
    ".cpp": Language.CPP,
    ".cc": Language.CPP,
    ".cxx": Language.CPP,
    ".hpp": Language.CPP,
    ".yaml": Language.YAML,
    ".yml": Language.YAML,
    ".json": Language.JSON,
    ".sql": Language.SQL,
    ".sh": Language.SHELL,
    ".bash": Language.SHELL,
}


def collect_files(
    root: Path,
    languages: set[Language] | None = None,
    exclude_patterns: list[str] | None = None,
    max_file_size_kb: int = 500,
) -> list[tuple[Path, Language]]:
    """Walk directory tree, collect analyzable files."""
    results: list[tuple[Path, Language]] = []
    exclude_patterns = exclude_patterns or []

    for path in root.rglob("*"):
        if any(skip in path.parts for skip in SKIP_DIRS):
            continue

        if not path.is_file():
            continue

        lang = LANGUAGE_MAP.get(path.suffix.lower())
        if lang is None:
            # Handle extensionless files by name
            name_lower = path.name.lower()
            if name_lower in ("dockerfile", "dockerfile.dev", "dockerfile.prod"):
                lang = Language.DOCKERFILE
            elif name_lower in (".dockerignore",):
                continue
            else:
                continue

        if languages and lang not in languages:
            continue

        if any(path.match(pat) for pat in exclude_patterns):
            continue

        name_lower = path.name.lower()
        if ".generated." in name_lower or ".designer." in name_lower:
            continue

        try:
            if path.stat().st_size > max_file_size_kb * 1024:
                continue
        except OSError:
            continue

        results.append((path, lang))

    return results
