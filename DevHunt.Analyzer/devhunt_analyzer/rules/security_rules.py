from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ---------------------------------------------------------------------------
# SEC-01: SQL injection patterns
# ---------------------------------------------------------------------------
@register
class SqlInjectionRule(RegexRule):
    rule_id = "SEC-01"
    name = "SQL injection risk"
    description = "String concatenation/interpolation in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"""(?i)(?:SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|CREATE)\s+.*?"""
        r"""(?:\$\{|\$"|["']\s*\+\s*\w|\{\d+\})"""
    )
    exclude_pattern = r"^\s*(?://|#|\*|--)"
    fix_suggestion = "Use parameterized queries or ORM methods."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# SEC-02: Command injection
# ---------------------------------------------------------------------------
@register
class CommandInjectionRule(RegexRule):
    rule_id = "SEC-02"
    name = "Command injection risk"
    description = "User-controlled input in command execution creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"Process\.Start\s*\(|Runtime\.exec\s*\(|"
        r"child_process|exec\s*\(|execSync\s*\(|"
        r"spawn\s*\(|spawnSync\s*\("
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Avoid shell commands. If necessary, validate and sanitize all inputs."
    cwe_id = "CWE-78"


# ---------------------------------------------------------------------------
# SEC-03: Path traversal
# ---------------------------------------------------------------------------
@register
class PathTraversalRule(RegexRule):
    rule_id = "SEC-03"
    name = "Path traversal risk"
    description = "Potential path traversal via user-controlled path construction."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""(?:Path\.Combine|path\.join|path\.resolve)\s*\(.*?(?:request|req\.|params|query|body|input|args)"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Validate paths against a whitelist or use Path.GetFullPath() + check prefix."
    cwe_id = "CWE-22"


# ---------------------------------------------------------------------------
# SEC-04: Hardcoded credentials
# ---------------------------------------------------------------------------
@register
class HardcodedCredentialsRule(RegexRule):
    rule_id = "SEC-04"
    name = "Hardcoded credentials"
    description = "Possible hardcoded password, secret, API key, or token."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""(?i)(?:password|secret|api.?key|token|private.?key|client.?secret)\s*[:=]\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*(?://|#|\*)|\.example|\.sample|placeholder|TODO|FIXME|test|mock|fake"
    fix_suggestion = "Use environment variables, secrets manager, or config files excluded from VCS."
    cwe_id = "CWE-798"

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip config example files and test files
        name = ctx.path.name.lower()
        if any(s in name for s in [".example", ".sample", ".test.", ".spec.", "mock", "fake", "seed"]):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# SEC-05: Missing sanitization (XSS)
# ---------------------------------------------------------------------------
@register
class MissingSanitizationRule(BaseRule):
    rule_id = "SEC-05"
    name = "Missing HTML sanitization"
    description = "Rich-text field assigned without sanitization creates XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-79"

    _CS_RICH_TEXT = re.compile(r"(?:Description|Bio|Content|HtmlContent|Body)\s*=\s*(?!null)")
    _TS_DANGEROUS = re.compile(r"dangerouslySetInnerHTML")
    _SANITIZE = re.compile(r"SanitizeHtml|DOMPurify|sanitize")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []

        if ctx.language == Language.CSHARP:
            for i, line in enumerate(ctx.lines):
                if line.strip().startswith("//"):
                    continue
                if self._CS_RICH_TEXT.search(line):
                    start = max(0, i - 5)
                    end = min(len(ctx.lines), i + 6)
                    window = "\n".join(ctx.lines[start:end])
                    if not self._SANITIZE.search(window):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="Rich-text field assigned without SanitizeHtml().",
                            suggestion="Apply SecurityHelpers.SanitizeHtml() before assignment.",
                        ))

        elif ctx.language == Language.TYPESCRIPT:
            for i, line in enumerate(ctx.lines):
                if self._TS_DANGEROUS.search(line):
                    start = max(0, i - 3)
                    end = min(len(ctx.lines), i + 4)
                    window = "\n".join(ctx.lines[start:end])
                    if not self._SANITIZE.search(window):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="dangerouslySetInnerHTML without sanitization.",
                            suggestion="Sanitize HTML with DOMPurify before rendering.",
                        ))

        return issues


# ---------------------------------------------------------------------------
# SEC-06: Insecure crypto (MD5, SHA1 for hashing)
# ---------------------------------------------------------------------------
@register
class InsecureCryptoRule(RegexRule):
    rule_id = "SEC-06"
    name = "Insecure cryptography"
    description = "MD5/SHA1 are cryptographically broken. Use SHA-256+ or bcrypt for passwords."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"MD5\.Create|SHA1\.Create|MD5CryptoServiceProvider|SHA1CryptoServiceProvider|"
        r"""crypto\.createHash\s*\(\s*["'](?:md5|sha1)["']\)"""
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use SHA-256, SHA-512, or bcrypt/Argon2 for password hashing."
    cwe_id = "CWE-328"


# ---------------------------------------------------------------------------
# SEC-07: SSRF (Server-Side Request Forgery)
# ---------------------------------------------------------------------------
@register
class SsrfRule(BaseRule):
    rule_id = "SEC-07"
    name = "SSRF risk"
    description = "User-controlled URL in server-side HTTP request creates SSRF risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-918"

    _HTTP_CALL = re.compile(
        r"(?:HttpClient|_httpClient|_client|httpClient)\s*\.\s*(?:Get|Post|Put|Delete|Send)Async\s*\(|"
        r"fetch\s*\(|axios\s*\.\s*(?:get|post|put|delete)\s*\("
    )
    _USER_INPUT = re.compile(r"(?:request|req\.|params|query|body|input|args|dto)\.", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._HTTP_CALL.search(line) and self._USER_INPUT.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="User-controlled input in HTTP request — potential SSRF.",
                    suggestion="Validate URLs against an allowlist of trusted domains.",
                ))
        return issues


