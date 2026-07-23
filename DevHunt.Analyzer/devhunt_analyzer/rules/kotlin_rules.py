from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class ForceUnwrapRule(RegexRule):
    rule_id = "KT-Q01"
    name = "Force unwrap (!!)"
    description = "!! operator throws NPE on null. Handle nullability safely."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]
    pattern = r"\w+!!\."
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use safe call (?.), let, or require/checkNotNull with message."


@register
class PrintlnInProductionRule(RegexRule):
    rule_id = "KT-Q02"
    name = "println in production"
    description = "println should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.KOTLIN]
    pattern = r"\bprintln\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use a logging framework (SLF4J, Timber)."

    def check(self, ctx):
        if "test" in ctx.path.name.lower() or "Test" in ctx.path.name:
            return []
        return super().check(ctx)


@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "KT-S01"
    name = "Hardcoded secret"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"""(?i)(?:password|secret|apiKey|api_key|token)\s*=\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = "Use environment variables or BuildConfig fields."
    cwe_id = "CWE-798"


@register
class SqlStringTemplateRule(RegexRule):
    rule_id = "KT-S02"
    name = "SQL string template"
    description = "String template in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"""(?i)(?:rawQuery|execSQL|query)\s*\(\s*["'](?:SELECT|INSERT|UPDATE|DELETE).*?\$"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized queries or Room DAO."
    cwe_id = "CWE-89"


@register
class GlobalScopeRule(RegexRule):
    rule_id = "KT-Q03"
    name = "GlobalScope usage"
    description = "GlobalScope.launch creates coroutines that outlive their scope."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]
    pattern = r"GlobalScope\.(launch|async)\s*[({]"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use structured concurrency: viewModelScope, lifecycleScope, or CoroutineScope."


@register
class VarMutableCollectionRule(RegexRule):
    rule_id = "KT-Q04"
    name = "var with mutable collection"
    description = "var + MutableList/Map is doubly mutable. Use val + mutable or var + immutable."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.KOTLIN]
    pattern = r"\bvar\s+\w+\s*(?::\s*Mutable(?:List|Map|Set)|=\s*mutable(?:ListOf|MapOf|SetOf))"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use 'val' with mutableListOf() or 'var' with listOf()."


@register
class InsecureCryptoRule(RegexRule):
    rule_id = "KT-S03"
    name = "Insecure cryptography"
    description = "MD5/SHA1/DES are cryptographically broken."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"""MessageDigest\.getInstance\s*\(\s*["'](?:MD5|SHA-?1)["']\)|Cipher\.getInstance\s*\(\s*["']DES"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use SHA-256+ for hashing and AES-GCM for encryption."
    cwe_id = "CWE-328"


@register
class RunCatchingSwallowRule(RegexRule):
    rule_id = "KT-Q05"
    name = "runCatching swallows errors"
    description = "runCatching{}.getOrNull() silently swallows all exceptions."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]
    pattern = r"runCatching\s*\{[^}]*\}\s*\.getOrNull\(\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use .onFailure { log(it) }.getOrNull() or handle errors explicitly."


# ---------------------------------------------------------------------------
# SECURITY
# ---------------------------------------------------------------------------


@register
class CommandInjectionRule(RegexRule):
    rule_id = "KT-S04"
    name = "Command injection via Runtime.exec"
    description = "Runtime.exec called with string concatenation allows command injection."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"Runtime\.getRuntime\(\)\s*\.exec\s*\([^)]*\+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Pass a String array to exec() and never concatenate user input into shell commands."
    cwe_id = "CWE-78"


@register
class PathTraversalRule(RegexRule):
    rule_id = "KT-S05"
    name = "Path traversal via user input"
    description = "File constructed from user-controlled input enables path traversal attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"\bFile\s*\([^)]*(?:request|param|input|query|body|user)[^)]*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Canonicalize the path and verify it starts with the expected base directory."
    cwe_id = "CWE-22"


@register
class InsecureHttpRule(RegexRule):
    rule_id = "KT-S06"
    name = "Insecure cleartext HTTP URL"
    description = "Hardcoded http:// URL transmits data without encryption."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"""["']http://(?!localhost|127\.0\.0\.1|10\.\d+\.\d+\.\d+|192\.168\.)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use https:// for all non-loopback URLs. Configure Network Security Config for debug builds."
    cwe_id = "CWE-319"


@register
class WebViewJavascriptInterfaceRule(RegexRule):
    rule_id = "KT-S07"
    name = "WebView addJavascriptInterface with untrusted content"
    description = "addJavascriptInterface exposes native methods to all JavaScript in the WebView."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"\.addJavascriptInterface\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Ensure WebView only loads trusted origins. "
        "Consider postMessage instead of addJavascriptInterface."
    )
    cwe_id = "CWE-749"


