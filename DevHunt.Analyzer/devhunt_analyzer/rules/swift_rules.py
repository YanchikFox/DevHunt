from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class ForceUnwrapRule(RegexRule):
    rule_id = "SW-Q01"
    name = "Force unwrap (!)"
    description = "Force unwrap crashes on nil. Use optional binding or guard."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.SWIFT]
    pattern = r"\w+!\.\w+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use 'if let', 'guard let', or nil coalescing (??)."


@register
class ForceCastRule(RegexRule):
    rule_id = "SW-Q02"
    name = "Force cast (as!)"
    description = "Force cast crashes on type mismatch."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.SWIFT]
    pattern = r"\bas!\s+\w+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use 'as?' with optional binding instead."


@register
class PrintInProductionRule(RegexRule):
    rule_id = "SW-Q03"
    name = "print() in production"
    description = "print() should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.SWIFT]
    pattern = r"\bprint\s*\("
    exclude_pattern = r"^\s*//|#if DEBUG"
    fix_suggestion = "Use os_log or Logger framework. Wrap in #if DEBUG for debug prints."

    def check(self, ctx):
        if "test" in ctx.path.name.lower() or "Test" in ctx.path.name:
            return []
        return super().check(ctx)


@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "SW-S01"
    name = "Hardcoded secret"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = r"""(?i)(?:password|secret|apiKey|api_key|token)\s*[:=]\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = "Use Keychain or environment configuration."
    cwe_id = "CWE-798"


@register
class InsecureHTTPRule(RegexRule):
    rule_id = "SW-S02"
    name = "Insecure HTTP URL"
    description = "Using http:// instead of https:// is insecure."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = r"""(?:URL|url)\s*[:=].*["']http://(?!localhost|127\.0\.0\.1|0\.0\.0\.0)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use https:// for all external URLs."
    cwe_id = "CWE-319"


@register
class RetainCycleRule(RegexRule):
    rule_id = "SW-P01"
    name = "Potential retain cycle"
    description = "Closure captures self strongly. Use [weak self] or [unowned self]."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.SWIFT]
    pattern = r"\{\s*(?!\[(?:weak|unowned)).*?\bself\.\w+"
    exclude_pattern = r"^\s*//|\[weak self\]|\[unowned self\]"
    fix_suggestion = "Add [weak self] to closure: { [weak self] in guard let self else { return } }."


@register
class NSLogRule(RegexRule):
    rule_id = "SW-Q04"
    name = "NSLog usage"
    description = "NSLog is slow and leaks to device console. Use os_log/Logger."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.SWIFT]
    pattern = r"\bNSLog\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use Logger (iOS 14+) or os_log for structured logging."


@register
class DispatchMainSyncRule(RegexRule):
    rule_id = "SW-Q05"
    name = "DispatchQueue.main.sync"
    description = "sync on main queue from main thread causes deadlock."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.SWIFT]
    pattern = r"DispatchQueue\.main\.sync\s*\{"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use DispatchQueue.main.async or check if already on main thread."


# ---------------------------------------------------------------------------
# SECURITY
# ---------------------------------------------------------------------------


@register
class InsecureKeychainAccessibilityRule(RegexRule):
    rule_id = "SW-S03"
    name = "Insecure Keychain accessibility"
    description = (
        "kSecAttrAccessibleAlways stores Keychain items accessible even when "
        "the device is locked, including when it has never been unlocked after reboot."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = r"\bkSecAttrAccessibleAlways\b(?!WhenPasscodeSetThisDeviceOnly|WhenUnlocked)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use kSecAttrAccessibleWhenUnlockedThisDeviceOnly or "
        "kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly instead."
    )
    cwe_id = "CWE-312"


@register
class DisabledATSRule(RegexRule):
    rule_id = "SW-S04"
    name = "Disabled App Transport Security"
    description = (
        "NSAllowsArbitraryLoads disables ATS and permits cleartext HTTP connections, "
        "exposing network traffic to interception."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = r"\bNSAllowsArbitraryLoads\b\s*[=:]\s*(?:true|YES|1)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Remove NSAllowsArbitraryLoads. Add specific NSExceptionDomains for "
        "domains that genuinely cannot use HTTPS."
    )
    cwe_id = "CWE-319"