# ---------------------------------------------------------------------------
# SEC-08: Open redirect
# ---------------------------------------------------------------------------
@register
class OpenRedirectRule(RegexRule):
    rule_id = "SEC-08"
    name = "Open redirect risk"
    description = "User-controlled value in redirect creates open redirect risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:Redirect|RedirectToAction|redirect)\s*\(.*?(?:request|req\.|params|query|returnUrl|url)|"
        r"window\.location\s*=\s*(?!['\"]/)"
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Validate redirect URLs against an allowlist or use Url.IsLocalUrl()."
    cwe_id = "CWE-601"


# ---------------------------------------------------------------------------
# SEC-09: Missing authorization on mutating endpoints
# ---------------------------------------------------------------------------
@register
class MissingAuthorizationRule(BaseRule):
    rule_id = "SEC-09"
    name = "Missing authorization"
    description = "Mutating endpoint without [Authorize] attribute."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-862"

    _HTTP_MUTATING = re.compile(r"\[(HttpPost|HttpPut|HttpDelete|HttpPatch)\]")
    _AUTH = re.compile(r"\[Authorize|AllowAnonymous\]")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Check if class has [Authorize]
        has_class_auth = bool(re.search(r"\[Authorize\]\s*\n\s*(?:public\s+)?class", ctx.content))

        if has_class_auth:
            return issues

        for i, line in enumerate(ctx.lines):
            if self._HTTP_MUTATING.search(line):
                # Check 5 lines above for [Authorize]
                start = max(0, i - 5)
                window = "\n".join(ctx.lines[start:i + 1])
                if not self._AUTH.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Mutating HTTP endpoint without [Authorize] attribute.",
                        suggestion="Add [Authorize] to the method or controller class.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SEC-10: Sensitive data in logs
