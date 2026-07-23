from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ---------------------------------------------------------------------------
# CS-B01: Blocking async calls (.Result, .Wait())
# ---------------------------------------------------------------------------
@register
class BlockingAsyncCallRule(RegexRule):
    rule_id = "CS-B01"
    name = "Blocking async call"
    description = "Synchronous .Result or .Wait() blocks the thread. Use await instead."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]
    pattern = r"\.(Result|Wait\(\))\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace .Result with 'await' and .Wait() with 'await'."

    def check(self, ctx: FileContext) -> list[Issue]:
        issues = super().check(ctx)
        return [
            i for i in issues
            if "context.Result" not in (i.snippet or "")
            and "ActionResult" not in (i.snippet or "")
        ]


# ---------------------------------------------------------------------------
# CS-B02: Raw SQL / string concatenation in queries
# ---------------------------------------------------------------------------
@register
class RawSqlConcatRule(RegexRule):
    rule_id = "CS-B02"
    name = "Raw SQL concatenation"
    description = "String interpolation/concatenation in raw SQL creates SQL injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r'(FromSqlRaw|ExecuteSqlRaw|SqlQuery)\s*\(\s*(\$"|[^"]*\+)'
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized queries: FromSqlInterpolated() or pass parameters separately."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# CS-B04: Too many constructor dependencies (>7)
# ---------------------------------------------------------------------------
@register
class TooManyDependenciesRule(BaseRule):
    rule_id = "CS-B04"
    name = "Too many dependencies"
    description = "Constructor has more than 7 dependencies. Consider using a Facade."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]
    needs_ast = False  # regex-based for now

    _CTOR_PATTERN = re.compile(
        r"(?:public|internal|protected)\s+(\w+)\s*\(([^)]*)\)",
        re.DOTALL,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Find class name
        class_match = re.search(r"class\s+(\w+)", ctx.content)
        if not class_match:
            return issues

        class_name = class_match.group(1)

        for match in self._CTOR_PATTERN.finditer(ctx.content):
            method_name = match.group(1)
            if method_name != class_name:
                continue
            params_str = match.group(2).strip()
            if not params_str:
                continue
            params = [p.strip() for p in params_str.split(",") if p.strip()]
            if len(params) > 7:
                line_num = ctx.content[: match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx,
                    line=line_num,
                    message=f"Constructor has {len(params)} dependencies (max 7). Use Facade pattern.",
                    suggestion="Group related dependencies into a Facade or Mediator.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-B07: Fire-and-forget async (_ = ...Async)
# ---------------------------------------------------------------------------
@register
class FireAndForgetAsyncRule(RegexRule):
    rule_id = "CS-B07"
    name = "Fire-and-forget async"
    description = "Discarding async result bypasses error handling and outbox guarantees."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"_\s*=\s*\w.*?Async\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use 'await' to ensure the async operation completes and errors are handled."


# ---------------------------------------------------------------------------
# CS-B08: TOCTOU (AnyAsync -> FirstOrDefaultAsync)
# ---------------------------------------------------------------------------
@register
class TocTouRule(BaseRule):
    rule_id = "CS-B08"
    name = "TOCTOU pattern"
    description = "Check-then-fetch pattern creates race condition. Use single query + null check."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _CHECK = re.compile(r"\.(AnyAsync|ExistsAsync|CountAsync)\s*\(")
    _FETCH = re.compile(r"\.(FirstOrDefaultAsync|SingleOrDefaultAsync|FindAsync)\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._CHECK.search(line):
                window = ctx.lines[i + 1: i + 11]
                for j, next_line in enumerate(window):
                    if self._FETCH.search(next_line):
                        issues.append(self._make_issue(
                            ctx,
                            line=i + 1,
                            message="TOCTOU: AnyAsync/ExistsAsync followed by fetch. Use single query + null check.",
                            suggestion="Replace with single FirstOrDefaultAsync + null check.",
                        ))
                        break
        return issues


# ---------------------------------------------------------------------------
# CS-B09: String literal statuses in EF queries
# ---------------------------------------------------------------------------
@register
class StringLiteralStatusRule(RegexRule):
    rule_id = "CS-B09"
    name = "String literal status"
    description = "Hardcoded status string in query. Use status constant (e.g., ProjectStatus.Active.Value)."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]
    pattern = r"""[=!]=\s*["'](active|inactive|draft|published|pending|approved|rejected|left|banned|completed|archived|removed|suspended)["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use typed status constants (e.g., TeamMemberStatus.Active.Value)."


# ---------------------------------------------------------------------------
# CS-B10: Missing URL validation
# ---------------------------------------------------------------------------
@register
class MissingUrlValidationRule(BaseRule):
    rule_id = "CS-B10"
    name = "Missing URL validation"
    description = "User URL field assigned without SecurityHelpers.IsValidUrl() check."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-918"

    _URL_ASSIGN = re.compile(r"(\w+Url)\s*=\s*(?!null)")
    _VALIDATION = re.compile(r"IsValidUrl")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            match = self._URL_ASSIGN.search(line)
            if not match:
                continue
            # Check surrounding 10 lines for validation
            start = max(0, i - 5)
            end = min(len(ctx.lines), i + 6)
            context_window = "\n".join(ctx.lines[start:end])
            if not self._VALIDATION.search(context_window):
                issues.append(self._make_issue(
                    ctx,
                    line=i + 1,
                    message=f"'{match.group(1)}' assigned without SecurityHelpers.IsValidUrl() check.",
                    suggestion="Add SecurityHelpers.IsValidUrl() validation before assignment.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q01: Function too long (>200 lines)
# ---------------------------------------------------------------------------
@register
class FunctionTooLongRule(BaseRule):
    rule_id = "CS-Q01"
    name = "Function too long"
    description = "Method exceeds 200 lines. Extract smaller methods."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _METHOD = re.compile(
        r"(?:public|private|protected|internal|static|async|override|virtual|sealed|\s)+"
        r"(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)\s*\{",
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Simple brace-counting approach
        for match in self._METHOD.finditer(ctx.content):
            method_name = match.group(1)
            start_pos = match.end() - 1  # position of {
            depth = 0
            end_pos = start_pos

            for j in range(start_pos, len(ctx.content)):
                if ctx.content[j] == "{":
                    depth += 1
                elif ctx.content[j] == "}":
                    depth -= 1
                    if depth == 0:
                        end_pos = j
                        break

            start_line = ctx.content[:start_pos].count("\n") + 1
            end_line = ctx.content[:end_pos].count("\n") + 1
            length = end_line - start_line

            if length > 200:
                issues.append(self._make_issue(
                    ctx,
                    line=start_line,
                    end_line=end_line,
                    message=f"Method '{method_name}' is {length} lines long (max 200).",
                    suggestion="Extract logic into smaller private methods.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q02: Cyclomatic complexity too high (>15)
# ---------------------------------------------------------------------------
@register
class CyclomaticComplexityRule(BaseRule):
    rule_id = "CS-Q02"
    name = "High cyclomatic complexity"
    description = "Method cyclomatic complexity exceeds 15."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _METHOD = re.compile(
        r"(?:public|private|protected|internal|static|async|override|virtual|sealed|\s)+"
        r"(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)\s*\{",
    )
    _COMPLEXITY_TOKENS = re.compile(
        r"\b(if|else if|case|for|foreach|while|do|catch)\b|(\?\?)|(\?\.)|(&&)|(\|\|)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._METHOD.finditer(ctx.content):
            method_name = match.group(1)
            start_pos = match.end() - 1
            depth = 0
            end_pos = start_pos

            for j in range(start_pos, len(ctx.content)):
                if ctx.content[j] == "{":
                    depth += 1
                elif ctx.content[j] == "}":
                    depth -= 1
                    if depth == 0:
                        end_pos = j
                        break

            body = ctx.content[start_pos:end_pos]
            complexity = 1 + len(self._COMPLEXITY_TOKENS.findall(body))

            if complexity > 15:
                line_num = ctx.content[:start_pos].count("\n") + 1
                issues.append(self._make_issue(
                    ctx,
                    line=line_num,
                    message=f"Method '{method_name}' has cyclomatic complexity {complexity} (max 15).",
                    suggestion="Simplify logic or extract into smaller methods.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q03: Missing CancellationToken in public async methods
# ---------------------------------------------------------------------------
@register
class MissingCancellationTokenRule(RegexRule):
    rule_id = "CS-Q03"
    name = "Missing CancellationToken"
    description = "Public async method without CancellationToken parameter."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"public\s+(?:async\s+)?Task\s*(?:<[^>]*>)?\s+\w+\s*\([^)]*\)"
    exclude_pattern = r"CancellationToken"
    fix_suggestion = "Add 'CancellationToken ct = default' as the last parameter."

    def check(self, ctx: FileContext) -> list[Issue]:
        issues = super().check(ctx)
        # Exclude override methods (they inherit signature)
        return [i for i in issues if "override" not in (i.snippet or "")]


# ---------------------------------------------------------------------------
# CS-Q04: Empty catch block
# ---------------------------------------------------------------------------
@register
class EmptyCatchBlockRule(BaseRule):
    rule_id = "CS-Q04"
    name = "Empty catch block"
    description = "Empty catch block swallows exceptions silently."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _CATCH = re.compile(r"catch\s*(?:\([^)]*\))?\s*\{")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._CATCH.finditer(ctx.content):
            brace_pos = ctx.content.index("{", match.start())
            # Find closing brace
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
                    ctx,
                    line=line_num,
                    message="Empty catch block silently swallows exception.",
                    suggestion="Log the exception or add a comment explaining why it's intentionally ignored.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q05: Unused using statements
# ---------------------------------------------------------------------------
@register
class UnusedUsingRule(BaseRule):
    rule_id = "CS-Q05"
    name = "Potentially unused using"
    description = "Using directive may be unused."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _USING = re.compile(r"^using\s+([\w.]+)\s*;", re.MULTILINE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Skip files with global usings
        if "global using" in ctx.content:
            return issues

        for match in self._USING.finditer(ctx.content):
            namespace = match.group(1)
            last_segment = namespace.split(".")[-1]
            # Check if the last segment appears anywhere else in the file
            rest = ctx.content[match.end():]
            if last_segment not in rest:
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx,
                    line=line_num,
                    message=f"Using '{namespace}' may be unused.",
                    suggestion="Remove unused using directive.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-S01: Hardcoded secrets
# ---------------------------------------------------------------------------
@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "CS-S01"
    name = "Hardcoded secret"
    description = "Possible hardcoded password/secret/API key detected."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"""(?i)(password|secret|apikey|api_key|connectionstring|private.?key)\s*=\s*["'][^"']{8,}["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Move secrets to environment variables or a secrets manager."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# CS-S02: LDAP injection
# ---------------------------------------------------------------------------
@register
class LdapInjectionRule(RegexRule):
    rule_id = "CS-S02"
    name = "LDAP injection risk"
    description = "String concatenation in DirectorySearcher.Filter creates LDAP injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"(?:\.Filter\s*=\s*(?:\$\"|[^\"]*\+)|DirectorySearcher\s*\([^)]*(?:\$\"|[^\"]*\+))"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized LDAP filters or escape user input with LdapFilterEncoder."
    cwe_id = "CWE-90"


# ---------------------------------------------------------------------------
# CS-S03: XML external entity (XXE)
# ---------------------------------------------------------------------------
@register
class XxeRule(BaseRule):
    rule_id = "CS-S03"
    name = "XXE risk"
    description = "XmlDocument used without setting XmlResolver = null — XXE risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-611"

    _XML_DOC = re.compile(r"new\s+XmlDocument\s*\(")
    _SAFE = re.compile(r"XmlResolver\s*=\s*null")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._XML_DOC.search(line):
                start = i
                end = min(len(ctx.lines), i + 10)
                window = "\n".join(ctx.lines[start:end])
                if not self._SAFE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="XmlDocument created without XmlResolver = null — XXE risk.",
                        suggestion="Set XmlResolver = null immediately after construction, or use XDocument.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# CS-S04: Insecure deserialization (BinaryFormatter)
# ---------------------------------------------------------------------------
@register
class InsecureDeserializationRule(RegexRule):
    rule_id = "CS-S04"
    name = "Insecure deserialization"
    description = "BinaryFormatter/NetDataContractSerializer is insecure. Use System.Text.Json."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"(?:BinaryFormatter|NetDataContractSerializer|SoapFormatter|ObjectStateFormatter|LosFormatter)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace with System.Text.Json or a safe serializer. BinaryFormatter is deprecated."
    cwe_id = "CWE-502"


# ---------------------------------------------------------------------------
# CS-S05: Open redirect (Redirect with user-controlled URL)
# ---------------------------------------------------------------------------
@register
class CSharpOpenRedirectRule(BaseRule):
    rule_id = "CS-S05"
    name = "Open redirect risk"
    description = "Redirect with user-controlled URL without Url.IsLocalUrl() check."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-601"

    _REDIRECT = re.compile(r"(?:Redirect|RedirectToAction|RedirectPermanent)\s*\(")
    _LOCAL_CHECK = re.compile(r"Url\.IsLocalUrl|IsLocalUrl|LocalRedirect")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._REDIRECT.search(line):
                start = max(0, i - 10)
                window = "\n".join(ctx.lines[start:i + 1])
                if not self._LOCAL_CHECK.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Redirect without Url.IsLocalUrl() check — open redirect risk.",
                        suggestion="Validate URL with Url.IsLocalUrl() or use LocalRedirect().",
                    ))
        return issues


# ---------------------------------------------------------------------------
# CS-S06: Missing [Authorize] on sensitive endpoints
# ---------------------------------------------------------------------------
@register
class MissingAuthorizeAttributeRule(BaseRule):
    rule_id = "CS-S06"
    name = "Missing [Authorize] on sensitive endpoint"
    description = "[AllowAnonymous] on endpoints that modify data is a security risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-862"

    _ALLOW_ANON = re.compile(r"\[AllowAnonymous\]")
    _MUTATING = re.compile(r"\[(HttpPost|HttpPut|HttpDelete|HttpPatch)\]")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._ALLOW_ANON.search(line):
                # Check next 5 lines for a mutating HTTP attribute
                window = ctx.lines[i:min(len(ctx.lines), i + 6)]
                for j, next_line in enumerate(window):
                    if self._MUTATING.search(next_line):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="[AllowAnonymous] on a mutating endpoint — review authorization.",
                            suggestion="Remove [AllowAnonymous] or add explicit authorization logic inside.",
                        ))
                        break
        return issues


# ---------------------------------------------------------------------------
# CS-S07: CSRF protection missing
# ---------------------------------------------------------------------------
@register
class MissingAntiForgeryRule(BaseRule):
    rule_id = "CS-S07"
    name = "Missing CSRF protection"
    description = "POST/PUT/DELETE action without [ValidateAntiForgeryToken] (MVC only)."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-352"

    _MUTATING = re.compile(r"\[(HttpPost|HttpPut|HttpDelete)\]")
    _ANTI_FORGERY = re.compile(r"\[(?:ValidateAntiForgeryToken|AutoValidateAntiforgeryToken|IgnoreAntiforgeryToken)\]")
    _API_CONTROLLER = re.compile(r"\[ApiController\]")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip API controllers (they use token-based auth, not cookies)
        if self._API_CONTROLLER.search(ctx.content):
            return []

        issues: list[Issue] = []
        has_class_anti_forgery = bool(re.search(
            r"\[AutoValidateAntiforgeryToken\]\s*\n\s*(?:public\s+)?class", ctx.content
        ))
        if has_class_anti_forgery:
            return issues

        for i, line in enumerate(ctx.lines):
            if self._MUTATING.search(line):
                start = max(0, i - 5)
                window = "\n".join(ctx.lines[start:i + 1])
                if not self._ANTI_FORGERY.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Mutating MVC action without [ValidateAntiForgeryToken].",
                        suggestion="Add [ValidateAntiForgeryToken] or [AutoValidateAntiforgeryToken] on the class.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# CS-S08: Path traversal via Path.Combine
# ---------------------------------------------------------------------------
@register
class CSharpPathTraversalRule(RegexRule):
    rule_id = "CS-S08"
    name = "Path traversal risk"
    description = "Path.Combine with user input without sanitization — path traversal risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"Path\.Combine\s*\(.*?(?:dto\.|request\.|input\.|model\.|fileName|filePath)"
    exclude_pattern = r"^\s*//|GetFullPath|Path\.GetFileName"
    fix_suggestion = "Use Path.GetFileName() on user input and validate with GetFullPath() prefix check."
    cwe_id = "CWE-22"


# ---------------------------------------------------------------------------
# CS-S09: Insecure cookie (missing Secure/HttpOnly)
# ---------------------------------------------------------------------------
@register
class InsecureCookieRule(BaseRule):
    rule_id = "CS-S09"
    name = "Insecure cookie"
    description = "CookieOptions without Secure or HttpOnly flag."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-614"

    _COOKIE_OPTIONS = re.compile(r"new\s+CookieOptions\s*(?:\(\s*\))?\s*\{")
    _SECURE = re.compile(r"Secure\s*=\s*true")
    _HTTP_ONLY = re.compile(r"HttpOnly\s*=\s*true")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._COOKIE_OPTIONS.finditer(ctx.content):
            # Find the closing brace of the initializer
            start = match.end()
            depth = 1
            pos = start
            while pos < len(ctx.content) and depth > 0:
                if ctx.content[pos] == "{":
                    depth += 1
                elif ctx.content[pos] == "}":
                    depth -= 1
                pos += 1
            body = ctx.content[start:pos]
            line_num = ctx.content[:match.start()].count("\n") + 1
            if not self._SECURE.search(body):
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="CookieOptions missing Secure = true.",
                    suggestion="Set Secure = true to prevent cookie transmission over HTTP.",
                ))
            if not self._HTTP_ONLY.search(body):
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="CookieOptions missing HttpOnly = true.",
                    suggestion="Set HttpOnly = true to prevent JavaScript access to the cookie.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-S10: Regex denial of service (ReDoS)