@register
class SensitiveDataLoggingRule(RegexRule):
    rule_id = "SW-S05"
    name = "Sensitive data in logs"
    description = (
        "Logging calls that reference sensitive field names (password, token, secret, "
        "ssn, creditCard) may expose private data in system logs."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = (
        r"""(?:os_log|Logger\.\w+|print|NSLog)\s*\("""
        r"""[^)]*(?i)(?:password|token|secret|ssn|credit.?card|cvv|pin)[^)]*\)"""
    )
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Never log sensitive fields. Use os_log privacy annotations like "
        "%{private}@ when a value must be logged for diagnostics."
    )
    cwe_id = "CWE-532"


@register
class SQLInjectionRule(RegexRule):
    rule_id = "SW-S06"
    name = "SQL injection via string interpolation"
    description = (
        "sqlite3_exec or raw SQL string built with string interpolation allows "
        "SQL injection if any interpolated value originates from user input."
    )
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = r"""sqlite3_exec\s*\([^,]+,\s*["'][^"']*\\\("""
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use sqlite3_prepare_v2 with sqlite3_bind_* for parameterized queries, "
        "or switch to a higher-level ORM."
    )
    cwe_id = "CWE-89"


@register
class InsecureWebViewRule(BaseRule):
    rule_id = "SW-S07"
    name = "Insecure WKWebView configuration"
    description = (
        "WKWebView with JavaScript enabled that loads a user-supplied or "
        "remote URL is vulnerable to cross-site scripting and content injection."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.SWIFT]
    fix_suggestion = (
        "Disable JavaScript when loading untrusted URLs, or use a Content Security "
        "Policy and validate the URL against an allowlist before loading."
    )
    cwe_id = "CWE-79"

    _js_enabled = re.compile(r"javaScriptEnabled\s*=\s*true")
    _load_url = re.compile(r"\.load\s*\(\s*URLRequest\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        source = ctx.content
        if not self._js_enabled.search(source):
            return issues
        for i, line in enumerate(ctx.lines, start=1):
            if self._load_url.search(line) and not line.lstrip().startswith("//"):
                issues.append(
                    Issue(
                        rule_id=self.rule_id,
                        name=self.name,
                        description=self.description,
                        severity=self.severity,
                        category=self.category,
                        file_path=ctx.path,
                        line=i,
                        column=1,
                        snippet=line.rstrip(),
                        fix_suggestion=self.fix_suggestion,
                        cwe_id=self.cwe_id,
                    )
                )
        return issues


@register
class HardcodedCryptoKeyRule(RegexRule):
    rule_id = "SW-S08"
    name = "Hardcoded cryptographic key or IV"
    description = (
        "Cryptographic keys or initialization vectors stored as string or byte "
        "literals are recoverable by static analysis of the binary."
    )
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.SWIFT]
    pattern = (
        r"""(?i)(?:let|var)\s+(?:key|iv|secret|aesKey|encryptionKey|initVector)"""
        r"""\s*[:=][^=\n]*["'][A-Za-z0-9+/=]{8,}["']"""
    )
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = (
        "Derive keys with CryptoKit's HKDF/PBKDF2 from a securely stored seed, "
        "or retrieve them from the Keychain at runtime."
    )
    cwe_id = "CWE-321"


# ---------------------------------------------------------------------------
# QUALITY
# ---------------------------------------------------------------------------


@register
class TodoFixmeRule(RegexRule):
    rule_id = "SW-Q06"
    name = "TODO/FIXME comment"
    description = "Unresolved TODO or FIXME comment indicates incomplete or broken code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.SWIFT]
    pattern = r"//\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Resolve the TODO/FIXME or convert it to a tracked issue."


