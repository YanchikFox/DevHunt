from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
# SECURITY (JV-S01 .. JV-S12)
# ===========================================================================

@register
class SqlConcatenationRule(RegexRule):
    rule_id = "JV-S01"
    name = "SQL concatenation"
    description = "String concatenation in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"""(?i)(?:Statement|createStatement|executeQuery|executeUpdate|prepareStatement)\s*\(?\s*["']?\s*(?:SELECT|INSERT|UPDATE|DELETE).*?\+"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use PreparedStatement with parameter binding."
    cwe_id = "CWE-89"


@register
class XssReflectionRule(RegexRule):
    rule_id = "JV-S02"
    name = "XSS reflection risk"
    description = "Request parameter directly used in response output."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"(?:getParameter|getHeader)\s*\(.*?\).*?(?:println|write|append|setAttribute)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Sanitize user input with OWASP encoder before output."
    cwe_id = "CWE-79"


@register
class DeserializationRule(RegexRule):
    rule_id = "JV-S03"
    name = "Unsafe deserialization"
    description = "ObjectInputStream can execute arbitrary code from untrusted data."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"ObjectInputStream\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use JSON/protobuf or implement ObjectInputFilter allowlist."
    cwe_id = "CWE-502"


@register
class HardcodedCredentialsRule(RegexRule):
    rule_id = "JV-S04"
    name = "Hardcoded credentials"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"""(?i)(?:password|secret|api_?key|token|private_?key)\s*=\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*//|test|mock|example"
    fix_suggestion = "Use environment variables or a secrets manager."
    cwe_id = "CWE-798"


@register
class InsecureCryptoRule(RegexRule):
    rule_id = "JV-S05"
    name = "Insecure cryptography"
    description = "MD5/SHA1/DES are cryptographically broken."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"""MessageDigest\.getInstance\s*\(\s*["'](?:MD5|SHA-?1|DES)["']\)|Cipher\.getInstance\s*\(\s*["']DES"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use SHA-256+ for hashing and AES-GCM for encryption."
    cwe_id = "CWE-327"


@register
class InsecureRandomRule(RegexRule):
    rule_id = "JV-S06"
    name = "Insecure random"
    description = "java.util.Random is not cryptographically secure."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"new\s+Random\s*\("
    exclude_pattern = r"^\s*//|SecureRandom"
    fix_suggestion = "Use java.security.SecureRandom for security-sensitive operations."
    cwe_id = "CWE-330"


@register
class XxeRule(RegexRule):
    rule_id = "JV-S07"
    name = "XXE in XML parser"
    description = "XML parser without disabled external entities is vulnerable to XXE."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"(?:DocumentBuilderFactory|SAXParserFactory|XMLInputFactory)\.newInstance\s*\("
    exclude_pattern = r"^\s*//|setFeature|FEATURE"
    fix_suggestion = "Disable external entities: setFeature(XMLConstants.FEATURE_SECURE_PROCESSING, true)."
    cwe_id = "CWE-611"


@register
class SsrfRule(RegexRule):
    rule_id = "JV-S08"
    name = "SSRF via URL connection"
    description = "Opening URL connection with user input creates SSRF risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"new\s+URL\s*\(.*\)\.open(?:Connection|Stream)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Validate and allowlist URLs before opening connections."
    cwe_id = "CWE-918"


@register
class PathTraversalRule(RegexRule):
    rule_id = "JV-S09"
    name = "Path traversal"
    description = "File creation with user input enables path traversal."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"new\s+File\s*\(.*(?:getParameter|request\.|input)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Canonicalize path and verify it's within allowed directory."
    cwe_id = "CWE-22"


@register
class LdapInjectionRule(RegexRule):
    rule_id = "JV-S10"
    name = "LDAP injection"
    description = "User input in LDAP search creates injection risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"search\s*\(.*\+\s*(?:request|input|param)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use LDAP parameterized filters or escape special characters."
    cwe_id = "CWE-90"


@register
class CommandInjectionRule(RegexRule):
    rule_id = "JV-S11"
    name = "Command injection"
    description = "Runtime.exec with concatenation creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"Runtime\.getRuntime\(\)\.exec\s*\(.*\+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use ProcessBuilder with explicit argument list."
    cwe_id = "CWE-78"


@register
class InsecureTlsRule(RegexRule):
    rule_id = "JV-S12"
    name = "Insecure TLS version"
    description = "SSLv3, TLSv1.0, and TLSv1.1 are deprecated and insecure."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"""SSLContext\.getInstance\s*\(\s*["'](?:SSL|TLSv1|TLSv1\.1)["']\)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use TLSv1.2 or TLSv1.3."
    cwe_id = "CWE-326"


# ===========================================================================
# QUALITY / RELIABILITY (JV-Q01 .. JV-Q16)
# ===========================================================================

@register
class EmptyCatchBlockRule(RegexRule):
    rule_id = "JV-Q01"
    name = "Empty catch block"
    description = "Empty catch block silently swallows exceptions."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"catch\s*\([^)]*\)\s*\{\s*\}"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Log the exception or handle it explicitly."


@register
class SystemOutPrintRule(RegexRule):
    rule_id = "JV-Q02"
    name = "System.out.print in production"
    description = "System.out/err.print should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.JAVA]
    pattern = r"System\.(out|err)\.print"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use SLF4J/Log4j logger instead."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower() or "Test" in ctx.path.name:
            return []
        return super().check(ctx)


@register
class TooManyParametersRule(BaseRule):
    rule_id = "JV-Q03"
    name = "Too many parameters"
    description = "Method has more than 7 parameters."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]

    _METHOD = re.compile(
        r"(?:public|private|protected|static|\s)+\w+(?:<[^>]+>)?\s+\w+\s*\(([^)]*)\)\s*(?:throws|{)",
        re.DOTALL,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._METHOD.finditer(ctx.content):
            params = match.group(1).strip()
            if not params:
                continue
            count = len([p for p in params.split(",") if p.strip()])
            if count > 7:
                line = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line,
                    message=f"Method has {count} parameters (max 7). Use a parameter object.",
                    suggestion="Create a DTO/record to group related parameters.",
                ))
        return issues


@register
class NullPointerDereferenceRule(RegexRule):
    rule_id = "JV-Q04"
    name = "Potential null dereference"
    description = "Method call on potentially null return without null check."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"\.get\(\w+\)\.\w+|\.find\w*\(\)\.(?!isPresent|orElse|ifPresent)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Check for null or use Optional methods (orElse, ifPresent)."


@register
class RawTypeUsageRule(RegexRule):
    rule_id = "JV-Q05"
    name = "Raw type usage"
    description = "Raw type usage loses generic type safety."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.JAVA]
    pattern = r"\b(?:List|Map|Set|Collection|Iterator|Iterable)\s+\w+\s*[=;]"
    exclude_pattern = r"^\s*//|<"
    fix_suggestion = "Use generic types: List<String> instead of raw List."


@register
class SynchronizedOnStringRule(RegexRule):
    rule_id = "JV-Q06"
    name = "Synchronized on String"
    description = "Synchronizing on String literal can cause unexpected lock sharing."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"""synchronized\s*\(\s*["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use a private final Object as lock."


@register
class ResourceLeakRule(RegexRule):
    rule_id = "JV-Q07"
    name = "Resource leak"
    description = "Stream/connection opened without try-with-resources."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"new\s+(?:FileInputStream|FileOutputStream|BufferedReader|BufferedWriter|Socket|Connection)\s*\("
    exclude_pattern = r"^\s*//|try\s*\("
    fix_suggestion = "Use try-with-resources: try (var stream = new ...) { }."
    cwe_id = "CWE-404"


@register
class OptionalGetRule(RegexRule):
    rule_id = "JV-Q08"
    name = "Optional.get() without check"
    description = "Optional.get() throws NoSuchElementException if empty."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"\.get\s*\(\s*\)"
    exclude_pattern = r"^\s*//|isPresent|ifPresent|orElse|Map\.|List\.|Set\."
    fix_suggestion = "Use orElse(), orElseThrow(), or ifPresent()."


@register
class MutableStaticFieldRule(RegexRule):
    rule_id = "JV-Q09"
    name = "Mutable static field"
    description = "Non-final static field is thread-unsafe."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"static\s+(?!final\s)(?!.*\bfinal\b)\w+(?:<[^>]*>)?\s+\w+\s*="
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Make static fields final or protect with synchronization."


@register
class LongMethodRule(BaseRule):
    rule_id = "JV-Q10"
    name = "Long method"
    description = "Method exceeds 60 lines."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]

    _METHOD = re.compile(
        r"(?:public|private|protected|static|abstract|final|synchronized|\s)+"
        r"(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)\s*(?:throws\s+\w+(?:,\s*\w+)*)?\s*\{",
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
            start_line = ctx.content[:start_pos].count("\n") + 1
            end_line = ctx.content[:end_pos].count("\n") + 1
            length = end_line - start_line
            if length > 60:
                issues.append(self._make_issue(
                    ctx, line=start_line,
                    message=f"Method '{method_name}' is {length} lines (max 60).",
                    suggestion="Extract logic into helper methods.",
                ))
        return issues


@register
class CyclomaticComplexityRule(BaseRule):
    rule_id = "JV-Q11"
    name = "High cyclomatic complexity"
    description = "Method cyclomatic complexity exceeds 15."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]

    _METHOD = re.compile(
        r"(?:public|private|protected|static|\s)+\w+(?:<[^>]+>)?\s+(\w+)\s*\([^)]*\)\s*\{",
    )
    _TOKENS = re.compile(r"\b(if|else if|case|for|while|do|catch)\b|(\?\?)|(\?\.)|(&&)|(\|\|)")

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
            complexity = 1 + len(self._TOKENS.findall(body))
            if complexity > 15:
                line_num = ctx.content[:start_pos].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message=f"Method '{method_name}' has complexity {complexity} (max 15).",
                    suggestion="Simplify logic or extract into smaller methods.",
                ))
        return issues


@register
class GodClassRule(BaseRule):
    rule_id = "JV-Q12"
    name = "God class"
    description = "Class exceeds 500 lines."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]

    def check(self, ctx: FileContext) -> list[Issue]:
        if len(ctx.lines) > 500:
            return [self._make_issue(
                ctx, line=1,
                message=f"Class file is {len(ctx.lines)} lines (max 500). Consider splitting.",
                suggestion="Split into focused classes following SRP.",
            )]
        return []


@register
class NestedTernaryRule(RegexRule):
    rule_id = "JV-Q13"
    name = "Nested ternary"
    description = "Nested ternary operator reduces readability."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]
    pattern = r"\?.*\?.*:"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace nested ternaries with if/else for readability."


@register
class NamingConventionRule(RegexRule):
    rule_id = "JV-Q14"
    name = "Class naming convention"
    description = "Class name should start with uppercase."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]
    pattern = r"\b(?:class|interface)\s+[a-z]\w*"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use PascalCase for class/interface names."


@register
class TodoFixmeRule(RegexRule):
    rule_id = "JV-Q15"
    name = "TODO/FIXME in code"
    description = "TODO or FIXME indicates unfinished work."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]
    pattern = r"//\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Address or create a tracking issue."