# ---------------------------------------------------------------------------
@register
class RegexDosRule(BaseRule):
    rule_id = "CS-S10"
    name = "Regex DoS risk"
    description = "new Regex() without RegexOptions.NonBacktracking or timeout — ReDoS risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-1333"

    _NEW_REGEX = re.compile(r"new\s+Regex\s*\(")
    _SAFE = re.compile(r"RegexOptions\.(?:NonBacktracking|Compiled)|matchTimeout|TimeSpan")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._NEW_REGEX.search(line):
                # Check current and next 2 lines for safe options
                window = "\n".join(ctx.lines[i:min(len(ctx.lines), i + 3)])
                if not self._SAFE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="new Regex() without timeout or NonBacktracking — ReDoS risk.",
                        suggestion="Use RegexOptions.NonBacktracking or pass a matchTimeout parameter.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q06: Async void method
# ---------------------------------------------------------------------------
@register
class AsyncVoidRule(RegexRule):
    rule_id = "CS-Q06"
    name = "Async void method"
    description = "async void is fire-and-forget. Exceptions are unobservable. Use async Task."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"async\s+void\s+\w+"
    exclude_pattern = r"^\s*//|event\s+handler|EventHandler"
    fix_suggestion = "Change return type from 'void' to 'Task'. async void is only acceptable for event handlers."