@register
class FunctionTooLongRule(BaseRule):
    rule_id = "SW-Q07"
    name = "Function too long"
    description = "Function body exceeds 50 lines, which reduces readability and testability."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.SWIFT]
    fix_suggestion = "Extract cohesive blocks into smaller, focused functions."

    _func_start = re.compile(
        r"^\s*(?:(?:private|public|internal|open|fileprivate|override|static|class|"
        r"mutating|nonmutating|final|required|convenience|dynamic)\s+)*"
        r"func\s+\w+"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        n = len(lines)
        i = 0
        while i < n:
            if self._func_start.match(lines[i]):
                func_start_line = i + 1
                # Find opening brace (may be on same or next line)
                brace_depth = 0
                body_start = None
                j = i
                while j < n:
                    for ch in lines[j]:
                        if ch == "{":
                            brace_depth += 1
                            if body_start is None:
                                body_start = j
                        elif ch == "}":
                            brace_depth -= 1
                    if body_start is not None and brace_depth == 0:
                        func_end_line = j + 1
                        length = func_end_line - func_start_line
                        if length > 50:
                            issues.append(
                                Issue(
                                    rule_id=self.rule_id,
                                    name=self.name,
                                    description=(
                                        f"Function starting at line {func_start_line} "
                                        f"is {length} lines long (limit: 50)."
                                    ),
                                    severity=self.severity,
                                    category=self.category,
                                    file_path=ctx.path,
                                    line=func_start_line,
                                    column=1,
                                    snippet=lines[i].rstrip(),
                                    fix_suggestion=self.fix_suggestion,
                                )
                            )
                        i = j
                        break
                    j += 1
            i += 1
        return issues


@register
class LargeClassRule(BaseRule):
    rule_id = "SW-Q08"
    name = "Large class/struct"
    description = (
        "Class or struct exceeds 500 lines. Large types are hard to maintain "
        "and often violate the Single Responsibility Principle."
    )
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.SWIFT]
    fix_suggestion = (
        "Split the type into smaller, focused types using protocols and extensions."
    )

    _type_start = re.compile(
        r"^\s*(?:(?:private|public|internal|open|fileprivate|final)\s+)*"
        r"(?:class|struct|actor)\s+\w+"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        n = len(lines)
        i = 0
        while i < n:
            if self._type_start.match(lines[i]):
                type_start_line = i + 1
                brace_depth = 0
                body_started = False
                j = i
                while j < n:
                    for ch in lines[j]:
                        if ch == "{":
                            brace_depth += 1
                            body_started = True
                        elif ch == "}":
                            brace_depth -= 1
                    if body_started and brace_depth == 0:
                        type_end_line = j + 1
                        length = type_end_line - type_start_line
                        if length > 500:
                            issues.append(
                                Issue(
                                    rule_id=self.rule_id,
                                    name=self.name,
                                    description=(
                                        f"Type starting at line {type_start_line} "
                                        f"is {length} lines long (limit: 500)."
                                    ),
                                    severity=self.severity,
                                    category=self.category,
                                    file_path=ctx.path,
                                    line=type_start_line,
                                    column=1,
                                    snippet=lines[i].rstrip(),
                                    fix_suggestion=self.fix_suggestion,
                                )
                            )
                        i = j
                        break
                    j += 1
            i += 1
        return issues


@register
class DeeplyNestedCodeRule(BaseRule):
    rule_id = "SW-Q09"
    name = "Deeply nested code"
    description = (
        "Code indented more than 4 levels (>16 spaces / >4 tabs) is hard to read "
        "and typically signals excessive cyclomatic complexity."
    )
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.SWIFT]
    fix_suggestion = (
        "Extract nested blocks into helper functions or use early returns / guard statements."
    )

    # Matches lines with more than 4 levels of indentation (spaces or tabs).
    _deep_indent_spaces = re.compile(r"^ {17,}\S")
    _deep_indent_tabs = re.compile(r"^\t{5,}\S")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.rstrip()
            if not stripped or stripped.lstrip().startswith("//"):
                continue
            if self._deep_indent_spaces.match(line) or self._deep_indent_tabs.match(line):
                issues.append(
                    Issue(
                        rule_id=self.rule_id,
                        name=self.name,
                        description=self.description,
                        severity=self.severity,
                        category=self.category,
                        file_path=ctx.path,
                        line=i,
                        column=1,
                        snippet=stripped,
                        fix_suggestion=self.fix_suggestion,
                    )
                )
        return issues


@register
class EmptyCatchBlockRule(RegexRule):
    rule_id = "SW-Q10"
    name = "Empty catch block"
    description = (
        "An empty catch block silently swallows errors, making failures "
        "invisible and extremely difficult to debug."
    )
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.SWIFT]
    pattern = r"\}\s*catch\s*\{?\s*\}"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "At minimum log the error with Logger/os_log. "
        "Consider propagating it with 'throws' or presenting it to the user."
    )


