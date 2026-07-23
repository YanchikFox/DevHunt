from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path


class Severity(Enum):
    CRITICAL = "critical"
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"
    INFO = "info"

    def __lt__(self, other: Severity) -> bool:
        order = [Severity.INFO, Severity.LOW, Severity.MEDIUM, Severity.HIGH, Severity.CRITICAL]
        return order.index(self) < order.index(other)

    def __le__(self, other: Severity) -> bool:
        return self == other or self.__lt__(other)


class Category(Enum):
    SECURITY = "security"
    PERFORMANCE = "performance"
    QUALITY = "quality"
    MAINTAINABILITY = "maintainability"
    RELIABILITY = "reliability"


class Language(Enum):
    CSHARP = "csharp"
    TYPESCRIPT = "typescript"
    PYTHON = "python"
    JAVA = "java"
    GO = "go"
    RUST = "rust"
    PHP = "php"
    RUBY = "ruby"
    KOTLIN = "kotlin"
    SWIFT = "swift"
    C = "c"
    CPP = "cpp"
    DOCKERFILE = "dockerfile"
    YAML = "yaml"
    JSON = "json"
    SQL = "sql"
    SHELL = "shell"
    ANY = "any"


@dataclass(frozen=True)
class Issue:
    rule_id: str
    rule_name: str
    severity: Severity
    category: Category
    file_path: str
    line: int
    column: int
    end_line: int | None
    message: str
    suggestion: str
    cwe_id: str | None = None
    snippet: str | None = None

    def to_dict(self) -> dict:
        return {
            "rule_id": self.rule_id,
            "rule_name": self.rule_name,
            "severity": self.severity.value,
            "category": self.category.value,
            "file_path": self.file_path,
            "line": self.line,
            "column": self.column,
            "end_line": self.end_line,
            "message": self.message,
            "suggestion": self.suggestion,
            "cwe_id": self.cwe_id,
            "snippet": self.snippet,
        }


@dataclass
class AnalysisReport:
    target_path: str
    files_analyzed: int
    total_issues: int
    issues_by_severity: dict[str, int] = field(default_factory=dict)
    issues_by_category: dict[str, int] = field(default_factory=dict)
    issues: list[Issue] = field(default_factory=list)
    duration_seconds: float = 0.0

    def to_dict(self) -> dict:
        return {
            "target_path": self.target_path,
            "files_analyzed": self.files_analyzed,
            "total_issues": self.total_issues,
            "issues_by_severity": self.issues_by_severity,
            "issues_by_category": self.issues_by_category,
            "duration_seconds": round(self.duration_seconds, 2),
            "issues": [i.to_dict() for i in self.issues],
        }


@dataclass
class FileContext:
    """Everything a rule needs to analyze a single file."""
    path: Path
    content: str
    lines: list[str]
    language: Language
    tree: object | None = None
