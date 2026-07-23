from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class SqlInjectionRule(RegexRule):
    rule_id = "PHP-S01"
    name = "SQL injection risk"
    description = "Variable interpolation in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"""(?:mysql_query|mysqli_query|->query)\s*\(\s*["'](?i:SELECT|INSERT|UPDATE|DELETE).*?\$"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use prepared statements: $stmt = $pdo->prepare('SELECT ... WHERE id = ?')."
    cwe_id = "CWE-89"


@register
class EvalUsageRule(RegexRule):
    rule_id = "PHP-S02"
    name = "eval() usage"
    description = "eval() executes arbitrary PHP code."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\beval\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Avoid eval(). Use proper language constructs instead."
    cwe_id = "CWE-95"


@register
class ShellExecRule(RegexRule):
    rule_id = "PHP-S03"
    name = "Shell execution"
    description = "Shell execution function with potential user input."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\b(?:exec|system|passthru|shell_exec|popen|proc_open)\s*\(|`[^`]*\$"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use escapeshellarg() and escapeshellcmd(). Avoid shell execution when possible."
    cwe_id = "CWE-78"


@register
class XssEchoRule(RegexRule):
    rule_id = "PHP-S04"
    name = "XSS via echo"
    description = "Echoing user input without htmlspecialchars creates XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"(?:echo|print)\s+.*\$_(?:GET|POST|REQUEST|COOKIE|SERVER)"
    exclude_pattern = r"^\s*(?://|#|\*)|htmlspecialchars|htmlentities"
    fix_suggestion = "Use htmlspecialchars($input, ENT_QUOTES, 'UTF-8') before output."
    cwe_id = "CWE-79"


@register
class DeprecatedMysqlRule(RegexRule):
    rule_id = "PHP-Q01"
    name = "Deprecated mysql_* functions"
    description = "mysql_* functions are deprecated and removed in PHP 7+."
    severity = Severity.HIGH
    category = Category.QUALITY
    languages = [Language.PHP]
    pattern = r"\bmysql_(?:connect|query|fetch|select_db|close|error|num_rows)\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use PDO or mysqli instead."


@register
class FileIncludeRule(RegexRule):
    rule_id = "PHP-S05"
    name = "File inclusion vulnerability"
    description = "Dynamic file inclusion with user input creates LFI/RFI risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"(?:include|require|include_once|require_once)\s*\(\s*\$"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use a whitelist of allowed files. Never include user-controlled paths."
    cwe_id = "CWE-98"


@register
class HardcodedCredentialsRule(RegexRule):
    rule_id = "PHP-S06"
    name = "Hardcoded credentials"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"""(?i)\$(?:password|secret|api_?key|token|db_pass)\s*=\s*['"][^'"\s]{8,}['"]"""
    exclude_pattern = r"^\s*(?://|#|\*)|\.example|test|mock"
    fix_suggestion = "Use environment variables: getenv('DB_PASSWORD')."
    cwe_id = "CWE-798"


@register
class InsecureHashRule(RegexRule):
    rule_id = "PHP-S07"
    name = "Insecure password hashing"
    description = "md5/sha1 should not be used for passwords."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\b(?:md5|sha1)\s*\(.*(?:password|pass|pwd)"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use password_hash() with PASSWORD_BCRYPT or PASSWORD_ARGON2ID."
    cwe_id = "CWE-328"


@register
class ErrorDisplayRule(RegexRule):
    rule_id = "PHP-Q02"
    name = "Error display enabled"
    description = "display_errors should be off in production to prevent info leakage."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"""(?:ini_set|display_errors)\s*\(\s*['"]display_errors['"].*['"](?:1|on|true)['"]|error_reporting\s*\(\s*E_ALL\s*\)"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Set display_errors = Off in production. Log errors to file instead."
    cwe_id = "CWE-209"


@register
class VarDumpRule(RegexRule):
    rule_id = "PHP-Q03"
    name = "var_dump/print_r in production"
    description = "Debug output functions should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PHP]
    pattern = r"\b(?:var_dump|print_r|var_export)\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Remove debug output or use a proper logger."


# ---------------------------------------------------------------------------
# Security rules PHP-S08 through PHP-S17
# ---------------------------------------------------------------------------

@register
class SsrfRule(RegexRule):
    rule_id = "PHP-S08"
    name = "SSRF via user-controlled URL"
    description = "file_get_contents/curl with a user-controlled URL enables Server-Side Request Forgery."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"(?:file_get_contents|curl_setopt|curl_init)\s*\(.*\$_(?:GET|POST|REQUEST|COOKIE)"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Validate and whitelist URLs before use. Check scheme (must be https), "
        "resolve hostname, and block private/loopback IP ranges."
    )
    cwe_id = "CWE-918"


@register
class XxeRule(RegexRule):
    rule_id = "PHP-S09"
    name = "XXE via XML parser"
    description = "XML parsing without disabling external entity loading enables XXE attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\b(?:xml_parser_create|simplexml_load_string|simplexml_load_file|DOMDocument|XMLReader)\b"
    exclude_pattern = r"^\s*(?://|#|\*)|LIBXML_NOENT\s*\|\s*LIBXML_DTDLOAD|libxml_disable_entity_loader\s*\(\s*true"
    fix_suggestion = (
        "Call libxml_disable_entity_loader(true) before parsing, or pass "
        "LIBXML_NOENT | LIBXML_DTDLOAD flags. Prefer a SAX parser for untrusted input."
    )
    cwe_id = "CWE-611"


@register
class InsecureDeserializeRule(RegexRule):
    rule_id = "PHP-S10"
    name = "Insecure deserialization"
    description = "unserialize() on user input allows remote code execution via object injection."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\bunserialize\s*\(.*\$_(?:GET|POST|REQUEST|COOKIE|SESSION)"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Use json_decode() instead of unserialize() for user-supplied data. "
        "If unserialize is unavoidable, pass an allowed_classes array as the second argument."
    )
    cwe_id = "CWE-502"


@register
class LdapInjectionRule(RegexRule):
    rule_id = "PHP-S11"
    name = "LDAP injection"
    description = "ldap_search with unescaped user input allows LDAP filter injection."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\bldap_(?:search|list|read)\s*\(.*\$_(?:GET|POST|REQUEST|COOKIE)"
    exclude_pattern = r"^\s*(?://|#|\*)|ldap_escape"
    fix_suggestion = (
        "Escape all user-supplied values with ldap_escape($value, '', LDAP_ESCAPE_FILTER) "
        "before embedding them in an LDAP filter."
    )
    cwe_id = "CWE-90"


@register
class OpenRedirectRule(RegexRule):
    rule_id = "PHP-S12"
    name = "Open redirect"
    description = "header(Location) with user-controlled input enables open redirect attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"""header\s*\(\s*['"]Location:\s*['"]\s*\.\s*\$"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Validate redirect targets against an explicit whitelist of allowed URLs or paths. "
        "Never forward raw user input directly to a Location header."
    )
    cwe_id = "CWE-601"


@register
class InsecureCookieRule(BaseRule):
    rule_id = "PHP-S13"
    name = "Insecure cookie flags"
    description = "setcookie() called without secure=true or httponly=true exposes the cookie to theft."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PHP]
    cwe_id = "CWE-614"

    # Matches setcookie( ... ) calls — we inspect the full call for missing flags
    _call_re = re.compile(r"\bsetcookie\s*\(([^;]*)\)", re.IGNORECASE)
    _comment_re = re.compile(r"^\s*(?://|#|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue
            match = self._call_re.search(line)
            if not match:
                continue
            args = match.group(1).lower()
            # Positional call: setcookie(name, value, expire, path, domain, secure, httponly)
            # Named-argument call (PHP 8): secure: true, httponly: true
            missing_secure = "secure" not in args and (args.count(",") < 5)
            missing_httponly = "httponly" not in args and (args.count(",") < 6)
            if missing_secure or missing_httponly:
                flags = []
                if missing_secure:
                    flags.append("secure=true")
                if missing_httponly:
                    flags.append("httponly=true")
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=match.start(),
                    message=f"setcookie() is missing flag(s): {', '.join(flags)}.",
                    suggestion=(
                        "Pass secure: true and httponly: true (PHP 8 named args) or "
                        "setcookie(name, value, ['secure'=>true,'httponly'=>true,'samesite'=>'Strict'])."
                    ),
                ))
        return issues


@register
class PathTraversalRule(RegexRule):
    rule_id = "PHP-S14"
    name = "Path traversal"
    description = "File operation with a user-controlled path allows directory traversal attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = (
        r"\b(?:fopen|file_get_contents|file_put_contents|readfile|unlink|rename|copy|mkdir|rmdir|scandir)\s*"
        r"\(.*\$_(?:GET|POST|REQUEST|COOKIE)"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|basename\s*\(|realpath\s*\("
    fix_suggestion = (
        "Use basename() to strip directory components and realpath() to resolve the canonical path. "
        "Verify the resolved path starts with the expected base directory before performing the operation."
    )
    cwe_id = "CWE-22"


@register
class CsrfMissingRule(BaseRule):
    rule_id = "PHP-S15"
    name = "CSRF token missing in form"
    description = "HTML form without a CSRF token field is vulnerable to cross-site request forgery."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    cwe_id = "CWE-352"

    _form_open_re = re.compile(r"<form\b[^>]*>", re.IGNORECASE)
    _comment_re = re.compile(r"^\s*(?://|#|\*)")
    # Matches common CSRF hidden field patterns
    _csrf_re = re.compile(
        r"csrf|_token|nonce",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        content = ctx.content
        # Find all <form> tag positions and the content until </form>
        for form_match in self._form_open_re.finditer(content):
            start = form_match.start()
            end = content.find("</form>", start)
            if end == -1:
                end = len(content)
            form_block = content[start:end]
            if not self._csrf_re.search(form_block):
                # Compute line number for the opening <form> tag
                line_no = content[:start].count("\n") + 1
                issues.append(self._make_issue(
                    ctx,
                    line=line_no,
                    column=form_match.start() - content.rfind("\n", 0, start) - 1,
                    message="<form> tag has no CSRF token field.",
                    suggestion=(
                        "Add a hidden CSRF token input: "
                        "<input type=\"hidden\" name=\"csrf_token\" value=\"<?= htmlspecialchars($csrfToken) ?>\">. "
                        "Validate it server-side on every state-changing request."
                    ),
                ))
        return issues


@register
class WeakRandomRule(RegexRule):
    rule_id = "PHP-S16"
    name = "Weak random number generator"
    description = "rand()/mt_rand() are not cryptographically secure and must not be used for security tokens."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    pattern = r"\b(?:rand|mt_rand)\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Use random_bytes() or random_int() for security-sensitive values such as tokens, "
        "nonces, and password-reset codes."
    )
    cwe_id = "CWE-338"


@register
class InsecureFileUploadRule(BaseRule):
    rule_id = "PHP-S17"
    name = "Insecure file upload"
    description = "move_uploaded_file() without extension/MIME validation allows upload of malicious files."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PHP]
    cwe_id = "CWE-434"

    _upload_re = re.compile(r"\bmove_uploaded_file\s*\(", re.IGNORECASE)
    _comment_re = re.compile(r"^\s*(?://|#|\*)")
    # Look for common validation hints in the surrounding context (±10 lines)
    _validation_re = re.compile(
        r"pathinfo|finfo_file|mime_content_type|getimagesize|allowedExt|allowed_ext|whitelist",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue
            if not self._upload_re.search(line):
                continue
            # Check ±10 lines for validation logic
            window_start = max(0, i - 11)
            window_end = min(len(ctx.lines), i + 10)
            context_block = "".join(ctx.lines[window_start:window_end])
            if not self._validation_re.search(context_block):
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=0,
                    message="move_uploaded_file() used without detectable extension or MIME-type validation.",
                    suggestion=(
                        "Validate file extension against an allowlist, verify MIME type with finfo_file(), "
                        "regenerate the filename server-side, and store uploads outside the web root."
                    ),
                ))
        return issues


# ---------------------------------------------------------------------------
# Quality rules PHP-Q04 through PHP-Q11
# ---------------------------------------------------------------------------

@register
class ShortPhpTagRule(RegexRule):
    rule_id = "PHP-Q04"
    name = "Deprecated short PHP open tag"
    description = "Short open tags (<?) are disabled by default in PHP 8 and cause portability issues."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.PHP]
    # Match <? that is NOT followed by php or xml (i.e. not <?php or <?xml)
    pattern = r"<\?(?!php|xml|\=)(?:\s|$)"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Replace <? with <?php to ensure compatibility across all PHP configurations."


@register
class ErrorSuppressionRule(RegexRule):
    rule_id = "PHP-Q05"
    name = "Error suppression operator"
    description = "The @ error-suppression operator hides runtime errors and makes debugging very difficult."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.PHP]
    pattern = r"(?<!['\"])\@(?![\w\s]*['\"])\$?\w"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Remove @. Handle errors explicitly with try/catch or check return values. "
        "Use set_error_handler() to log errors properly."
    )