# ---------------------------------------------------------------------------
# CS-Q07: ConfigureAwait(false) missing in library code
# ---------------------------------------------------------------------------
@register
class MissingConfigureAwaitRule(BaseRule):
    rule_id = "CS-Q07"
    name = "Missing ConfigureAwait in library"
    description = "Library code should use ConfigureAwait(false) to avoid deadlocks."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _AWAIT = re.compile(r"\bawait\s+\w")
    _CONFIGURE = re.compile(r"\.ConfigureAwait\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only apply to library projects (Infrastructure, not Controllers/Pages)
        path_str = str(ctx.path).lower()
        if "controller" in path_str or "page" in path_str or "program.cs" in path_str:
            return []
        if "infrastructure" not in path_str and "service" not in path_str:
            return []

        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._AWAIT.search(line) and not self._CONFIGURE.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="await without ConfigureAwait(false) in library code.",
                    suggestion="Add .ConfigureAwait(false) to prevent potential deadlocks.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q08: Magic numbers in logic
# ---------------------------------------------------------------------------
@register
class MagicNumberRule(BaseRule):
    rule_id = "CS-Q08"
    name = "Magic number"
    description = "Literal number in logic. Use a named constant for readability."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _MAGIC = re.compile(r"(?<![.\w])(\d{2,})\b")
    _SAFE_CONTEXTS = re.compile(
        r"^\s*(?:const|enum|case|return\s+\d|\.Take\(|\.Skip\(|"
        r"//|Sleep|Delay|Timeout|TimeSpan|\.Seconds|\.Minutes|"
        r"new\s+\w+\[\d|\.Length|\.Count|page|size|limit|offset)",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("///"):
                continue
            if self._SAFE_CONTEXTS.search(line):
                continue
            match = self._MAGIC.search(line)
            if match:
                num = int(match.group(1))
                # Skip common safe values: 0, 1, 10, 100, etc.
                if num in (0, 1, 2, 10, 100, 1000, 200, 201, 204, 400, 401, 403, 404, 409, 500):
                    continue
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message=f"Magic number {num}. Define as a named constant.",
                    suggestion="Extract to a const or readonly field with a descriptive name.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-Q09: TODO/FIXME comments
# ---------------------------------------------------------------------------
@register
class TodoFixmeRule(RegexRule):
    rule_id = "CS-Q09"
    name = "TODO/FIXME comment"
    description = "Unresolved TODO/FIXME/HACK/TEMP comment found."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]
    pattern = r"//\s*(?:TODO|FIXME|HACK|TEMP|XXX|BUG)\b"
    fix_suggestion = "Resolve the TODO/FIXME or create a tracked issue for it."


# ---------------------------------------------------------------------------
# CS-Q10: Nested ternary operators
# ---------------------------------------------------------------------------
@register
class NestedTernaryRule(RegexRule):
    rule_id = "CS-Q10"
    name = "Nested ternary"
    description = "Nested ternary operator reduces readability."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]
    pattern = r"\?[^;]*\?[^;]*:"
    exclude_pattern = r"^\s*//|^\s*\*|^\s*\"\"\""
    fix_suggestion = "Replace nested ternary with if/else or a helper method."


