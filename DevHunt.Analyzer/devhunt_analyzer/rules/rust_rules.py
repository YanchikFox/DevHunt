from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class UnwrapUsageRule(RegexRule):
    rule_id = "RS-Q01"
    name = "unwrap() usage"
    description = ".unwrap() panics on None/Err. Handle errors gracefully."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.RUST]
    pattern = r"\.unwrap\(\)"
    exclude_pattern = r"^\s*//|#\[test\]|#\[cfg\(test\)\]"
    fix_suggestion = "Use .expect('msg'), ?, .unwrap_or(), or match."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


@register
class ExpectWithoutMessageRule(RegexRule):
    rule_id = "RS-Q02"
    name = "expect() with poor message"
    description = ".expect() should have a meaningful error message."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"""\.expect\s*\(\s*["'](?:error|failed|fail|err|oops|bug|todo|xxx)["']\s*\)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Provide a descriptive message: .expect('Failed to parse config file')."


@register
class UnsafeBlockRule(RegexRule):
    rule_id = "RS-S01"
    name = "Unsafe block"
    description = "unsafe block bypasses Rust's memory safety guarantees."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"\bunsafe\s*\{"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Minimize unsafe code. Document safety invariants with // SAFETY: comment."


@register
class TodoFixmeRule(RegexRule):
    rule_id = "RS-Q03"
    name = "TODO/FIXME in code"
    description = "TODO or FIXME comment indicates unfinished work."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.RUST]
    pattern = r"//\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Address the TODO or create a tracking issue."


@register
class PanicInLibRule(RegexRule):
    rule_id = "RS-Q04"
    name = "panic! in library code"
    description = "panic!/todo!/unimplemented! should not be in library code."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.RUST]
    pattern = r"\b(?:panic|todo|unimplemented)!\s*\("
    exclude_pattern = r"^\s*//|#\[test\]"
    fix_suggestion = "Return Result<T, E> instead of panicking."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower() or "main.rs" == ctx.path.name:
            return []
        return super().check(ctx)


@register
class CloneOnLargeTypeRule(RegexRule):
    rule_id = "RS-P01"
    name = "Clone on potentially large type"
    description = ".clone() on Vec, String, or HashMap may be expensive."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUST]
    pattern = r"(?:Vec|String|HashMap|BTreeMap|HashSet)\s*(?:>|::).*?\.clone\(\)|\.to_vec\(\)|\.to_string\(\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Consider borrowing (&) or using Cow<> to avoid unnecessary clones."


@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "RS-S02"
    name = "Hardcoded secret"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"""(?i)(?:password|secret|api_?key|token)\s*[:=]\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = "Use environment variables: std::env::var('SECRET_KEY')."
    cwe_id = "CWE-798"


@register
class SqlFormatRule(RegexRule):
    rule_id = "RS-S03"
    name = "SQL string formatting"
    description = "String formatting in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"""format!\s*\(\s*["'](?i:SELECT|INSERT|UPDATE|DELETE)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized queries with sqlx::query! or diesel."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# SECURITY — RS-S04 through RS-S09
# ---------------------------------------------------------------------------


@register
class CommandInjectionRule(RegexRule):
    rule_id = "RS-S04"
    name = "Command injection via std::process::Command"
    description = (
        "Passing user-controlled input to std::process::Command::new() or .arg() "
        "can lead to OS command injection."
    )
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"\bCommand\s*::\s*new\s*\(|\.arg\s*\(\s*(?:&?[a-z_][a-z0-9_]*|format!)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Never pass unsanitized user input to Command. Use an allowlist of permitted "
        "commands and pass arguments as separate .arg() calls, never via shell interpolation."
    )
    cwe_id = "CWE-78"

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        # Only flag files that actually import std::process or use Command
        if "Command" not in ctx.content:
            return []
        return super().check(ctx)


@register
class InsecureDeserializationRule(RegexRule):
    rule_id = "RS-S05"
    name = "Insecure deserialization"
    description = (
        "deserialize_any or bincode deserialization from untrusted sources can lead "
        "to type confusion or remote code execution."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"\bdeserialize_any\b|bincode\s*::\s*(?:deserialize|decode)\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Deserialize into a concrete, strongly-typed struct. Validate and bound-check "
        "all fields before use. Prefer serde_json::from_str with explicit types."
    )
    cwe_id = "CWE-502"


@register
class PathTraversalRule(RegexRule):
    rule_id = "RS-S06"
    name = "Path traversal via concatenated path"
    description = (
        "Concatenating user input into file system paths allows directory traversal attacks."
    )
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = (
        r"(?:fs::(?:read|write|remove_file|create_dir|read_to_string|File::open|File::create)"
        r"|Path::new\s*\(|PathBuf::from\s*\()"
        r".*?(?:format!|push\s*\(|join\s*\()"
    )
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Canonicalize the final path with std::fs::canonicalize() and verify it starts "
        "with the expected base directory before performing any file operation."
    )
    cwe_id = "CWE-22"