@register
class EmptyCatchRule(BaseRule):
    rule_id = "PHP-Q06"
    name = "Empty catch block"
    description = "An empty catch block silently swallows exceptions, hiding bugs and error conditions."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.PHP]

    _catch_re = re.compile(r"\}\s*catch\s*\([^)]+\)\s*\{", re.IGNORECASE)
    _comment_re = re.compile(r"^\s*(?://|#|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        for i, line in enumerate(lines, start=1):
            if self._comment_re.search(line):
                continue
            if not self._catch_re.search(line):
                continue
            # Scan forward to find the matching closing brace
            depth = 0
            body_lines: list[str] = []
            for j in range(i - 1, min(i + 30, len(lines))):
                chunk = lines[j]
                depth += chunk.count("{") - chunk.count("}")
                if j > i - 1:
                    body_lines.append(chunk.strip())
                if depth <= 0 and j > i - 1:
                    break
            # Body is empty if every line is blank, a comment, or just braces
            non_empty = [
                ln for ln in body_lines
                if ln and not re.match(r"^(?://|#|\*|/\*|\*/|\}|\{)\s*$", ln)
            ]
            if not non_empty:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=0,
                    message="Empty catch block silently ignores exceptions.",
                    suggestion=(
                        "Log the exception or rethrow it. "
                        "At minimum: catch (Exception $e) { error_log($e->getMessage()); throw $e; }"
                    ),
                ))
        return issues