# ---------------------------------------------------------------------------
@register
class SensitiveDataInLogsRule(RegexRule):
    rule_id = "SEC-10"
    name = "Sensitive data in logs"
    description = "Logging potentially sensitive data (password, token, secret)."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""(?i)(?:_?logger\.\w+|Log\.\w+|console\.\w+)\s*\(.*?(?:password|token|secret|credential|apikey|private.?key)"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Never log sensitive data. Redact or mask before logging."
    cwe_id = "CWE-532"


# ---------------------------------------------------------------------------
# SEC-11: JWT none algorithm
# ---------------------------------------------------------------------------
@register
class JwtNoneAlgorithmRule(RegexRule):
    rule_id = "SEC-11"
    name = "JWT 'none' algorithm"
    description = "JWT with 'none' algorithm allows token forgery."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""(?i)[\"'](?:alg|algorithm)[\"']\s*[:=]\s*[\"']none[\"']"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Never allow 'none' algorithm. Use HS256/RS256 at minimum."
    cwe_id = "CWE-347"


# ---------------------------------------------------------------------------
# SEC-12: Weak TLS/SSL
# ---------------------------------------------------------------------------
@register
class WeakTlsRule(RegexRule):
    rule_id = "SEC-12"
    name = "Weak TLS/SSL protocol"
    description = "TLS 1.0/1.1 or SSL 3.0 are insecure. Use TLS 1.2+."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"(?i)(?:Ssl3|Tls(?:11)?(?:\b|[^2])|TLSv1(?:\.0|\.1)?(?:[\"']|\b))|SecurityProtocolType\.(?:Ssl3|Tls(?:11)?)\b"
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use TLS 1.2 or TLS 1.3 only. Set SecurityProtocolType.Tls12 | Tls13."
    cwe_id = "CWE-326"


# ---------------------------------------------------------------------------
# SEC-13: Unvalidated file upload
# ---------------------------------------------------------------------------
@register
class UnvalidatedFileUploadRule(BaseRule):
    rule_id = "SEC-13"
    name = "Unvalidated file upload"
    description = "File upload without content-type or extension validation."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-434"

    _UPLOAD = re.compile(
        r"(?:IFormFile|multipart|upload|multer|formidable|busboy|"
        r"FileUpload|UploadedFile|request\.files|req\.file)\b",
        re.IGNORECASE,
    )
    _VALIDATION = re.compile(
        r"(?:ContentType|content.?type|mime.?type|extension|allowedExtensions|"
        r"file\.type|accept|validateFile|fileFilter)",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith(("//", "#", "*")):
                continue
            if self._UPLOAD.search(line):
                start = max(0, i - 10)
                end = min(len(ctx.lines), i + 15)
                window = "\n".join(ctx.lines[start:end])
                if not self._VALIDATION.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="File upload without content-type/extension validation.",
                        suggestion="Validate file extension and content-type against an allowlist.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SEC-14: CORS wildcard (Access-Control-Allow-Origin: *)
# ---------------------------------------------------------------------------
@register
class CorsWildcardRule(RegexRule):
    rule_id = "SEC-14"
    name = "CORS wildcard origin"
    description = "Access-Control-Allow-Origin: * allows any origin — security risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""(?:AllowAnyOrigin|Access-Control-Allow-Origin[\"':\s]*\*|cors\(\s*\)|origins?\s*[:=]\s*[\"']\*)"""
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Restrict CORS to specific trusted origins instead of wildcard."
    cwe_id = "CWE-942"


# ---------------------------------------------------------------------------
# SEC-15: HTTP without TLS (insecure endpoint)
# ---------------------------------------------------------------------------
@register
class InsecureHttpRule(RegexRule):
    rule_id = "SEC-15"
    name = "Insecure HTTP URL"
    description = "HTTP URL without TLS exposes data in transit."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""[\"']http://(?!localhost|127\.0\.0\.1|0\.0\.0\.0|::1|10\.|172\.(?:1[6-9]|2\d|3[01])\.|192\.168\.)[\w.-]+"""
    exclude_pattern = r"^\s*(?://|#|\*)|test|mock|example\.com|placeholder"
    fix_suggestion = "Use HTTPS instead of HTTP for all non-local URLs."
    cwe_id = "CWE-319"


# ---------------------------------------------------------------------------
# SEC-16: Disabled certificate validation
# ---------------------------------------------------------------------------
@register
class DisabledCertValidationRule(RegexRule):
    rule_id = "SEC-16"
    name = "Disabled certificate validation"
    description = "Certificate validation disabled — vulnerable to MITM attacks."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:ServerCertificateValidation(?:Callback)?\s*(?:=|\+=)\s*.*?(?:true|=>)|"
        r"rejectUnauthorized\s*:\s*false|"
        r"verify\s*=\s*False|"
        r"InsecureSkipVerify\s*:\s*true|"
        r"CURLOPT_SSL_VERIFYPEER\s*,\s*(?:0|false)|"
        r"NODE_TLS_REJECT_UNAUTHORIZED\s*=\s*[\"']0[\"'])"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|test|dev|local"
    fix_suggestion = "Never disable certificate validation in production. Use proper CA certificates."
    cwe_id = "CWE-295"


# ---------------------------------------------------------------------------
# SEC-17: Mass assignment / over-posting
# ---------------------------------------------------------------------------
@register
class MassAssignmentRule(RegexRule):
    rule_id = "SEC-17"
    name = "Mass assignment risk"
    description = "Binding directly to entity model allows over-posting attacks."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:\[FromBody\]\s*\w+Entity|\[FromBody\]\s*\w+Model\b|"
        r"Object\.assign\s*\(\s*\w+\s*,\s*req\.body|"
        r"\.update\s*\(\s*req\.body\s*\)|"
        r"attr_accessible\s+:all)"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|Dto|DTO|Request|Command|ViewModel"
    fix_suggestion = "Use DTOs/ViewModels instead of binding directly to entity models."
    cwe_id = "CWE-915"


# ---------------------------------------------------------------------------
# SEC-18: Hardcoded IP address
# ---------------------------------------------------------------------------
@register
class HardcodedIpRule(RegexRule):
    rule_id = "SEC-18"
    name = "Hardcoded IP address"
    description = "Hardcoded IP address reduces portability and may expose infrastructure."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"""[\"']\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(?::\d+)?[\"']"""
    exclude_pattern = r"^\s*(?://|#|\*)|127\.0\.0\.1|0\.0\.0\.0|localhost|test|mock|example|192\.168\.|10\.|172\."
    fix_suggestion = "Use environment variables or configuration files for IP addresses."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# SEC-19: XML bomb (billion laughs)
# ---------------------------------------------------------------------------
@register
class XmlBombRule(RegexRule):
    rule_id = "SEC-19"
    name = "XML bomb risk"
    description = "XML parsing without entity expansion limits — XML bomb (billion laughs) risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"(?:XmlReader\.Create|parseString|etree\.parse|SAXParser|DOMParser)\s*\("
    exclude_pattern = r"^\s*(?://|#|\*)|DtdProcessing\.Prohibit|resolve_entities\s*=\s*False"
    fix_suggestion = "Disable DTD processing and limit entity expansion."
    cwe_id = "CWE-776"


# ---------------------------------------------------------------------------
# SEC-20: Exposed stack trace / debug info
# ---------------------------------------------------------------------------
@register
class ExposedStackTraceRule(RegexRule):
    rule_id = "SEC-20"
    name = "Exposed stack trace"
    description = "Stack trace or debug information exposed to users."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:\.StackTrace|\.ToString\(\)\s*\)|"
        r"traceback\.format_exc|"
        r"e\.printStackTrace\(\)|"
        r"500.*?(?:stack|trace|debug))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|_?logger\.|Log\.|console\.error|log\."
    fix_suggestion = "Log stack traces server-side only. Return generic error messages to users."
    cwe_id = "CWE-209"

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only flag in controller/handler/route files
        path_lower = str(ctx.path).lower()
        if not any(k in path_lower for k in ["controller", "handler", "route", "endpoint", "api", "page"]):
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# SEC-21: Insecure randomness
# ---------------------------------------------------------------------------
@register
class InsecureRandomRule(RegexRule):
    rule_id = "SEC-21"
    name = "Insecure randomness"
    description = "Using non-cryptographic RNG for security-sensitive operations."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:new\s+Random\s*\(|Math\.random\s*\(|"
        r"random\.random\s*\(|random\.randint\s*\(|"
        r"rand\s*\(|srand\s*\(|"
        r"java\.util\.Random\b)"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|test|mock|seed|shuffle|sample"
    fix_suggestion = "Use cryptographic RNG: RandomNumberGenerator (C#), crypto.getRandomValues (JS), secrets (Python)."
    cwe_id = "CWE-338"


# ---------------------------------------------------------------------------
# SEC-22: Timing attack (string comparison for secrets)
# ---------------------------------------------------------------------------
@register
class TimingAttackRule(BaseRule):
    rule_id = "SEC-22"
    name = "Timing attack risk"
    description = "Direct string comparison for secrets/tokens is vulnerable to timing attacks."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-208"

    _SECRET_COMPARE = re.compile(
        r"(?i)(?:token|secret|apikey|password|hash|signature|hmac|digest)\s*"
        r"(?:==|!=|\.equals\s*\(|===)"
    )
    _SAFE_COMPARE = re.compile(
        r"(?i)(?:FixedTimeEquals|timingSafeEqual|timing.safe|constant.time|hmac\.compare|"
        r"secure_compare|Rack::Utils\.secure_compare|MessageDigest\.isEqual)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith(("//", "#", "*")):
                continue
            if self._SECRET_COMPARE.search(line):
                start = max(0, i - 5)
                end = min(len(ctx.lines), i + 6)
                window = "\n".join(ctx.lines[start:end])
                if not self._SAFE_COMPARE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Direct string comparison for secrets — timing attack risk.",
                        suggestion="Use constant-time comparison (CryptographicOperations.FixedTimeEquals, crypto.timingSafeEqual).",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SEC-23: Race condition in file operations
# ---------------------------------------------------------------------------
@register
class FileRaceConditionRule(BaseRule):
    rule_id = "SEC-23"
    name = "File race condition"
    description = "Check-then-use on file system is a TOCTOU race condition."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-367"

    _CHECK = re.compile(r"(?:File\.Exists|os\.path\.exists|fs\.existsSync|Path\.exists|access\s*\()")
    _USE = re.compile(r"(?:File\.(Read|Write|Open|Delete|Move)|open\s*\(|fs\.(read|write|unlink)|os\.(remove|rename))")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith(("//", "#", "*")):
                continue
            if self._CHECK.search(line):
                window = ctx.lines[i + 1:min(len(ctx.lines), i + 8)]
                for j, next_line in enumerate(window):
                    if self._USE.search(next_line):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="File existence check followed by file operation — TOCTOU race condition.",
                            suggestion="Use try/catch instead of check-then-use. Open file directly and handle exceptions.",
                        ))
                        break
        return issues


# ---------------------------------------------------------------------------
# SEC-24: Unencrypted sensitive data at rest
# ---------------------------------------------------------------------------
@register
class UnencryptedStorageRule(RegexRule):
    rule_id = "SEC-24"
    name = "Unencrypted sensitive storage"
    description = "Sensitive data written to file/DB without encryption."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?i)(?:write|save|store|persist|insert).*?"
        r"(?:password|ssn|social.?security|credit.?card|cvv|"
        r"bank.?account|private.?key)"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|encrypt|hash|bcrypt|argon|scrypt|protect"
    fix_suggestion = "Encrypt sensitive data before storage. Use field-level encryption or hashing."
    cwe_id = "CWE-312"


# ---------------------------------------------------------------------------
# SEC-25: NoSQL injection
# ---------------------------------------------------------------------------
@register
class NoSqlInjectionRule(RegexRule):
    rule_id = "SEC-25"
    name = "NoSQL injection risk"
    description = "User input in NoSQL query without sanitization."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:\$where\s*:|"
        r"\.find\s*\(\s*\{.*?(?:req\.|request\.|params\.|body\.|input\.)|"
        r"\.aggregate\s*\(\s*\[.*?(?:req\.|request\.|params\.))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Sanitize user input. Use parameterized queries and avoid $where."
    cwe_id = "CWE-943"


# ---------------------------------------------------------------------------
# SEC-26: Server-side template injection (SSTI)
# ---------------------------------------------------------------------------
@register
class SstiRule(RegexRule):
    rule_id = "SEC-26"
    name = "Template injection risk"
    description = "User input in template rendering creates SSTI risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:render_template_string\s*\(.*?(?:request|input|user)|"
        r"Template\s*\(.*?(?:request|input|user)|"
        r"Razor\.Parse\s*\(.*?(?:request|input|user)|"
        r"new\s+Function\s*\(.*?(?:req|input|user))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Never pass user input directly to template engines. Use parameterized templates."
    cwe_id = "CWE-1336"


# ---------------------------------------------------------------------------
# SEC-27: Prototype pollution (JS/TS)
# ---------------------------------------------------------------------------
@register
class PrototypePollutionRule(RegexRule):
    rule_id = "SEC-27"
    name = "Prototype pollution risk"
    description = "Deep merge/clone with user input can cause prototype pollution."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = (
        r"(?:Object\.assign\s*\(\s*\{\}\s*,.*?(?:req\.|body|params|input)|"
        r"(?:lodash\.)?merge\s*\(.*?(?:req\.|body|params|input)|"
        r"\[__proto__\]|\[constructor\]|\[prototype\])"
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Use Object.create(null) as base, or use structured clone. Validate keys against __proto__."
    cwe_id = "CWE-1321"


# ---------------------------------------------------------------------------
# SEC-28: Deserialization of untrusted data (any language)
# ---------------------------------------------------------------------------
@register
class GenericDeserializationRule(RegexRule):
    rule_id = "SEC-28"
    name = "Unsafe deserialization"
    description = "Deserializing untrusted data can lead to RCE."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:pickle\.loads?\s*\(|yaml\.(?:load|unsafe_load)\s*\((?!.*Loader=yaml\.SafeLoader)|"
        r"Marshal\.load\s*\(|"
        r"unserialize\s*\(|"
        r"ObjectInputStream|"
        r"readObject\s*\(\s*\))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|SafeLoader|safe_load"
    fix_suggestion = "Use safe deserialization: yaml.safe_load, JSON, or schema-validated formats."
    cwe_id = "CWE-502"


# ---------------------------------------------------------------------------
# SEC-29: Privilege escalation via role manipulation
# ---------------------------------------------------------------------------
@register
class RoleManipulationRule(RegexRule):
    rule_id = "SEC-29"
    name = "Role manipulation risk"
    description = "User-supplied role/permission in assignment — privilege escalation risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?i)(?:role|permission|is_?admin|is_?superuser|is_?staff)\s*=\s*"
        r"(?:req(?:uest)?\.(?:body|params|query)|input|dto|model)\."
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Never accept role/permission from user input. Set roles server-side based on authorization."
    cwe_id = "CWE-269"


# ---------------------------------------------------------------------------
# SEC-30: Clickjacking (missing X-Frame-Options)
# ---------------------------------------------------------------------------
@register
class ClickjackingRule(BaseRule):
    rule_id = "SEC-30"
    name = "Clickjacking risk"
    description = "Missing X-Frame-Options or CSP frame-ancestors header."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.ANY]

    _FRAME_OPTIONS = re.compile(r"(?i)(?:X-Frame-Options|frame-ancestors)")
    _HELMET = re.compile(r"(?:helmet|frameguard|AddAntiforgery)")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only check in startup/config files
        name = ctx.path.name.lower()
        if not any(k in name for k in ["startup", "program", "app.", "server.", "middleware", "config"]):
            return []
        if self._FRAME_OPTIONS.search(ctx.content) or self._HELMET.search(ctx.content):
            return []
        return []  # Too noisy for general use — skip unless in startup files


# ---------------------------------------------------------------------------
# SEC-31: Environment variable injection
# ---------------------------------------------------------------------------
@register
class EnvVarInjectionRule(RegexRule):
    rule_id = "SEC-31"
    name = "Environment variable injection"
    description = "User input in environment variable name or Process.Start — injection risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:Environment\.SetEnvironmentVariable\s*\(.*?(?:req|input|user|dto)|"
        r"os\.environ\[.*?(?:request|input|user)|"
        r"process\.env\[.*?(?:req|input|user))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)"
    fix_suggestion = "Never use user input as environment variable names. Validate against an allowlist."
    cwe_id = "CWE-78"


# ---------------------------------------------------------------------------
# SEC-32: Missing rate limiting
# ---------------------------------------------------------------------------
@register
class MissingRateLimitRule(BaseRule):
    rule_id = "SEC-32"
    name = "Missing rate limiting"
    description = "Auth/login endpoint without rate limiting."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-307"

    _AUTH_ENDPOINT = re.compile(
        r"(?i)(?:login|signin|sign.in|authenticate|register|signup|sign.up|"
        r"forgot.?password|reset.?password|verify|otp|token)"
    )
    _RATE_LIMIT = re.compile(
        r"(?i)(?:RateLimit|Throttle|rate.?limit|throttle|EnableRateLimiting|"
        r"rateLimit|express-rate-limit|@Throttle)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if not any(k in name for k in ["auth", "login", "controller", "route", "handler"]):
            return []
        if not self._AUTH_ENDPOINT.search(ctx.content):
            return []
        if self._RATE_LIMIT.search(ctx.content):
            return []
        return [self._make_issue(
            ctx, line=1,
            message="Auth endpoint without rate limiting — brute force risk.",
            suggestion="Add rate limiting (e.g., [EnableRateLimiting], express-rate-limit, @Throttle).",
        )]


# ---------------------------------------------------------------------------
# SEC-33: Information disclosure via error messages
# ---------------------------------------------------------------------------
@register
class InfoDisclosureRule(RegexRule):
    rule_id = "SEC-33"
    name = "Information disclosure"
    description = "Detailed error message returned to client exposes internals."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?i)(?:return\s+(?:Ok|Json|BadRequest|StatusCode)\s*\(.*?"
        r"(?:ex\.Message|exception\.Message|err\.message|error\.stack|"
        r"e\.getMessage|traceback))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|development|debug|isDev"
    fix_suggestion = "Return generic error messages. Log details server-side only."
    cwe_id = "CWE-209"


# ---------------------------------------------------------------------------
# SEC-34: Regex injection
# ---------------------------------------------------------------------------
@register
class RegexInjectionRule(RegexRule):
    rule_id = "SEC-34"
    name = "Regex injection"
    description = "User input used in regex pattern — ReDoS and injection risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?:new\s+Regex\s*\(.*?(?:req|input|user|dto|param)|"
        r"re\.compile\s*\(.*?(?:request|input|user)|"
        r"RegExp\s*\(.*?(?:req|input|user|param))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|Escape|escape|sanitize"
    fix_suggestion = "Escape user input with Regex.Escape / re.escape before using in patterns."
    cwe_id = "CWE-1333"


# ---------------------------------------------------------------------------
# SEC-35: Exposed .env / config files
# ---------------------------------------------------------------------------
@register
class ExposedEnvFileRule(BaseRule):
    rule_id = "SEC-35"
    name = "Exposed .env file"
    description = ".env file contains secrets and should not be committed or served."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-538"

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if name in (".env", ".env.local", ".env.production", ".env.staging"):
            # Check if it actually has secrets
            if re.search(r"(?i)(password|secret|api.?key|token|private.?key)\s*=\s*\S+", ctx.content):
                return [self._make_issue(
                    ctx, line=1,
                    message=f"'{ctx.path.name}' contains secrets and should not be in the repository.",
                    suggestion="Add to .gitignore. Use .env.example with placeholder values.",
                )]
        return []


# ---------------------------------------------------------------------------
# SEC-36: Missing Content Security Policy
# ---------------------------------------------------------------------------
@register
class MissingCspRule(BaseRule):
    rule_id = "SEC-36"
    name = "Missing CSP header"
    description = "No Content-Security-Policy header configured — XSS mitigation missing."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-693"

    _CSP = re.compile(r"(?i)(?:Content-Security-Policy|contentSecurityPolicy|csp)")

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if not any(k in name for k in ["startup", "program.cs", "server.", "app.", "next.config", "middleware"]):
            return []
        if self._CSP.search(ctx.content):
            return []
        return [self._make_issue(
            ctx, line=1,
            message="No Content-Security-Policy header configured.",
            suggestion="Add CSP header to prevent XSS. Use helmet (Node.js) or middleware (ASP.NET).",
        )]


# ---------------------------------------------------------------------------
# SEC-37: Unsafe innerHTML / DOM manipulation
# ---------------------------------------------------------------------------
@register
class UnsafeInnerHtmlRule(RegexRule):
    rule_id = "SEC-37"
    name = "Unsafe innerHTML"
    description = "Direct innerHTML assignment — XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"\.innerHTML\s*="
    exclude_pattern = r"^\s*(?://|#|\*)|DOMPurify|sanitize|textContent"
    fix_suggestion = "Use textContent for plain text, or DOMPurify.sanitize() for HTML."
    cwe_id = "CWE-79"


# ---------------------------------------------------------------------------
# SEC-38: GraphQL introspection enabled
# ---------------------------------------------------------------------------
@register
class GraphqlIntrospectionRule(RegexRule):
    rule_id = "SEC-38"
    name = "GraphQL introspection enabled"
    description = "GraphQL introspection should be disabled in production."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = r"(?i)introspection\s*[:=]\s*true"
    exclude_pattern = r"^\s*(?://|#|\*)|development|dev|test"
    fix_suggestion = "Disable introspection in production: introspection: false."
    cwe_id = "CWE-200"


# ---------------------------------------------------------------------------
# SEC-39: Missing HSTS (HTTP Strict Transport Security)
# ---------------------------------------------------------------------------
@register
class MissingHstsRule(BaseRule):
    rule_id = "SEC-39"
    name = "Missing HSTS"
    description = "No HSTS header — users can be downgraded to HTTP."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.ANY]
    cwe_id = "CWE-319"

    _HSTS = re.compile(r"(?i)(?:Strict-Transport-Security|UseHsts|hsts)")

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if not any(k in name for k in ["startup", "program.cs", "server.", "app.", "middleware"]):
            return []
        if self._HSTS.search(ctx.content):
            return []
        return [self._make_issue(
            ctx, line=1,
            message="No HSTS header configured.",
            suggestion="Add app.UseHsts() (ASP.NET) or Strict-Transport-Security header.",
        )]


# ---------------------------------------------------------------------------
# SEC-40: Exposed debug mode in production
# ---------------------------------------------------------------------------
@register
class DebugModeRule(RegexRule):
    rule_id = "SEC-40"
    name = "Debug mode enabled"
    description = "Debug mode enabled in production config — information disclosure risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.ANY]
    pattern = (
        r"(?i)(?:DEBUG\s*[:=]\s*[Tt]rue|"
        r"app\.UseDeveloperExceptionPage|"
        r"FLASK_DEBUG\s*=\s*1|"
        r"debug\s*:\s*true|"
        r"EnableDetailedErrors\s*\(\s*true\s*\))"
    )
    exclude_pattern = r"^\s*(?://|#|\*)|development|\.dev\.|appsettings\.Development|test"
    fix_suggestion = "Disable debug mode in production. Use environment-specific configuration."
    cwe_id = "CWE-215"