# ---------------------------------------------------------------------------
# CS-Q11: string.Equals without StringComparison
# ---------------------------------------------------------------------------
@register
class StringEqualsWithoutComparisonRule(RegexRule):
    rule_id = "CS-Q11"
    name = "string.Equals without comparison"
    description = "string.Equals() without StringComparison may cause culture-sensitive issues."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"\.Equals\s*\(\s*\"[^\"]*\"\s*\)"
    exclude_pattern = r"^\s*//|StringComparison"
    fix_suggestion = "Use string.Equals(value, StringComparison.OrdinalIgnoreCase) for safe comparisons."


# ---------------------------------------------------------------------------
# CS-Q12: Large class (>500 lines)
# ---------------------------------------------------------------------------
@register
class LargeClassRule(BaseRule):
    rule_id = "CS-Q12"
    name = "Large class"
    description = "Class exceeds 500 lines. Consider splitting."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    def check(self, ctx: FileContext) -> list[Issue]:
        if len(ctx.lines) > 500:
            return [self._make_issue(
                ctx, line=1,
                message=f"File is {len(ctx.lines)} lines (max 500). Consider splitting.",
                suggestion="Extract related logic into separate classes or partial classes.",
            )]
        return []


# ---------------------------------------------------------------------------
# CS-Q13: Return null collection
# ---------------------------------------------------------------------------
@register
class ReturnNullCollectionRule(RegexRule):
    rule_id = "CS-Q13"
    name = "Return null collection"
    description = "Returning null instead of empty collection causes NullReferenceException at caller."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"return\s+null\s*;"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Return an empty collection (Array.Empty<T>(), Enumerable.Empty<T>(), new List<T>())."

    _COLLECTION_RETURN = re.compile(
        r"(?:IEnumerable|IList|ICollection|List|IReadOnlyList|IReadOnlyCollection)\s*<"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues = super().check(ctx)
        # Only flag if the method returns a collection type
        filtered: list[Issue] = []
        for issue in issues:
            line_idx = issue.line - 1
            # Look backward for method signature to check return type
            start = max(0, line_idx - 30)
            window = "\n".join(ctx.lines[start:line_idx])
            if self._COLLECTION_RETURN.search(window):
                filtered.append(issue)
        return filtered


# ---------------------------------------------------------------------------
# CS-Q14: Multiple return statements (>5)
# ---------------------------------------------------------------------------
@register
class MultipleReturnsRule(BaseRule):
    rule_id = "CS-Q14"
    name = "Too many returns"
    description = "Method has more than 5 return statements. Simplify control flow."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _METHOD = re.compile(
        r"(?:public|private|protected|internal|static|async|override|virtual|sealed|\s)+"
        r"(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)\s*\{",
    )
    _RETURN = re.compile(r"\breturn\s")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._METHOD.finditer(ctx.content):
            method_name = match.group(1)
            start_pos = match.end() - 1
            depth = 0
            end_pos = start_pos
            for j in range(start_pos, len(ctx.content)):
                if ctx.content[j] == "{":
                    depth += 1
                elif ctx.content[j] == "}":
                    depth -= 1
                    if depth == 0:
                        end_pos = j
                        break
            body = ctx.content[start_pos:end_pos]
            returns = len(self._RETURN.findall(body))
            if returns > 5:
                line_num = ctx.content[:start_pos].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message=f"Method '{method_name}' has {returns} return statements (max 5).",
                    suggestion="Simplify control flow or extract logic into helper methods.",
                ))
        return issues


