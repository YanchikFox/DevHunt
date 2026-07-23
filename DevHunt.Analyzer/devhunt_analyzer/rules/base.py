from __future__ import annotations

import re
from abc import ABC, abstractmethod
from typing import ClassVar

from devhunt_analyzer.engine.models import (
    Category,
    FileContext,
    Issue,
    Language,
    Severity,
)


class BaseRule(ABC):
    """Abstract base for all analyzer rules."""

    rule_id: ClassVar[str]
    name: ClassVar[str]
    description: ClassVar[str]
    severity: ClassVar[Severity]
    category: ClassVar[Category]
    languages: ClassVar[list[Language]]
    cwe_id: ClassVar[str | None] = None
    needs_ast: ClassVar[bool] = False

    @abstractmethod
    def check(self, ctx: FileContext) -> list[Issue]:
        ...

    def _make_issue(
        self,
        ctx: FileContext,
        line: int,
        column: int = 0,
        end_line: int | None = None,
        message: str = "",
        suggestion: str = "",
    ) -> Issue:
        snippet = ctx.lines[line - 1].rstrip() if 0 < line <= len(ctx.lines) else None
        return Issue(
            rule_id=self.rule_id,
            rule_name=self.name,
            severity=self.severity,
            category=self.category,
            file_path=str(ctx.path),
            line=line,
            column=column,
            end_line=end_line,
            message=message or self.description,
            suggestion=suggestion,
            cwe_id=self.cwe_id,
            snippet=snippet,
        )


class RegexRule(BaseRule):
    """
    Convenience base for line-by-line regex scanning.
    Subclass defines pattern, message_template, fix_suggestion.
    """

    pattern: ClassVar[str | re.Pattern]
    message_template: ClassVar[str] = ""
    fix_suggestion: ClassVar[str] = ""
    exclude_pattern: ClassVar[str | re.Pattern | None] = None

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        compiled = re.compile(self.pattern) if isinstance(self.pattern, str) else self.pattern
        exclude = (
            re.compile(self.exclude_pattern)
            if self.exclude_pattern and isinstance(self.exclude_pattern, str)
            else self.exclude_pattern
        )

        for i, line in enumerate(ctx.lines, start=1):
            if exclude and exclude.search(line):
                continue
            match = compiled.search(line)
            if match:
                msg = self.message_template.format(match=match.group(0)) if self.message_template else self.description
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=match.start(),
                    message=msg,
                    suggestion=self.fix_suggestion,
                ))
        return issues