@register
class AsyncVoidRule(RegexRule):
    rule_id = "JV-Q16"
    name = "Exception swallowing"
    description = "Catch block returns null/default, swallowing the exception."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"catch\s*\([^)]*\)\s*\{[^}]*return\s+(?:null|0|false|"")\s*;"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Log the exception before returning fallback."


# ===========================================================================
# PERFORMANCE (JV-P01 .. JV-P04)
# ===========================================================================

@register
class StringConcatInLoopRule(RegexRule):
    rule_id = "JV-P01"
    name = "String concat in loop"
    description = "String concatenation with + in loop creates O(n^2) behavior."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.JAVA]
    pattern = r"\+=\s*\"|\+\s*\w+\s*;"
    exclude_pattern = r"^\s*//|StringBuilder"
    fix_suggestion = "Use StringBuilder.append() inside loops."


@register
class LogConcatRule(RegexRule):
    rule_id = "JV-P02"
    name = "String concat in logging"
    description = "String concatenation in log call evaluates even when level is disabled."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.JAVA]
    pattern = r"(?:log|LOG|logger|LOGGER)\.\w+\s*\(\s*\"[^\"]*\"\s*\+"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use parameterized logging: log.info(\"msg {}\", var)."


@register
class RegexCompileInMethodRule(RegexRule):
    rule_id = "JV-P03"
    name = "Pattern.compile in method"
    description = "Compiling regex in method body is wasteful if called repeatedly."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.JAVA]
    pattern = r"Pattern\.compile\s*\("
    exclude_pattern = r"^\s*//|static\s+final|private\s+static"
    fix_suggestion = "Move Pattern.compile() to a static final field."


@register
class RuntimeExecRule(RegexRule):
    rule_id = "JV-P04"
    name = "Runtime.exec() usage"
    description = "Runtime.exec() or ProcessBuilder may create security and performance issues."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA]
    pattern = r"Runtime\.getRuntime\(\)\.exec\s*\(|new\s+ProcessBuilder\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Validate all inputs. Prefer ProcessBuilder with explicit argument list."
    cwe_id = "CWE-78"