@register
class WeakCryptoRule(RegexRule):
    rule_id = "RS-S07"
    name = "Weak cryptographic algorithm"
    description = "MD5 and SHA-1 are cryptographically broken and must not be used for security."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"\b(?:md5|sha1)\s*::|extern\s+crate\s+(?:md5|sha1)\b"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use SHA-256 or stronger (sha2 crate) for hashing. "
        "For passwords use argon2, bcrypt, or scrypt."
    )
    cwe_id = "CWE-327"


@register
class HttpWithoutTlsRule(RegexRule):
    rule_id = "RS-S08"
    name = "HTTP without TLS"
    description = (
        "Using plain HTTP URLs in reqwest or hyper transmits data in cleartext."
    )
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"""["']http://(?!localhost|127\.0\.0\.1)[^"']+["']"""
    exclude_pattern = r"^\s*//|#\[cfg\(test\)\]|#\[test\]"
    fix_suggestion = (
        "Use HTTPS URLs for all external communication. "
        "For development-only targets, gate with #[cfg(debug_assertions)]."
    )
    cwe_id = "CWE-319"

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


@register
class DisabledCertVerificationRule(RegexRule):
    rule_id = "RS-S09"
    name = "Disabled TLS certificate verification"
    description = (
        "danger_accept_invalid_certs(true) disables certificate validation, "
        "making the connection vulnerable to MITM attacks."
    )
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUST]
    pattern = r"danger_accept_invalid_certs\s*\(\s*true\s*\)|danger_accept_invalid_hostnames\s*\(\s*true\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Never disable certificate verification in production code. "
        "Use a proper CA bundle or add specific trusted certificates."
    )
    cwe_id = "CWE-295"

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# QUALITY — RS-Q05 through RS-Q12
# ---------------------------------------------------------------------------


@register
class DeadCodeAllowRule(RegexRule):
    rule_id = "RS-Q05"
    name = "#[allow(dead_code)] overuse"
    description = (
        "#[allow(dead_code)] suppresses warnings for unused items. "
        "Accumulation indicates dead weight in the codebase."
    )
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"#\s*\[\s*allow\s*\(\s*dead_code\s*\)\s*\]"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Remove unused code or mark intentionally unused items with a leading underscore (_name). "
        "Reserve #[allow(dead_code)] for FFI structs and intentional stubs."
    )


@register
class ClippyAllowAllRule(RegexRule):
    rule_id = "RS-Q06"
    name = "#[allow(clippy::all)] disables lints"
    description = (
        "#[allow(clippy::all)] silences all Clippy diagnostics for the annotated item, "
        "hiding real bugs and style violations."
    )
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"#\s*\[\s*allow\s*\(\s*clippy\s*::\s*(?:all|restriction)\s*\)\s*\]"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Suppress only the specific lint you need: #[allow(clippy::too_many_arguments)]. "
        "Document the reason in an inline comment."
    )


@register
class LargeEnumVariantRule(BaseRule):
    """Flag enum variants that have more than 3 fields (proxy for large discriminated unions)."""

    rule_id = "RS-Q07"
    name = "Large enum variant"
    description = (
        "An enum variant with more than 3 fields may cause the whole enum to be "
        "padded to the size of the largest variant, wasting memory."
    )
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUST]
    fix_suggestion = (
        "Box the large variant's payload or extract it into a named struct. "
        "Clippy's large_enum_variant lint can calculate the exact size difference."
    )

    # Matches:  VariantName { field1: T, field2: T, field3: T, field4: T }
    _variant_re = re.compile(
        r"^\s*[A-Z][A-Za-z0-9_]*\s*\{([^}]+)\}"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            m = self._variant_re.search(line)
            if not m:
                continue
            fields_section = m.group(1)
            # Count fields by counting colons (each `name: Type` has one colon)
            field_count = fields_section.count(":")
            if field_count > 3:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=m.start(),
                    message=(
                        f"Enum variant has {field_count} fields. "
                        "Consider boxing or extracting to a struct."
                    ),
                    suggestion=self.fix_suggestion,
                ))
        return issues