@register
class GlobalVariableRule(RegexRule):
    rule_id = "PHP-Q07"
    name = "Global variable usage"
    description = "Use of $GLOBALS or the global keyword creates hidden coupling and makes testing difficult."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PHP]
    pattern = r"\$GLOBALS\s*\[|\bglobal\s+\$"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Pass dependencies as function/constructor parameters or use a dependency injection container "
        "instead of relying on global state."
    )


@register
class TodoCommentRule(RegexRule):
    rule_id = "PHP-Q08"
    name = "TODO/FIXME comment"
    description = "TODO or FIXME comment indicates unfinished or known-broken code."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.PHP]
    pattern = r"(?://|#|/\*)\s*(?:TODO|FIXME|HACK|XXX)\b"
    exclude_pattern = None
    fix_suggestion = "Resolve the TODO/FIXME before merging, or create a tracked issue and reference it in the comment."


@register
class FunctionTooLongRule(BaseRule):
    rule_id = "PHP-Q09"
    name = "Function too long"
    description = "Functions longer than 50 lines are hard to understand, test, and maintain."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PHP]

    _func_re = re.compile(
        r"^\s*(?:public|protected|private|static|abstract|final|\s)*function\s+\w+\s*\(",
        re.IGNORECASE,
    )
    _comment_re = re.compile(r"^\s*(?://|#|\*)")
    MAX_LINES = 50

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        i = 0
        while i < len(lines):
            line = lines[i]
            if self._comment_re.search(line):
                i += 1
                continue
            if not self._func_re.search(line):
                i += 1
                continue
            func_start = i + 1  # 1-based
            # Find opening brace (may be on the same line or the next)
            brace_line = i
            while brace_line < min(i + 5, len(lines)) and "{" not in lines[brace_line]:
                brace_line += 1
            if brace_line >= len(lines):
                i += 1
                continue
            # Walk forward tracking brace depth to find the function end
            depth = 0
            func_end = brace_line
            for j in range(brace_line, len(lines)):
                depth += lines[j].count("{") - lines[j].count("}")
                if depth <= 0:
                    func_end = j + 1  # 1-based
                    break
            body_length = func_end - func_start
            if body_length > self.MAX_LINES:
                issues.append(self._make_issue(
                    ctx,
                    line=func_start,
                    column=0,
                    message=f"Function body is {body_length} lines (limit: {self.MAX_LINES}).",
                    suggestion=(
                        "Extract cohesive blocks into smaller, well-named private methods. "
                        "Aim for functions that do one thing."
                    ),
                ))
            i = func_end if func_end > i else i + 1
        return issues


