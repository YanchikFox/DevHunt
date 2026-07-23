from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ---------------------------------------------------------------------------
# TS-F01: any type usage
# ---------------------------------------------------------------------------
@register
class AnyTypeRule(RegexRule):
    rule_id = "TS-F01"
    name = "any type usage"
    description = "Type 'any' disables type safety. Use 'unknown' with type guards or precise types."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r":\s*any\b|<any>|as\s+any\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace 'any' with 'unknown' and add type guard, or define a precise interface."

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip .d.ts files
        if ctx.path.name.endswith(".d.ts"):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-F05: Unsafe type casts
# ---------------------------------------------------------------------------
@register
class UnsafeCastRule(RegexRule):
    rule_id = "TS-F05"
    name = "Unsafe type cast"
    description = "Forced type cast bypasses type safety. Use type guards or Zod safeParse."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"as\s+unknown\s+as\b|as\s*\{[^}]*\}"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use a type guard function or Zod safeParse for runtime validation."


# ---------------------------------------------------------------------------
# TS-F06: console.log/debug/info in production code
# ---------------------------------------------------------------------------
@register
class ConsoleLogRule(RegexRule):
    rule_id = "TS-F06"
    name = "console.log in production"
    description = "console.log/debug/info should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"console\.(log|debug|info)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Remove or wrap in: if (process.env.NODE_ENV === 'development'). Use console.error/warn in catch blocks."

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip test files
        name = ctx.path.name.lower()
        if any(s in name for s in [".test.", ".spec.", "__test__"]):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-F07: refetchInterval on SignalR data
# ---------------------------------------------------------------------------
@register
class RefetchIntervalRule(RegexRule):
    rule_id = "TS-F07"
    name = "refetchInterval on real-time data"
    description = "refetchInterval should not be used for data updated via SignalR."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"refetchInterval\s*:\s*(?!false|0\b)\w+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use SignalR subscription + queryClient.invalidateQueries() instead."


# ---------------------------------------------------------------------------
# TS-F08: Inline arrow functions in JSX props
# ---------------------------------------------------------------------------
@register
class InlineArrowPropsRule(RegexRule):
    rule_id = "TS-F08"
    name = "Inline arrow in props"
    description = "Inline arrow function in JSX prop causes unnecessary re-renders."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"(on[A-Z]\w+)=\{\s*\(.*?\)\s*=>"
    exclude_pattern = r"^\s*//"
    message_template = "Inline arrow function in prop '{match}' causes re-renders."
    fix_suggestion = "Extract to a useCallback hook or named function."

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.path.suffix == ".tsx":
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-F09: JSX nesting depth > 4 levels
# ---------------------------------------------------------------------------
@register
class JsxNestingDepthRule(BaseRule):
    rule_id = "TS-F09"
    name = "Deep JSX nesting"
    description = "JSX nesting exceeds 4 levels. Extract into sub-components."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]

    _JSX_OPEN = re.compile(r"<([A-Z]\w+)(?:\s|>)")
    _JSX_CLOSE = re.compile(r"</([A-Z]\w+)>")
    _JSX_SELF_CLOSE = re.compile(r"<[A-Z]\w+[^>]*/\s*>")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.path.suffix == ".tsx":
            return []

        issues: list[Issue] = []
        depth = 0
        max_depth = 0
        max_depth_line = 0

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue

            opens = len(self._JSX_OPEN.findall(line))
            closes = len(self._JSX_CLOSE.findall(line))
            self_closes = len(self._JSX_SELF_CLOSE.findall(line))

            depth += opens - closes
            # Self-closing tags don't change depth

            if depth > max_depth:
                max_depth = depth
                max_depth_line = i

            if depth > 4 and opens > 0:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"JSX nesting depth is {depth} (max 4). Extract into a sub-component.",
                    suggestion="Create a separate component for the deeply nested JSX.",
                ))
                break  # Only report once per file

        return issues