@register
class InsecureSharedPreferencesRule(RegexRule):
    rule_id = "KT-S08"
    name = "Insecure SharedPreferences secret storage"
    description = "Storing secrets in SharedPreferences exposes them to rooted devices and backups."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"""\.putString\s*\(\s*["'][^"']*(?:password|secret|token|api_?key|private_?key)[^"']*["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use Android Keystore or EncryptedSharedPreferences from Jetpack Security."
    cwe_id = "CWE-312"


@register
class DisabledSslVerificationRule(RegexRule):
    rule_id = "KT-S09"
    name = "Disabled SSL certificate/hostname verification"
    description = "TrustAllCerts or ALLOW_ALL HostnameVerifier disables TLS validation, enabling MitM attacks."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.KOTLIN]
    pattern = r"(?:TrustAllCerts|ALLOW_ALL_HOSTNAME_VERIFIER|allowAllHostnames|NullHostnameVerifier|override\s+fun\s+checkClientTrusted|override\s+fun\s+checkServerTrusted\b[^{]*\{\s*\})"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Never disable SSL verification in production. Use a properly configured TrustManager and HostnameVerifier."
    cwe_id = "CWE-295"


# ---------------------------------------------------------------------------
# QUALITY
# ---------------------------------------------------------------------------


@register
class TodoFixmeCommentRule(RegexRule):
    rule_id = "KT-Q06"
    name = "TODO/FIXME comment"
    description = "TODO or FIXME comment indicates unfinished work left in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.KOTLIN]
    pattern = r"//\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Resolve the issue or track it in the project issue tracker and remove the comment."


@register
class LateinitOveruseRule(RegexRule):
    rule_id = "KT-Q07"
    name = "lateinit var overuse"
    description = "lateinit var used without ::property.isInitialized guard risks UninitializedPropertyAccessException."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]
    pattern = r"\blateInit\s+var\b|\blateinit\s+var\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Guard access with ::property.isInitialized, or replace lateinit with a nullable val "
        "initialized lazily via by lazy {}."
    )


@register
class EmptyCatchBlockRule(BaseRule):
    rule_id = "KT-Q08"
    name = "Empty catch block"
    description = "Empty catch block silently swallows exceptions and hides failures."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]

    _catch_re = re.compile(r"\}\s*catch\s*\([^)]+\)\s*\{")
    _empty_body_re = re.compile(r"\}\s*catch\s*\([^)]+\)\s*\{\s*\}")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        content = ctx.content
        for m in self._empty_body_re.finditer(content):
            line_no = content[: m.start()].count("\n") + 1
            issues.append(self._make_issue(
                ctx,
                line=line_no,
                column=m.start() - content.rfind("\n", 0, m.start()) - 1,
                suggestion="Log the exception or re-throw it. Never silently ignore errors.",
            ))
        return issues


@register
class MutableDataClassRule(BaseRule):
    rule_id = "KT-Q09"
    name = "Mutable data class property"
    description = "data class with var properties is mutable, breaking equals/hashCode contracts when used in sets or as map keys."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.KOTLIN]

    _data_class_re = re.compile(r"^\s*(?:(?:internal|public|private|protected)\s+)?data\s+class\s+\w+", re.MULTILINE)
    _var_param_re = re.compile(r"\bvar\s+\w+\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        for i, line in enumerate(lines, start=1):
            if self._data_class_re.search(line):
                # Scan constructor lines (until closing paren of primary constructor)
                depth = 0
                for j in range(i - 1, min(i + 30, len(lines))):
                    seg = lines[j]
                    if self._var_param_re.search(seg) and not seg.strip().startswith("//"):
                        issues.append(self._make_issue(
                            ctx,
                            line=j + 1,
                            column=0,
                            suggestion="Replace var with val in data class primary constructor to ensure immutability.",
                        ))
                    depth += seg.count("(") - seg.count(")")
                    if j > i - 1 and depth <= 0:
                        break
        return issues


@register
class FunctionTooLongRule(BaseRule):
    rule_id = "KT-Q10"
    name = "Function too long (>50 lines)"
    description = "Function body exceeds 50 lines, reducing readability and testability."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.KOTLIN]

    _fun_re = re.compile(r"^\s*(?:(?:private|internal|public|protected|override|suspend|inline|infix|operator)\s+)*fun\s+\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        i = 0
        while i < len(lines):
            if self._fun_re.search(lines[i]) and not lines[i].strip().startswith("//"):
                fun_start = i + 1
                depth = 0
                found_open = False
                for j in range(i, len(lines)):
                    depth += lines[j].count("{") - lines[j].count("}")
                    if "{" in lines[j]:
                        found_open = True
                    if found_open and depth <= 0:
                        fun_length = j - i + 1
                        if fun_length > 50:
                            issues.append(self._make_issue(
                                ctx,
                                line=fun_start,
                                column=0,
                                message=f"{self.description} (found {fun_length} lines)",
                                suggestion="Extract logical blocks into smaller, focused functions.",
                            ))
                        i = j
                        break
            i += 1
        return issues


@register
class NestedConditionDepthRule(BaseRule):
    rule_id = "KT-Q11"
    name = "Deeply nested when/if (>3 levels)"
    description = "Nesting depth exceeds 3 levels, making control flow hard to follow."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.KOTLIN]

    _nesting_re = re.compile(r"^\s*(?:if\s*\(|when\s*[({]|for\s*\(|while\s*\()")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        reported: set[int] = set()
        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            opens = line.count("{")
            closes = line.count("}")
            if self._nesting_re.search(line) and depth >= 3 and i not in reported:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=len(line) - len(line.lstrip()),
                    message=f"{self.description} (current depth {depth})",
                    suggestion="Extract nested blocks into separate functions or use guard clauses / early returns.",
                ))
                reported.add(i)
            depth += opens - closes
            if depth < 0:
                depth = 0
        return issues


@register
class PlatformTypeRule(RegexRule):
    rule_id = "KT-Q12"
    name = "Platform type (Java interop) without null check"
    description = "Java method return value assigned without explicit nullability annotation risks NPE via platform type."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.KOTLIN]
    pattern = r"\bval\s+\w+\s*=\s*\w+\.(?:get|find|load|fetch|read|create|build|make)\w*\s*\([^)]*\)\s*$"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Add explicit ?: or !! after the Java call, or annotate the Java source with @Nullable/@NonNull. "
        "Use ?: error(\"...\") to fail fast with a clear message."
    )


@register
class DeprecatedApiUsageRule(RegexRule):
    rule_id = "KT-Q13"
    name = "Deprecated API usage"
    description = "@Deprecated API is called without a suppression annotation, risking breakage on next library upgrade."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.KOTLIN]
    pattern = r"@Deprecated\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Migrate to the replacement API indicated in the @Deprecated message. "
        "If deferring, add @Suppress(\"DEPRECATION\") with a tracking comment."
    )


# ---------------------------------------------------------------------------
# PERFORMANCE
# ---------------------------------------------------------------------------


@register
class ListCreationInLoopRule(BaseRule):
    rule_id = "KT-P01"
    name = "List/collection created inside loop"
    description = "Creating a new collection object on every loop iteration causes excessive allocations."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.KOTLIN]

    _loop_re = re.compile(r"^\s*(?:for|while)\s*[\(\{]")
    _collection_re = re.compile(
        r"\b(?:mutableListOf|listOf|mutableMapOf|mapOf|mutableSetOf|setOf|ArrayList|HashMap|HashSet|LinkedList)\s*[<(]"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        loop_depth = 0
        brace_stack: list[int] = []

        for i, line in enumerate(lines, start=1):
            if line.strip().startswith("//"):
                continue
            if self._loop_re.search(line):
                loop_depth += 1
                brace_stack.append(line.count("{") - line.count("}"))
            else:
                if brace_stack:
                    brace_stack[-1] += line.count("{") - line.count("}")
                    if brace_stack[-1] <= 0:
                        brace_stack.pop()
                        loop_depth = max(0, loop_depth - 1)

            if loop_depth > 0 and self._collection_re.search(line):
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=len(line) - len(line.lstrip()),
                    suggestion="Hoist the collection creation outside the loop, or use a sequence/buildList.",
                ))
        return issues


@register
class StringTemplateInLoggingRule(RegexRule):
    rule_id = "KT-P02"
    name = "String template evaluated before log level check"
    description = (
        "String interpolation in logging calls is always evaluated, even when the log level is disabled, "
        "causing unnecessary allocations."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.KOTLIN]
    pattern = r"""(?:Log\.(?:d|v|i|w|e)|logger\.(?:debug|trace|info|warn|error))\s*\([^)]*\$(?:\{[^}]+\}|\w+)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Wrap with a level check (if (Log.isLoggable(TAG, Log.DEBUG))), "
        "use lazy lambda (logger.debug { \"...${expr}\" }), "
        "or Timber.d(\"format %s\", value) with %-style arguments."
    )


