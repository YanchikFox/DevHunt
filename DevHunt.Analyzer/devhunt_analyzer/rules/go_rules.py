from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
# SECURITY (GO-S01 .. GO-S09)
# ===========================================================================

@register
class SqlStringConcatRule(RegexRule):
    rule_id = "GO-S01"
    name = "SQL string concatenation"
    description = "String formatting in SQL creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"""(?:fmt\.Sprintf|Sprintf)\s*\(\s*["'](?i:SELECT|INSERT|UPDATE|DELETE)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized queries: db.Query('SELECT ... WHERE id=$1', id)."
    cwe_id = "CWE-89"


@register
class CommandInjectionRule(RegexRule):
    rule_id = "GO-S02"
    name = "Command injection risk"
    description = "exec.Command with user input creates command injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"exec\.Command\s*\(.*(?:request|req\.|params|query|input|args|r\.|\+)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Pass arguments separately with exec.Command(cmd, arg1, arg2)."
    cwe_id = "CWE-78"


@register
class InsecureTLSRule(RegexRule):
    rule_id = "GO-S03"
    name = "Insecure TLS config"
    description = "InsecureSkipVerify disables TLS certificate verification."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"InsecureSkipVerify\s*:\s*true"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Remove InsecureSkipVerify or set to false."
    cwe_id = "CWE-295"


@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "GO-S04"
    name = "Hardcoded secret"
    description = "Possible hardcoded password, secret or API key."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"""(?i)(?:password|secret|apiKey|api_key|token|privateKey)\s*[:=]\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = "Use environment variables: os.Getenv('SECRET_KEY')."
    cwe_id = "CWE-798"


@register
class MathRandForCryptoRule(RegexRule):
    rule_id = "GO-S05"
    name = "math/rand for crypto"
    description = "math/rand is not cryptographically secure."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r'"math/rand"'
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use crypto/rand for security-sensitive randomness."
    cwe_id = "CWE-330"


@register
class PathTraversalRule(RegexRule):
    rule_id = "GO-S06"
    name = "Path traversal"
    description = "File operation with concatenated path creates traversal risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"(?:os\.Open|os\.ReadFile|ioutil\.ReadFile)\s*\(.*\+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use filepath.Clean() and validate path prefix."
    cwe_id = "CWE-22"


@register
class FilePermissionsRule(RegexRule):
    rule_id = "GO-S07"
    name = "File permissions too open"
    description = "World-readable/writable file permissions."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"os\.(?:OpenFile|WriteFile|MkdirAll)\s*\(.*0o?7[67][67]"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use restrictive permissions: 0o600 for files, 0o700 for directories."
    cwe_id = "CWE-732"


@register
class WeakHashRule(RegexRule):
    rule_id = "GO-S08"
    name = "Weak hash (md5/sha1)"
    description = "MD5/SHA1 are broken for security purposes."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"(?:md5|sha1)\.(?:New|Sum)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use sha256 or sha512 for integrity; bcrypt/argon2 for passwords."
    cwe_id = "CWE-328"


@register
class HttpDefaultClientRule(RegexRule):
    rule_id = "GO-S09"
    name = "HTTP client without timeout"
    description = "http.DefaultClient or http.Get() have no timeout."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.GO]
    pattern = r"http\.DefaultClient|http\.Get\s*\(|http\.Client\s*\{\s*\}"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Set timeout: &http.Client{Timeout: 30 * time.Second}."
    cwe_id = "CWE-400"


# ===========================================================================
# QUALITY / RELIABILITY (GO-Q01 .. GO-Q15)
# ===========================================================================

@register
class UncheckedErrorRule(BaseRule):
    rule_id = "GO-Q01"
    name = "Unchecked error"
    description = "Error return value discarded with '_'."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.GO]

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            if re.search(r",\s*_\s*[:=]", line) or re.match(r"^\s*_\s*[:=]", stripped):
                if "import" not in stripped and "range" not in stripped:
                    issues.append(self._make_issue(
                        ctx, line=i,
                        message="Error return value discarded with '_'.",
                        suggestion="Handle the error: if err != nil { return err }",
                    ))
        return issues


@register
class FmtInProductionRule(RegexRule):
    rule_id = "GO-Q02"
    name = "fmt.Print in production"
    description = "fmt.Print/Println should not be in production. Use structured logger."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.GO]
    pattern = r"fmt\.(?:Print|Println|Printf)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use log/slog, zerolog, or zap."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower() or ctx.path.name == "main.go":
            return []
        return super().check(ctx)


@register
class PanicInLibraryRule(RegexRule):
    rule_id = "GO-Q03"
    name = "Panic in library code"
    description = "panic() in library code should be avoided. Return errors instead."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.GO]
    pattern = r"\bpanic\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Return an error instead of panicking."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name.endswith("_test.go") or ctx.path.name == "main.go":
            return []
        return super().check(ctx)


@register
class DeferInLoopRule(BaseRule):
    rule_id = "GO-Q04"
    name = "Defer in loop"
    description = "defer inside loop delays cleanup until function returns."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.GO]

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_depth = 0
        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if re.match(r"for\s+", stripped) or re.match(r"for\s*\{", stripped):
                in_loop = True
                loop_depth = 0
            if in_loop:
                loop_depth += stripped.count("{") - stripped.count("}")
                if loop_depth <= 0:
                    in_loop = False
                if re.match(r"\s*defer\s+", line):
                    issues.append(self._make_issue(
                        ctx, line=i,
                        message="defer inside loop delays cleanup until function returns.",
                        suggestion="Use a closure or manual cleanup inside the loop.",
                    ))
        return issues


@register
class GoroutineLeakRule(RegexRule):
    rule_id = "GO-Q05"
    name = "Potential goroutine leak"
    description = "Goroutine started without context or cancellation mechanism."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.GO]
    pattern = r"go\s+func\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Pass context.Context and check ctx.Done() for cancellation."


@register
class InitFunctionRule(RegexRule):
    rule_id = "GO-Q06"
    name = "init() function"
    description = "init() makes testing and ordering difficult."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.GO]
    pattern = r"func\s+init\s*\(\s*\)\s*\{"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Prefer explicit initialization; init() is hard to test."


@register
class ErrorNotWrappedRule(RegexRule):
    rule_id = "GO-Q07"
    name = "Error not wrapped"
    description = "Returning bare error without context."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.GO]
    pattern = r"return\s+err\s*$"
    exclude_pattern = r"^\s*//|fmt\.Errorf|errors\.Wrap|%w"
    fix_suggestion = "Wrap errors: fmt.Errorf(\"operation failed: %w\", err)."


@register
class NilTypeAssertionRule(RegexRule):
    rule_id = "GO-Q08"
    name = "Unsafe type assertion"
    description = "Type assertion without comma-ok pattern panics on failure."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.GO]
    pattern = r"\.\(\*?\w+\)\s*[^,]"
    exclude_pattern = r"^\s*//|,\s*ok"
    fix_suggestion = "Use comma-ok: val, ok := x.(Type)."


@register
class MutexCopiedRule(RegexRule):
    rule_id = "GO-Q09"
    name = "Mutex possibly copied"
    description = "Copying a sync.Mutex leads to undefined behavior."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.GO]
    pattern = r"=\s*\*?\w+\.(?:Mutex|RWMutex)\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Pass mutexes by pointer; never copy a sync.Mutex."


@register
class GlobalMutableStateRule(RegexRule):
    rule_id = "GO-Q10"
    name = "Global mutable state"
    description = "Package-level mutable variable creates coupling."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.GO]
    pattern = r"^var\s+\w+\s*(?:=|map\[|(?:\[\]))"
    exclude_pattern = r"^\s*//|_test\.go"
    fix_suggestion = "Minimize package-level mutable state; use dependency injection."


@register
class TodoFixmeRule(RegexRule):
    rule_id = "GO-Q11"
    name = "TODO/FIXME in code"
    description = "TODO or FIXME indicates unfinished work."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.GO]
    pattern = r"//\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Address or create a tracking issue."


@register
class ContextNotPassedRule(BaseRule):
    rule_id = "GO-Q12"
    name = "Exported func without context"
    description = "Exported function without context.Context parameter."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.GO]

    _FUNC = re.compile(r"^func\s+([A-Z]\w+)\s*\(([^)]*)\)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            match = self._FUNC.match(line)
            if match and "context.Context" not in match.group(2):
                name = match.group(1)
                if name in ("New", "Default", "String", "Error", "Close", "Len", "Less", "Swap"):
                    continue
                issues.append(self._make_issue(
                    ctx, line=i,
                    message=f"Exported function '{name}' lacks context.Context parameter.",
                    suggestion="Accept context.Context as first parameter.",
                ))
        return issues


# ===========================================================================
# PERFORMANCE (GO-P01 .. GO-P06)
# ===========================================================================

@register
class StringConcatInLoopRule(BaseRule):
    rule_id = "GO-P01"
    name = "String concat in loop"
    description = "String concatenation with += in loop is O(n^2)."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.GO]

    _LOOP = re.compile(r"^\s*for\s+")
    _CONCAT = re.compile(r"\w+\s*\+=\s*")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_depth = 0
        for i, line in enumerate(ctx.lines, start=1):
            if self._LOOP.match(line):
                in_loop = True
                loop_depth = 0
            if in_loop:
                loop_depth += line.count("{") - line.count("}")
                if loop_depth <= 0:
                    in_loop = False
                if self._CONCAT.search(line) and "string" in ctx.content[:500].lower():
                    issues.append(self._make_issue(
                        ctx, line=i,
                        message="String concatenation in loop. Use strings.Builder.",
                        suggestion="var b strings.Builder; b.WriteString(s)",
                    ))
        return issues


@register
class RegexCompileInFuncRule(RegexRule):
    rule_id = "GO-P02"
    name = "Regex compile in function"
    description = "regexp.MustCompile in function body runs on every call."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.GO]
    pattern = r"regexp\.(?:MustCompile|Compile)\s*\("
    exclude_pattern = r"^\s*//|^var\s|^func\s+init"
    fix_suggestion = "Move regexp.MustCompile() to a package-level var."


@register
class AppendWithoutPreallocRule(RegexRule):
    rule_id = "GO-P03"
    name = "Append without prealloc"
    description = "Slice grows without pre-allocated capacity."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.GO]
    pattern = r"make\(\[\]\w+,\s*0\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Pre-allocate: make([]T, 0, expectedLen)."


@register
class MapWithoutSizeRule(RegexRule):
    rule_id = "GO-P04"
    name = "Map without size hint"
    description = "Map created without capacity hint."
    severity = Severity.INFO
    category = Category.PERFORMANCE
    languages = [Language.GO]
    pattern = r"make\(map\[\w+\]\w+\)\s*$"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Provide size hint: make(map[K]V, expectedSize)."