@register
class PrintlnInLibRule(RegexRule):
    rule_id = "RS-Q08"
    name = "println!/eprintln! in library code"
    description = (
        "println! and eprintln! write directly to stdout/stderr. "
        "Library code should use the log crate (trace!/debug!/info!/warn!/error!) instead."
    )
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"\b(?:println|eprintln)!\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Replace with log::info!/log::error! (or tracing::info!/tracing::error!). "
        "This lets the application binary control the log sink and format."
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        # Allow in binary entry points and test files
        if ctx.path.name in ("main.rs", "bin.rs") or "test" in ctx.path.name.lower():
            return []
        # Allow in example files (examples/ directory)
        if "examples" in ctx.path.parts:
            return []
        return super().check(ctx)


@register
class MutexPoisonUnwrapRule(RegexRule):
    rule_id = "RS-Q09"
    name = "Mutex lock() unwrap without PoisonError handling"
    description = (
        ".lock().unwrap() on a Mutex panics if another thread panicked while holding "
        "the lock (PoisonError). This can bring down the whole process."
    )
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.RUST]
    pattern = r"\.lock\s*\(\s*\)\s*\.unwrap\s*\(\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use .lock().unwrap_or_else(|e| e.into_inner()) to recover from poison, "
        "or handle the PoisonError explicitly with match."
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


@register
class BoxDynErrorReturnRule(RegexRule):
    rule_id = "RS-Q10"
    name = "Box<dyn Error> as return type"
    description = (
        "Returning Box<dyn std::error::Error> erases error type information and "
        "forces callers to downcast. Use a typed error type instead."
    )
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"Box\s*<\s*dyn\s+(?:std::error::)?Error\s*>"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Define a typed error enum with thiserror, or use anyhow::Result<T> "
        "for application-level code where error propagation context is sufficient."
    )


@register
class RecursiveFunctionRule(BaseRule):
    """Detect recursive functions that lack an explicit depth or iteration limit."""

    rule_id = "RS-Q11"
    name = "Recursive function without depth limit"
    description = (
        "A function that calls itself without a depth counter risks stack overflow "
        "on deeply nested or adversarial input."
    )
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.RUST]
    fix_suggestion = (
        "Add a depth parameter and return an error when the limit is exceeded, "
        "or convert to an explicit stack-based iterative approach."
    )

    _fn_name_re = re.compile(r"^\s*(?:pub\s+)?(?:async\s+)?fn\s+([A-Za-z_][A-Za-z0-9_]*)\s*[<(]")
    _depth_keyword_re = re.compile(r"\bdepth\b|\blimit\b|\bmax_depth\b|\bmax_level\b")

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []

        issues: list[Issue] = []
        lines = ctx.lines

        i = 0
        while i < len(lines):
            m = self._fn_name_re.match(lines[i])
            if m:
                fn_name = m.group(1)
                fn_start = i + 1  # 1-based

                # Collect the function body by tracking brace depth
                brace_depth = 0
                body_lines: list[tuple[int, str]] = []
                j = i
                found_open = False
                while j < len(lines):
                    ln = lines[j]
                    for ch in ln:
                        if ch == "{":
                            brace_depth += 1
                            found_open = True
                        elif ch == "}":
                            brace_depth -= 1
                    body_lines.append((j + 1, ln))
                    if found_open and brace_depth == 0:
                        break
                    j += 1

                # Check if function calls itself
                call_pattern = re.compile(r"\b" + re.escape(fn_name) + r"\s*\(")
                body_text = "".join(ln for _, ln in body_lines)
                if call_pattern.search(body_text):
                    # Only flag if no depth-limiting variable is present
                    if not self._depth_keyword_re.search(body_text):
                        issues.append(self._make_issue(
                            ctx,
                            line=fn_start,
                            column=0,
                            message=(
                                f"Function '{fn_name}' is recursive but has no depth limit. "
                                "May cause stack overflow on deep input."
                            ),
                            suggestion=self.fix_suggestion,
                        ))
                i = j  # skip past the function body

            i += 1

        return issues