@register
class MixedHtmlPhpRule(RegexRule):
    rule_id = "PHP-Q10"
    name = "Mixed HTML and PHP"
    description = "PHP logic embedded directly in HTML templates mixes concerns and reduces maintainability."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PHP]
    # Detects closing PHP tag ?> followed (on the same line or nearby) by raw HTML tags
    pattern = r"\?>\s*<(?:html|head|body|div|span|table|form|input|p|h[1-6])\b"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Separate business logic from presentation. Use a templating engine (Twig, Blade) "
        "or move HTML rendering into dedicated view files."
    )


@register
class MissingTypeHintsRule(BaseRule):
    rule_id = "PHP-Q11"
    name = "Missing type hints on function parameters"
    description = "Function parameters without type hints reduce static analysis coverage and code clarity."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.PHP]

    # Matches function signatures; captures the parameter list
    _func_re = re.compile(
        r"(?:public|protected|private|static|\s)+function\s+\w+\s*\(([^)]*)\)",
        re.IGNORECASE,
    )
    _comment_re = re.compile(r"^\s*(?://|#|\*)")
    # A typed parameter looks like: int $x, string $y, ?Foo $z, array $a = []
    _typed_param_re = re.compile(r"^\s*\??\w[\w\\|&]*\s+\$\w+")
    # Variadic with type: ...int $args
    _variadic_typed_re = re.compile(r"^\s*\??\w[\w\\|&]*\s+\.\.\.\$\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue
            match = self._func_re.search(line)
            if not match:
                continue
            param_str = match.group(1).strip()
            if not param_str:
                continue  # No parameters — nothing to check
            params = [p.strip() for p in param_str.split(",") if p.strip()]
            untyped = []
            for param in params:
                # Remove default value
                param_core = param.split("=")[0].strip()
                if param_core.startswith("..."):
                    # Variadic
                    if not self._variadic_typed_re.match(param_core):
                        untyped.append(param_core)
                elif not self._typed_param_re.match(param_core):
                    untyped.append(param_core)
            if untyped:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=match.start(),
                    message=f"Parameter(s) lack type hints: {', '.join(untyped)}.",
                    suggestion=(
                        "Add scalar (int, string, bool, float), class, interface, or union type hints "
                        "to all parameters. Use mixed if the type is genuinely unknown."
                    ),
                ))
        return issues