# ---------------------------------------------------------------------------
# TS-F10: Inline styles
# ---------------------------------------------------------------------------
@register
class InlineStyleRule(RegexRule):
    rule_id = "TS-F10"
    name = "Inline style"
    description = "Inline styles detected. Use TailwindCSS classes instead."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"style=\{\{"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace inline styles with TailwindCSS utility classes."

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.path.suffix == ".tsx":
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-Q01: Hardcoded role strings
# ---------------------------------------------------------------------------
@register
class HardcodedRoleRule(RegexRule):
    rule_id = "TS-Q01"
    name = "Hardcoded role string"
    description = "Hardcoded role string. Use ROLES.* constants."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""["'](admin|curator|superadmin|moderator)["']"""
    exclude_pattern = r"^\s*//|^\s*\*|const\s+ROLE|type\s+|interface\s+"
    fix_suggestion = "Use ROLES.ADMIN, ROLES.CURATOR etc. from constants."


# ---------------------------------------------------------------------------
# TS-Q02: Large component file (>300 lines)
# ---------------------------------------------------------------------------
@register
class LargeComponentRule(BaseRule):
    rule_id = "TS-Q02"
    name = "Large component file"
    description = "Component file exceeds 300 lines. Consider splitting."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.path.suffix == ".tsx":
            return []
        if len(ctx.lines) > 300:
            return [self._make_issue(
                ctx,
                line=1,
                message=f"Component file is {len(ctx.lines)} lines (max 300). Consider splitting.",
                suggestion="Extract sub-components or hooks into separate files.",
            )]
        return []


# ---------------------------------------------------------------------------
# TS-Q03: Missing error boundary for pages
# ---------------------------------------------------------------------------
@register
class MissingErrorBoundaryRule(BaseRule):
    rule_id = "TS-Q03"
    name = "Missing error boundary"
    description = "Page component has no error boundary (error.tsx sibling)."
    severity = Severity.INFO
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "page.tsx":
            return []
        error_file = ctx.path.parent / "error.tsx"
        if not error_file.exists():
            return [self._make_issue(
                ctx,
                line=1,
                message="Page has no error.tsx boundary for error handling.",
                suggestion="Create an error.tsx file in the same directory.",
            )]
        return []


# ---------------------------------------------------------------------------
# TS-S01: Hardcoded secrets
# ---------------------------------------------------------------------------
@register
class TsHardcodedSecretsRule(RegexRule):
    rule_id = "TS-S01"
    name = "Hardcoded secret"
    description = "Possible hardcoded API key, token, or password in source code."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""(?i)(?:api.?key|secret|token|password|private.?key|client.?secret)\s*[:=]\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|\.env|process\.env|import\.meta\.env|\.example|\.sample|placeholder"
    fix_suggestion = "Move secrets to environment variables (NEXT_PUBLIC_* or server-only)."
    cwe_id = "CWE-798"

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if any(s in name for s in [".test.", ".spec.", ".example", ".sample", ".d.ts", "mock"]):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-S02: dangerouslySetInnerHTML (XSS)
# ---------------------------------------------------------------------------
@register
class DangerouslySetInnerHtmlRule(BaseRule):
    rule_id = "TS-S02"
    name = "dangerouslySetInnerHTML"
    description = "dangerouslySetInnerHTML without sanitization creates XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    cwe_id = "CWE-79"

    _DANGEROUS = re.compile(r"dangerouslySetInnerHTML")
    _SANITIZE = re.compile(r"DOMPurify|sanitize|purify", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._DANGEROUS.search(line):
                start = max(0, i - 5)
                end = min(len(ctx.lines), i + 6)
                window = "\n".join(ctx.lines[start:end])
                if not self._SANITIZE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="dangerouslySetInnerHTML without DOMPurify sanitization.",
                        suggestion="Sanitize HTML with DOMPurify.sanitize() before rendering.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# TS-S03: eval() usage
# ---------------------------------------------------------------------------
@register
class EvalUsageRule(RegexRule):
    rule_id = "TS-S03"
    name = "eval() usage"
    description = "eval() executes arbitrary code — severe security risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"\beval\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Remove eval(). Use JSON.parse() for data or a safe alternative."
    cwe_id = "CWE-95"


# ---------------------------------------------------------------------------
# TS-S04: Open redirect via window.location
# ---------------------------------------------------------------------------
@register
class TsOpenRedirectRule(RegexRule):
    rule_id = "TS-S04"
    name = "Open redirect risk"
    description = "window.location assignment with user input creates open redirect risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"(?:window\.location(?:\.href)?\s*=|location\.(?:replace|assign)\s*\()(?!.*(?:['\"]/|window\.location))"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Validate redirect URLs or use Next.js router.push() with relative paths only."
    cwe_id = "CWE-601"


# ---------------------------------------------------------------------------
# TS-S05: localStorage for sensitive data
# ---------------------------------------------------------------------------
@register
class LocalStorageSensitiveRule(RegexRule):
    rule_id = "TS-S05"
    name = "localStorage for sensitive data"
    description = "Storing tokens/passwords in localStorage is accessible to XSS attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""localStorage\.setItem\s*\(\s*["'](?:token|password|secret|auth|session|jwt|access_token|refresh_token)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use httpOnly cookies for tokens. localStorage is accessible to any JS on the page."
    cwe_id = "CWE-922"


# ---------------------------------------------------------------------------
# TS-S06: Insecure postMessage (no origin check)
# ---------------------------------------------------------------------------
@register
class InsecurePostMessageRule(BaseRule):
    rule_id = "TS-S06"
    name = "Insecure postMessage"
    description = "addEventListener('message') without origin validation."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    cwe_id = "CWE-346"

    _MSG_LISTENER = re.compile(r"""addEventListener\s*\(\s*["']message["']""")
    _ORIGIN_CHECK = re.compile(r"(?:event|e|ev|msg)\.origin")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._MSG_LISTENER.search(line):
                # Check next 15 lines for origin validation
                end = min(len(ctx.lines), i + 15)
                window = "\n".join(ctx.lines[i:end])
                if not self._ORIGIN_CHECK.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="postMessage listener without origin check.",
                        suggestion="Validate event.origin against expected origins before processing.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# TS-Q04: TODO/FIXME comments
# ---------------------------------------------------------------------------
@register
class TsTodoFixmeRule(RegexRule):
    rule_id = "TS-Q04"
    name = "TODO/FIXME comment"
    description = "Unresolved TODO/FIXME/HACK/TEMP comment found."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"//\s*(?:TODO|FIXME|HACK|TEMP|XXX|BUG)\b"
    fix_suggestion = "Resolve the TODO/FIXME or create a tracked issue for it."


# ---------------------------------------------------------------------------
# TS-Q05: Non-null assertion (!) overuse
# ---------------------------------------------------------------------------
@register
class NonNullAssertionRule(RegexRule):
    rule_id = "TS-Q05"
    name = "Non-null assertion"
    description = "Non-null assertion (!) bypasses null checks. Use optional chaining or type narrowing."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"\w+!\.|\w+!\["
    exclude_pattern = r"^\s*//|\.d\.ts"
    fix_suggestion = "Replace ! with optional chaining (?.) or add a proper null check."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name.endswith(".d.ts"):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# TS-Q06: Nested ternary operators
# ---------------------------------------------------------------------------
@register
class TsNestedTernaryRule(RegexRule):
    rule_id = "TS-Q06"
    name = "Nested ternary"
    description = "Nested ternary operator reduces readability."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"\?[^;:]*\?[^;]*:"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace nested ternary with if/else, switch, or a helper function."


# ---------------------------------------------------------------------------
# TS-Q07: useEffect without dependency array
# ---------------------------------------------------------------------------
@register
class UseEffectNoDepsRule(RegexRule):
    rule_id = "TS-Q07"
    name = "useEffect without deps"
    description = "useEffect without dependency array runs on every render."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"useEffect\s*\(\s*(?:\(\s*\)\s*=>|function)\s*\{[^}]*\}\s*\)\s*;?"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Add a dependency array: useEffect(() => { ... }, [deps])."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix not in (".tsx", ".ts"):
            return []
        issues: list[Issue] = []
        content = ctx.content
        # Find useEffect calls without a second argument (dependency array)
        pattern = re.compile(r"useEffect\s*\(")
        for match in pattern.finditer(content):
            pos = match.end()
            # Count parentheses to find the closing paren
            depth = 1
            while pos < len(content) and depth > 0:
                if content[pos] == "(":
                    depth += 1
                elif content[pos] == ")":
                    depth -= 1
                pos += 1
            # Get the full useEffect call content
            call = content[match.start():pos]
            # Check if there's a comma (separator between callback and deps)
            # Simple heuristic: count commas at depth=1 inside the call
            inner = call[call.index("(") + 1:-1]
            cb_depth = 0
            comma_count = 0
            for ch in inner:
                if ch in "({[":
                    cb_depth += 1
                elif ch in ")}]":
                    cb_depth -= 1
                elif ch == "," and cb_depth == 0:
                    comma_count += 1
            if comma_count == 0:
                line_num = content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="useEffect without dependency array — runs on every render.",
                    suggestion="Add dependency array: useEffect(() => { ... }, []).",
                ))
        return issues


# ---------------------------------------------------------------------------
# TS-Q08: useEffect with object/array in deps
# ---------------------------------------------------------------------------
@register
class UseEffectObjectDepsRule(RegexRule):
    rule_id = "TS-Q08"
    name = "Object/array in useEffect deps"
    description = "Object or array literal in useEffect deps triggers on every render (reference equality)."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"useEffect\s*\([^)]*,\s*\[[^\]]*(?:\{|\[)[^\]]*\]\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Memoize objects/arrays with useMemo or extract primitive values for deps."


# ---------------------------------------------------------------------------
# TS-Q09: Importing from parent index (potential circular dep)
# ---------------------------------------------------------------------------
@register
class ImportCycleRule(RegexRule):
    rule_id = "TS-Q09"
    name = "Potential import cycle"
    description = "Importing from parent index ('../') may cause circular dependencies."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""from\s+["']\.\./(?:index)?["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Import specific modules directly instead of from the parent index."


# ---------------------------------------------------------------------------
# TS-Q10: Empty catch block
# ---------------------------------------------------------------------------
@register
class TsEmptyCatchRule(BaseRule):
    rule_id = "TS-Q10"
    name = "Empty catch block"
    description = "Empty catch block silently swallows errors."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _CATCH = re.compile(r"catch\s*(?:\([^)]*\))?\s*\{")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._CATCH.finditer(ctx.content):
            brace_pos = ctx.content.index("{", match.start())
            depth = 1
            pos = brace_pos + 1
            while pos < len(ctx.content) and depth > 0:
                if ctx.content[pos] == "{":
                    depth += 1
                elif ctx.content[pos] == "}":
                    depth -= 1
                pos += 1
            body = ctx.content[brace_pos + 1: pos - 1].strip()
            if not body:
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="Empty catch block silently swallows errors.",
                    suggestion="Log the error with console.error or add a comment explaining why it's ignored.",
                ))
        return issues


# ---------------------------------------------------------------------------
# TS-Q11: Disabled ESLint rules
# ---------------------------------------------------------------------------
@register
class DisabledEslintRule(RegexRule):
    rule_id = "TS-Q11"
    name = "Disabled ESLint rule"
    description = "eslint-disable without justification comment."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"eslint-disable(?:-next-line|-line)?"
    exclude_pattern = r"--\s*\w+.*?reason:|justified|intentional"
    fix_suggestion = "Fix the ESLint issue or add a reason comment: // eslint-disable-next-line rule -- reason."


# ---------------------------------------------------------------------------
# TS-Q12: @ts-ignore usage
# ---------------------------------------------------------------------------
@register
class TsIgnoreRule(RegexRule):
    rule_id = "TS-Q12"
    name = "@ts-ignore usage"
    description = "@ts-ignore suppresses TypeScript errors. Use @ts-expect-error instead."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"@ts-ignore"
    exclude_pattern = None
    fix_suggestion = "Replace @ts-ignore with @ts-expect-error (will error when the issue is fixed)."


# ---------------------------------------------------------------------------
# TS-P01: useMemo/useCallback with empty deps (might as well be const)
# ---------------------------------------------------------------------------
@register
class UselessMemoRule(RegexRule):
    rule_id = "TS-P01"
    name = "Useless useMemo/useCallback"
    description = "useMemo/useCallback with empty deps and no external refs — use a const instead."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"(?:useMemo|useCallback)\s*\(\s*\(\s*\)\s*=>\s*(?:\{[^}]{0,50}\}|[^,]{0,50}),\s*\[\s*\]\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "If there are no dependencies, define as a const outside the component or above the hook."


# ---------------------------------------------------------------------------
# TS-P02: Expensive computation in render body
# ---------------------------------------------------------------------------
@register
class ExpensiveRenderComputationRule(RegexRule):
    rule_id = "TS-P02"
    name = "Expensive computation in render"
    description = "Array sort/filter/map chain in render body without useMemo."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"(?:\.filter\s*\([^)]+\)\s*\.(?:map|sort|reduce)|\.sort\s*\([^)]*\)\s*\.(?:map|filter))"
    exclude_pattern = r"^\s*//|useMemo"
    fix_suggestion = "Wrap expensive computations with useMemo(() => ..., [deps])."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        # Only flag inside component render body (heuristic: in return statement area)
        issues = super().check(ctx)
        filtered: list[Issue] = []
        for issue in issues:
            line_idx = issue.line - 1
            # Check if we're inside a component (look for function or const component)
            start = max(0, line_idx - 50)
            window = "\n".join(ctx.lines[start:line_idx])
            if "useMemo" not in window:
                filtered.append(issue)
        return filtered


# ---------------------------------------------------------------------------
# TS-P03: Large bundle import (entire library)
# ---------------------------------------------------------------------------
@register
class LargeBundleImportRule(RegexRule):
    rule_id = "TS-P03"
    name = "Large bundle import"
    description = "Importing entire library increases bundle size. Use specific imports."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"""import\s+\w+\s+from\s+["'](?:lodash|moment|rxjs|date-fns|@mui/material|@mui/icons-material)["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use specific imports: import debounce from 'lodash/debounce' or import { format } from 'date-fns'."


# ---------------------------------------------------------------------------
# TS-P04: Missing key in .map() render
# ---------------------------------------------------------------------------
@register
class MissingKeyInMapRule(BaseRule):
    rule_id = "TS-P04"
    name = "Missing key in map"
    description = "JSX element in .map() without key prop causes reconciliation issues."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]

    _MAP_JSX = re.compile(r"\.map\s*\(\s*(?:\([^)]*\)|[a-zA-Z_]\w*)\s*=>")
    _KEY_PROP = re.compile(r"\bkey\s*=")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []

        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._MAP_JSX.search(line):
                # Check next 5 lines for a key prop in JSX
                end = min(len(ctx.lines), i + 6)
                window = "\n".join(ctx.lines[i:end])
                if "<" in window and not self._KEY_PROP.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="JSX element in .map() without key prop.",
                        suggestion="Add a unique key prop: <Component key={item.id} />.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# TS-P05: Using array index as key
# ---------------------------------------------------------------------------
@register
class IndexAsKeyRule(RegexRule):
    rule_id = "TS-P05"
    name = "Index as key"
    description = "Using array index as key causes issues with dynamic lists."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"""key\s*=\s*\{\s*(?:index|idx|i)\s*\}"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use a unique identifier (e.g., item.id) instead of array index as key."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        return super().check(ctx)