# ---------------------------------------------------------------------------
# CS-P01: LINQ in hot loop
# ---------------------------------------------------------------------------
@register
class LinqInLoopRule(BaseRule):
    rule_id = "CS-P01"
    name = "LINQ in loop"
    description = "LINQ method inside for/foreach loop. Consider refactoring for performance."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _LOOP = re.compile(r"\b(for|foreach|while)\s*\(")
    _LINQ = re.compile(
        r"\.(Where|Select|Any|All|First|FirstOrDefault|Single|SingleOrDefault|"
        r"Count|Sum|Min|Max|Average|OrderBy|OrderByDescending|GroupBy|ToList|ToArray|ToDictionary)\s*\("
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_depth = 0

        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._LOOP.search(line):
                in_loop = True
                loop_depth = 0
            if in_loop:
                loop_depth += line.count("{") - line.count("}")
                if loop_depth <= 0:
                    in_loop = False
                    continue
                if self._LINQ.search(line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="LINQ method inside loop body — potential N+1 or O(n²) performance.",
                        suggestion="Precompute LINQ results before the loop, or use a HashSet/Dictionary for lookups.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# CS-P02: String concatenation in loop
# ---------------------------------------------------------------------------
@register
class StringConcatInLoopRule(BaseRule):
    rule_id = "CS-P02"
    name = "String concat in loop"
    description = "String concatenation (+=) inside loop creates excessive allocations."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _LOOP = re.compile(r"\b(for|foreach|while)\s*\(")
    _CONCAT = re.compile(r"\w+\s*\+=\s*(?:\$?\"|'|\w)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_depth = 0

        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._LOOP.search(line):
                in_loop = True
                loop_depth = 0
            if in_loop:
                loop_depth += line.count("{") - line.count("}")
                if loop_depth <= 0:
                    in_loop = False
                    continue
                if self._CONCAT.search(line) and "string" in line.lower() or "+=" in line:
                    # Heuristic: check if it looks like string concat
                    if '\"' in line or "$\"" in line or "string" in "\n".join(ctx.lines[max(0, i - 5):i]).lower():
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="String concatenation inside loop — O(n²) allocations.",
                            suggestion="Use StringBuilder for string building in loops.",
                        ))
        return issues


# ---------------------------------------------------------------------------
# CS-P03: ToList() before Count/Any (unnecessary materialization)
# ---------------------------------------------------------------------------
@register
class UnnecessaryMaterializationRule(RegexRule):
    rule_id = "CS-P03"
    name = "Unnecessary ToList()"
    description = "ToList()/ToArray() before Count()/Any() materializes unnecessarily."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]
    pattern = r"\.(?:ToList|ToArray)\s*\(\s*\)\s*\.(?:Count|Any|First|FirstOrDefault|All)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Call Count()/Any() directly on the queryable/enumerable without materializing."


# ---------------------------------------------------------------------------
# CS-P04: Regex compiled per call
# ---------------------------------------------------------------------------
@register
class RegexPerCallRule(BaseRule):
    rule_id = "CS-P04"
    name = "Regex compiled per call"
    description = "Regex created inside method body. Use static/compiled Regex for performance."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _NEW_REGEX = re.compile(r"new\s+Regex\s*\(")
    _STATIC_FIELD = re.compile(r"(?:static|readonly)\s+.*Regex")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Check if there are static regex fields (meaning the dev knows the pattern)
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._NEW_REGEX.search(line):
                # Check if this line is inside a method (indented) vs a field declaration
                if self._STATIC_FIELD.search(line):
                    continue
                # Heuristic: if indentation > 8 spaces, likely inside a method
                leading = len(line) - len(line.lstrip())
                if leading >= 8:
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Regex created inside method — compiled on every call.",
                        suggestion="Move to a static readonly field or use GeneratedRegex (source generator).",
                    ))
        return issues