# ---------------------------------------------------------------------------
# Performance rules PHP-P01 through PHP-P04
# ---------------------------------------------------------------------------

@register
class CountInLoopRule(RegexRule):
    rule_id = "PHP-P01"
    name = "count() in loop condition"
    description = "Calling count() on every iteration recomputes the size of the array each time."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PHP]
    # for/while loop condition that contains count(
    pattern = r"\bfor\s*\([^;]*;[^;]*\bcount\s*\(|while\s*\(.*\bcount\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Cache the count before the loop: $len = count($arr); for ($i = 0; $i < $len; $i++). "
        "Or use foreach which does not call count() on each iteration."
    )


@register
class ConcatenationInLoopRule(RegexRule):
    rule_id = "PHP-P02"
    name = "String concatenation in loop"
    description = "Building a string with .= inside a loop has O(n²) complexity for large datasets."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PHP]
    pattern = r"\$\w+\s*\.=\s*"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Collect items in an array and call implode() after the loop, "
        "or use ob_start()/ob_get_clean() for HTML buffers."
    )


@register
class SelectStarRule(RegexRule):
    rule_id = "PHP-P03"
    name = "SELECT * usage"
    description = "SELECT * fetches all columns including unused ones, wasting memory and bandwidth."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PHP]
    pattern = r"""(?i)\bSELECT\s+\*\s+FROM\b"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = (
        "Enumerate only the columns your code actually uses: SELECT id, name, email FROM users."
    )


@register
class RegexInLoopRule(BaseRule):
    rule_id = "PHP-P04"
    name = "Regex compilation inside loop"
    description = "preg_match/preg_replace inside a loop recompiles the pattern on every iteration."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PHP]

    _loop_re = re.compile(r"\b(?:for|foreach|while)\b", re.IGNORECASE)
    _preg_re = re.compile(r"\bpreg_(?:match|replace|split|match_all)\s*\(", re.IGNORECASE)
    _comment_re = re.compile(r"^\s*(?://|#|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        i = 0
        while i < len(lines):
            line = lines[i]
            if self._comment_re.search(line) or not self._loop_re.search(line):
                i += 1
                continue
            # Found a loop — locate its body via brace matching
            brace_line = i
            while brace_line < min(i + 3, len(lines)) and "{" not in lines[brace_line]:
                brace_line += 1
            depth = 0
            loop_end = brace_line
            for j in range(brace_line, len(lines)):
                depth += lines[j].count("{") - lines[j].count("}")
                if depth <= 0:
                    loop_end = j
                    break
            # Scan the body for preg_* calls
            for k in range(i + 1, loop_end):
                body_line = lines[k]
                if self._comment_re.search(body_line):
                    continue
                m = self._preg_re.search(body_line)
                if m:
                    issues.append(self._make_issue(
                        ctx,
                        line=k + 1,
                        column=m.start(),
                        message="preg_match/preg_replace called inside a loop recompiles the regex each iteration.",
                        suggestion=(
                            "PHP's regex engine caches compiled patterns automatically via the JIT cache, "
                            "but hoisting the pattern to a constant (const PATTERN = '/.../') "
                            "makes the intent explicit and avoids accidental string concatenation in the pattern."
                        ),
                    ))
            i = loop_end + 1 if loop_end > i else i + 1
        return issues