@register
class StringFormatInPanicRule(RegexRule):
    rule_id = "RS-Q12"
    name = "String format! used inside panic! message"
    description = (
        "Using format!() inside panic!() is redundant — panic! already accepts "
        "a format string. The extra allocation adds noise and cost."
    )
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.RUST]
    pattern = r"\bpanic!\s*\(\s*format!\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Pass the format string directly: panic!(\"value was {val}\") "
        "instead of panic!(format!(\"value was {val}\"))."
    )


# ---------------------------------------------------------------------------
# PERFORMANCE — RS-P02 through RS-P04
# ---------------------------------------------------------------------------


@register
class CollectThenIterateRule(BaseRule):
    """Detect collect() immediately followed by .iter()/.into_iter() on the next line or same line."""

    rule_id = "RS-P02"
    name = "collect() then iterate"
    description = (
        "Calling .collect::<Vec<_>>() followed immediately by .iter() or .into_iter() "
        "allocates an intermediate Vec that is never needed."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUST]
    fix_suggestion = (
        "Remove the collect() and keep the iterator chain. "
        "Only collect when you need an owned, indexable collection."
    )

    _collect_re = re.compile(r"\.collect\s*:?\s*:?\s*<[^>]*>\s*\(\s*\)|\.collect\s*\(\s*\)")
    _iter_re = re.compile(r"\.(?:iter|into_iter|iter_mut)\s*\(\s*\)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines

        for i, line in enumerate(lines):
            stripped = line.rstrip()
            if not self._collect_re.search(stripped):
                continue

            # Check same line
            # Remove the collect match, then look for .iter on the remainder
            after_collect = self._collect_re.sub("", stripped, count=1)
            if self._iter_re.search(after_collect):
                issues.append(self._make_issue(
                    ctx,
                    line=i + 1,
                    column=0,
                    message=(
                        "Intermediate collect() immediately followed by iteration. "
                        "The Vec allocation is unnecessary."
                    ),
                    suggestion=self.fix_suggestion,
                ))
                continue

            # Check the next non-blank line
            j = i + 1
            while j < len(lines) and lines[j].strip() == "":
                j += 1
            if j < len(lines) and self._iter_re.search(lines[j]):
                issues.append(self._make_issue(
                    ctx,
                    line=i + 1,
                    column=0,
                    message=(
                        "Intermediate collect() immediately followed by iteration. "
                        "The Vec allocation is unnecessary."
                    ),
                    suggestion=self.fix_suggestion,
                ))

        return issues


@register
class UnnecessaryAllocationRule(RegexRule):
    rule_id = "RS-P03"
    name = "Unnecessary heap allocation"
    description = (
        "String::from(\"\") or format!(\"\") for a static string literal allocates "
        "on the heap when a &'static str would suffice."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUST]
    pattern = r'String::from\s*\(\s*"[^"]*"\s*\)|format!\s*\(\s*"[^{}]*"\s*\)'
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use a &str literal directly where ownership is not required. "
        "If you need a String, prefer .to_owned() on a literal to signal intent. "
        "Replace format!(\"literal\") with \"literal\".to_owned()."
    )


@register
class ArcMutexInsteadOfAtomicRule(RegexRule):
    rule_id = "RS-P04"
    name = "Arc<Mutex<>> for simple shared state"
    description = (
        "Arc<Mutex<T>> for primitive types (integers, booleans) is significantly "
        "slower than std::sync::atomic types or parking_lot::Mutex."
    )
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUST]
    pattern = r"Arc\s*<\s*Mutex\s*<\s*(?:u8|u16|u32|u64|u128|usize|i8|i16|i32|i64|i128|isize|bool|f32|f64)\s*>"
    exclude_pattern = r"^\s*//"
    fix_suggestion = (
        "Use std::sync::atomic::AtomicUsize / AtomicBool / AtomicI64 etc. for primitive types. "
        "For non-primitive types, consider parking_lot::Mutex which is faster than std::sync::Mutex "
        "and does not have PoisonError semantics."
    )