@register
class RegexInFunctionBodyRule(BaseRule):
    rule_id = "KT-P03"
    name = "Regex compiled inside function body"
    description = (
        "Regex() or toRegex() inside a function body recompiles the pattern on every call. "
        "Pattern compilation is expensive."
    )
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.KOTLIN]

    _fun_re = re.compile(
        r"^\s*(?:(?:private|internal|public|protected|override|suspend|inline|infix|operator)\s+)*fun\s+\w+"
    )
    _regex_re = re.compile(r"\bRegex\s*\(|\btoRegex\s*\(\)")
    _companion_re = re.compile(r"companion\s+object|val\s+\w+\s*=\s*Regex")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        inside_fun = False
        fun_depth = 0

        for i, line in enumerate(lines, start=1):
            if line.strip().startswith("//"):
                continue

            # Entering a function
            if self._fun_re.search(line):
                inside_fun = True
                fun_depth = 0

            if inside_fun:
                fun_depth += line.count("{") - line.count("}")

                if self._regex_re.search(line) and not self._companion_re.search(line):
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=len(line) - len(line.lstrip()),
                        suggestion=(
                            "Move the Regex to a companion object val or top-level val "
                            "so it is compiled once: "
                            "companion object { private val PATTERN = Regex(\"...\") }"
                        ),
                    ))

                if fun_depth <= 0 and "{" in "".join(lines[max(0, i - 5):i]):
                    inside_fun = False

        return issues