@register
class DeprecatedAPIRule(RegexRule):
    rule_id = "SW-Q11"
    name = "Deprecated API usage"
    description = (
        "Known deprecated APIs detected. These may be removed in future OS versions "
        "and produce runtime warnings."
    )
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.SWIFT]
    pattern = (
        r"\b(?:"
        r"UIAlertView|UIActionSheet|UIWebView|"
        r"valueForKeyPath:|setValue:forKeyPath:|"
        r"NSURLConnection\.sendSynchronousRequest|"
        r"UIApplication\.shared\.statusBarOrientation|"
        r"UIApplication\.shared\.statusBarFrame|"
        r"addObserver:selector:name:object:"
        r")\b"
    )
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Replace with modern equivalents: UIAlertController, WKWebView, "
        "URLSession, NotificationCenter.addObserver(forName:)."
    )


@register
class MassiveSwiftUIBodyRule(BaseRule):
    rule_id = "SW-Q12"
    name = "Massive SwiftUI view body"
    description = (
        "SwiftUI 'var body' exceeding 50 lines is difficult to read and slows "
        "incremental compilation."
    )
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.SWIFT]
    fix_suggestion = (
        "Extract sub-views into separate View structs or computed properties "
        "to keep 'body' concise and composable."
    )

    _body_start = re.compile(r"^\s*var\s+body\s*:\s*some\s+View\s*\{")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        n = len(lines)
        i = 0
        while i < n:
            if self._body_start.match(lines[i]):
                body_start_line = i + 1
                brace_depth = 0
                j = i
                while j < n:
                    for ch in lines[j]:
                        if ch == "{":
                            brace_depth += 1
                        elif ch == "}":
                            brace_depth -= 1
                    if brace_depth == 0 and j > i:
                        body_end_line = j + 1
                        length = body_end_line - body_start_line
                        if length > 50:
                            issues.append(
                                Issue(
                                    rule_id=self.rule_id,
                                    name=self.name,
                                    description=(
                                        f"SwiftUI body at line {body_start_line} "
                                        f"is {length} lines long (limit: 50)."
                                    ),
                                    severity=self.severity,
                                    category=self.category,
                                    file_path=ctx.path,
                                    line=body_start_line,
                                    column=1,
                                    snippet=lines[i].rstrip(),
                                    fix_suggestion=self.fix_suggestion,
                                )
                            )
                        i = j
                        break
                    j += 1
            i += 1
        return issues


# ---------------------------------------------------------------------------
# PERFORMANCE
# ---------------------------------------------------------------------------


@register
class LargeArrayCopyRule(RegexRule):
    rule_id = "SW-P02"
    name = "Large array passed by value"
    description = (
        "Passing an array named with plural nouns as a function argument by value "
        "triggers a full copy. For large collections this can be a significant "
        "performance hit."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.SWIFT]
    # Matches function calls where a variable that looks like a collection (plural noun
    # or ends with Array/List/Items/Elements) is passed directly as an argument.
    pattern = (
        r"\bfunc\s+\w+\s*\([^)]*\b(?:[a-z]\w*(?:s|List|Array|Items|Elements))\s*:\s*\["
    )
    exclude_pattern = r"^\s*//|inout\s"
    fix_suggestion = (
        "Mark the parameter 'inout' if mutation is needed, or accept a "
        "Sequence/Collection protocol type to avoid the copy."
    )


@register
class UnnecessaryAnyViewRule(RegexRule):
    rule_id = "SW-P03"
    name = "Unnecessary AnyView type erasure"
    description = (
        "AnyView wraps a view in a type-erased box, disabling SwiftUI's "
        "structural diffing and forcing full subtree re-evaluation on every render."
    )
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.SWIFT]
    pattern = r"\bAnyView\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use @ViewBuilder with conditional branches, or 'some View' return type, "
        "to preserve SwiftUI's diffing optimizations."
    )


@register
class ImageWithoutCachingRule(RegexRule):
    rule_id = "SW-P04"
    name = "Image loaded without cache policy"
    description = (
        "URLRequest for image resources without an explicit cachePolicy defaults "
        "to .useProtocolCachePolicy, which may result in redundant network fetches "
        "and increased memory pressure."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.SWIFT]
    pattern = (
        r"URLRequest\s*\(\s*url\s*:[^)]*\)"
        r"(?!\s*\n*\s*\.cachePolicy\b)"
    )
    exclude_pattern = r"^\s*//|cachePolicy"
    fix_suggestion = (
        "Set an explicit cachePolicy, e.g. URLRequest(url: url, "
        "cachePolicy: .returnCacheDataElseLoad, timeoutInterval: 30). "
        "Consider using SDWebImage or Kingfisher for image-specific caching."
    )
