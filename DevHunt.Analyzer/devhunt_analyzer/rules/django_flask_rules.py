from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  Django / Flask rules (DF-*)
#  Universal rules for any Django or Flask project.
# ===========================================================================


# ---------------------------------------------------------------------------
# DF-S01: Raw SQL in Django (SQL injection)
# ---------------------------------------------------------------------------
@register
class DfRawSqlRule(RegexRule):
    rule_id = "DF-S01"
    name = "Django raw SQL injection"
    description = "Using raw() or cursor.execute() with string formatting — SQL injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""(?:\.raw|cursor\.execute)\s*\(\s*(?:f["']|["'].*%s.*["']\s*%|.*\.format\()"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use parameterized queries: Model.objects.raw('SELECT ... WHERE id=%s', [id])."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# DF-S02: DEBUG = True in non-dev settings
# ---------------------------------------------------------------------------
@register
class DfDebugTrueRule(BaseRule):
    rule_id = "DF-S02"
    name = "DEBUG = True in production settings"
    description = "DEBUG=True in settings leaks stack traces and internal info."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    cwe_id = "CWE-215"

    _DEBUG = re.compile(r"^\s*DEBUG\s*=\s*True\b")

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if "settings" not in name:
            return []
        # Skip *_dev.py / *_local.py / development.py
        if any(s in name for s in ["dev", "local", "test", "development"]):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._DEBUG.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="DEBUG = True in production settings file.",
                    suggestion="Set DEBUG = os.environ.get('DEBUG', 'False') == 'True'.",
                ))
        return issues


# ---------------------------------------------------------------------------
# DF-S03: Secret key hardcoded
# ---------------------------------------------------------------------------
@register
class DfHardcodedSecretRule(RegexRule):
    rule_id = "DF-S03"
    name = "Hardcoded SECRET_KEY"
    description = "SECRET_KEY/secret_key hardcoded in source — must come from env."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""(?:SECRET_KEY|secret_key|app\.secret_key)\s*=\s*["'][^"']{8,}["']"""
    exclude_pattern = r"^\s*#|\.example|os\.environ|config\["
    fix_suggestion = "Use os.environ['SECRET_KEY'] or a .env file."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# DF-S04: CSRF disabled in Flask
# ---------------------------------------------------------------------------
@register
class FlaskCsrfDisabledRule(RegexRule):
    rule_id = "DF-S04"
    name = "Flask CSRF disabled"
    description = "CSRF protection explicitly disabled — vulnerable to cross-site request forgery."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""WTF_CSRF_ENABLED\s*=\s*False|CSRFProtect.*disable|csrf\.exempt"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Enable CSRF protection with Flask-WTF CSRFProtect."
    cwe_id = "CWE-352"


# ---------------------------------------------------------------------------
# DF-S05: @csrf_exempt in Django
# ---------------------------------------------------------------------------
@register
class DjangoCsrfExemptRule(RegexRule):
    rule_id = "DF-S05"
    name = "Django CSRF exempt"
    description = "@csrf_exempt disables CSRF protection for this view."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"@csrf_exempt"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Remove @csrf_exempt and use proper CSRF tokens in forms/AJAX."
    cwe_id = "CWE-352"


# ---------------------------------------------------------------------------
# DF-S06: ALLOWED_HOSTS = ['*']
# ---------------------------------------------------------------------------
@register
class DfAllowedHostsStarRule(RegexRule):
    rule_id = "DF-S06"
    name = "ALLOWED_HOSTS wildcard"
    description = "ALLOWED_HOSTS = ['*'] allows any host — Host header injection risk."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""ALLOWED_HOSTS\s*=\s*\[\s*["']\*["']\s*\]"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Set ALLOWED_HOSTS to specific domain(s)."
    cwe_id = "CWE-644"


# ---------------------------------------------------------------------------
# DF-S07: Unsafe deserialization (pickle)
# ---------------------------------------------------------------------------
@register
class UnsafeDeserializationRule(RegexRule):
    rule_id = "DF-S07"
    name = "Unsafe deserialization"
    description = "pickle.loads on untrusted data — arbitrary code execution."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"pickle\.loads?\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use JSON or a safe serialization format for untrusted data."
    cwe_id = "CWE-502"


# ---------------------------------------------------------------------------
# DF-S08: XSS via mark_safe / |safe
# ---------------------------------------------------------------------------
@register
class MarkSafeXssRule(RegexRule):
    rule_id = "DF-S08"
    name = "XSS via mark_safe"
    description = "mark_safe() or Markup() on user-controlled input — XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"(?:mark_safe|Markup)\s*\(\s*(?:f[\"']|.*\.format|.*%\s*\(|request\.|.*\+)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Sanitize input before mark_safe() or use template auto-escaping."
    cwe_id = "CWE-79"


# ---------------------------------------------------------------------------
# DF-S09: Flask debug mode in production
# ---------------------------------------------------------------------------
@register
class FlaskDebugModeRule(RegexRule):
    rule_id = "DF-S09"
    name = "Flask debug mode"
    description = "app.run(debug=True) in production — exposes debugger console."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"app\.run\s*\([^)]*debug\s*=\s*True"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use environment variable: app.run(debug=os.environ.get('FLASK_DEBUG'))."
    cwe_id = "CWE-215"


# ---------------------------------------------------------------------------
# DF-S10: Open redirect
# ---------------------------------------------------------------------------
@register
class OpenRedirectRule(RegexRule):
    rule_id = "DF-S10"
    name = "Open redirect"
    description = "Redirect using unvalidated user input — open redirect vulnerability."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"(?:redirect|HttpResponseRedirect)\s*\(\s*request\.(?:GET|POST|args|form)\s*[.\[]"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Validate redirect URL against allowlist with url_has_allowed_host_and_scheme()."
    cwe_id = "CWE-601"


# ---------------------------------------------------------------------------
# DF-Q01: N+1 queries in Django (no select_related/prefetch_related)
# ---------------------------------------------------------------------------
@register
class DfNPlusOneRule(BaseRule):
    rule_id = "DF-Q01"
    name = "Django N+1 query pattern"
    description = "Accessing related objects in loop without select_related/prefetch_related."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]

    _LOOP = re.compile(r"for\s+\w+\s+in\s+\w+")
    _RELATED_ACCESS = re.compile(r"\.\w+(?:_set|_id)\.|\.\w+\.objects\.")
    _PREFETCH = re.compile(r"(?:select_related|prefetch_related)\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        if self._PREFETCH.search(ctx.content):
            return []
        issues: list[Issue] = []
        in_loop = False
        for i, line in enumerate(ctx.lines):
            if self._LOOP.search(line):
                in_loop = True
            elif in_loop and line.strip() and not line[0].isspace():
                in_loop = False
            if in_loop and self._RELATED_ACCESS.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Related object accessed in loop without select_related/prefetch_related.",
                    suggestion="Add .select_related() or .prefetch_related() to the queryset.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# DF-Q02: Missing authentication decorator
# ---------------------------------------------------------------------------
@register
class MissingAuthDecoratorRule(BaseRule):
    rule_id = "DF-Q02"
    name = "Missing authentication decorator"
    description = "View function without @login_required or authentication class."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]

    _VIEW_FUNC = re.compile(r"^def\s+(get|post|put|patch|delete|list|create|update|destroy)\s*\(", re.MULTILINE)
    _AUTH_DECORATORS = re.compile(
        r"@(?:login_required|permission_required|staff_member_required|"
        r"api_view|action|user_passes_test)"
    )
    _AUTH_CLASSES = re.compile(
        r"(?:authentication_classes|permission_classes|IsAuthenticated)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if self._AUTH_CLASSES.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            match = self._VIEW_FUNC.search(line)
            if match:
                # Check decorators above
                start = max(0, i - 5)
                window = "\n".join(ctx.lines[start:i])
                if not self._AUTH_DECORATORS.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message=f"View '{match.group(1)}' without authentication decorator.",
                        suggestion="Add @login_required or set permission_classes.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# DF-Q03: Model without __str__ method
# ---------------------------------------------------------------------------
@register
class ModelMissingStrRule(BaseRule):
    rule_id = "DF-Q03"
    name = "Model without __str__"
    description = "Django model without __str__ — admin and debugging show unhelpful repr."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _MODEL_CLASS = re.compile(r"class\s+(\w+)\s*\(\s*(?:models\.Model|AbstractBaseUser|AbstractUser)")
    _STR_METHOD = re.compile(r"def\s+__str__\s*\(\s*self\s*\)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_model = False
        model_name = ""
        model_line = 0
        depth = 0

        for i, line in enumerate(ctx.lines):
            match = self._MODEL_CLASS.search(line)
            if match:
                in_model = True
                model_name = match.group(1)
                model_line = i + 1
                depth = 0
                continue

            if in_model:
                depth += line.count("class ") if re.match(r"\S", line) and i != model_line - 1 else 0
                if self._STR_METHOD.search(line):
                    in_model = False
                    continue
                # Detect end of class (next class definition or non-indented line)
                if re.match(r"^(?:class |def |$)", line) and i > model_line:
                    issues.append(self._make_issue(
                        ctx, line=model_line,
                        message=f"Model '{model_name}' without __str__ method.",
                        suggestion="Add def __str__(self): return self.name (or appropriate field).",
                    ))
                    in_model = False

        if in_model:
            issues.append(self._make_issue(
                ctx, line=model_line,
                message=f"Model '{model_name}' without __str__ method.",
                suggestion="Add def __str__(self): return self.name (or appropriate field).",
            ))
        return issues


# ---------------------------------------------------------------------------
# DF-Q04: QuerySet evaluated in template
# ---------------------------------------------------------------------------
@register
class QuerySetInTemplateRule(RegexRule):
    rule_id = "DF-Q04"
    name = "Unfiltered QuerySet to template"
    description = "Passing .all() to template context — load only needed data."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"""["']\w+["']\s*:\s*\w+\.objects\.all\s*\(\s*\)"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use pagination or .filter()/.only() to limit data sent to templates."


# ---------------------------------------------------------------------------
# DF-Q05: Hardcoded database URL
# ---------------------------------------------------------------------------
@register
class HardcodedDbUrlRule(RegexRule):
    rule_id = "DF-Q05"
    name = "Hardcoded database URL"
    description = "Database connection string hardcoded in settings."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""(?:DATABASES|SQLALCHEMY_DATABASE_URI|DATABASE_URL)\s*=\s*(?:\{[^}]*["'](?:postgres|mysql|sqlite)://|["'](?:postgres|mysql|sqlite)://)"""
    exclude_pattern = r"^\s*#|os\.environ|config\("
    fix_suggestion = "Use os.environ or dj-database-url for database configuration."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# DF-Q06: Missing migration (model changes without makemigrations)
# ---------------------------------------------------------------------------
@register
class ModelFieldWithoutDefaultRule(BaseRule):
    rule_id = "DF-Q06"
    name = "Model field without default"
    description = "Non-nullable field without default — migration will fail without default."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.PYTHON]

    _FIELD = re.compile(
        r"=\s*models\.(?:CharField|IntegerField|FloatField|TextField|DateTimeField|BooleanField)\s*\("
    )
    _NULLABLE = re.compile(r"null\s*=\s*True|blank\s*=\s*True|default\s*=")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._FIELD.search(line):
                # Check if default/null is set (might span lines)
                window = "\n".join(ctx.lines[i:i + 3])
                if not self._NULLABLE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Model field without default or null=True — migration requires a default.",
                        suggestion="Add default=... or null=True to the field definition.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# DF-Q07: Flask global state mutation
# ---------------------------------------------------------------------------
@register
class FlaskGlobalStateRule(RegexRule):
    rule_id = "DF-Q07"
    name = "Flask global state mutation"
    description = "Mutating global variable in Flask route — not thread-safe."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.PYTHON]
    pattern = r"(?:global\s+\w+|^[a-zA-Z_]\w*\s*(?:\+=|\-=|\.append|\.extend|\.update)\s*)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use Flask's g, session, or a database instead of global state."


# ---------------------------------------------------------------------------
# DF-Q08: Missing database index on commonly filtered field
# ---------------------------------------------------------------------------
@register
class MissingDbIndexRule(RegexRule):
    rule_id = "DF-Q08"
    name = "Missing database index"
    description = "ForeignKey/filter field without db_index — may cause slow queries."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"=\s*models\.(?:CharField|SlugField)\s*\([^)]*unique\s*=\s*False[^)]*\)"
    exclude_pattern = r"^\s*#|db_index"
    fix_suggestion = "Add db_index=True for fields frequently used in .filter() or .get()."


# ---------------------------------------------------------------------------
# DF-P01: Unbounded queryset serialization
# ---------------------------------------------------------------------------
@register
class UnboundedQuerysetRule(RegexRule):
    rule_id = "DF-P01"
    name = "Unbounded queryset"
    description = "Serializing all objects without pagination — memory and performance issue."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"(?:serializer|Serializer)\s*\(\s*\w+\.objects\.all\s*\(\s*\)\s*,\s*many\s*=\s*True"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use pagination: paginator.paginate_queryset(queryset)."


# ---------------------------------------------------------------------------
# DF-P02: Inefficient bulk_create without batch_size
# ---------------------------------------------------------------------------
@register
class BulkCreateWithoutBatchRule(RegexRule):
    rule_id = "DF-P02"
    name = "bulk_create without batch_size"
    description = "bulk_create without batch_size — may exceed DB query length limits."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"\.bulk_create\s*\([^)]*\)\s*$"
    exclude_pattern = r"^\s*#|batch_size"
    fix_suggestion = "Add batch_size parameter: .bulk_create(objects, batch_size=1000)."


# ---------------------------------------------------------------------------
# DF-P03: Using .count() after .all()
# ---------------------------------------------------------------------------
@register
class CountAfterAllRule(RegexRule):
    rule_id = "DF-P03"
    name = "Redundant .all().count()"
    description = ".all().count() is redundant — use .count() directly."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"\.all\s*\(\s*\)\s*\.count\s*\(\s*\)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use Model.objects.count() directly."


# ---------------------------------------------------------------------------
# DF-P04: sync_to_async wrapping blocking ORM call
# ---------------------------------------------------------------------------
@register
class SyncToAsyncOrmRule(RegexRule):
    rule_id = "DF-P04"
    name = "Blocking ORM in async view"
    description = "Django ORM call in async view without sync_to_async — blocks event loop."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"async\s+def\s+\w+.*(?:\.objects\.|\.save\(|\.delete\(|\.filter\(|\.get\()"
    exclude_pattern = r"^\s*#|sync_to_async|await"
    fix_suggestion = "Wrap ORM calls with sync_to_async or use Django 4.1+ async ORM."


# ---------------------------------------------------------------------------
# DF-M01: Wildcard import in views/models
# ---------------------------------------------------------------------------
@register
class WildcardImportRule(RegexRule):
    rule_id = "DF-M01"
    name = "Wildcard import"
    description = "Wildcard import in views/models pollutes namespace."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]
    pattern = r"from\s+\w[\w.]*\s+import\s+\*"
    exclude_pattern = r"^\s*#|__init__"
    fix_suggestion = "Import specific names: from module import Class1, Class2."


# ---------------------------------------------------------------------------
# DF-M02: Fat view function (too long)
# ---------------------------------------------------------------------------
@register
class FatViewFunctionRule(BaseRule):
    rule_id = "DF-M02"
    name = "Fat view function"
    description = "View function exceeds 50 lines — extract logic to services/utils."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _ROUTE = re.compile(r"@(?:app\.route|require_http_methods|api_view)")
    _FUNC = re.compile(r"^def\s+\w+\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        is_view = False
        func_start = 0

        for i, line in enumerate(ctx.lines):
            if self._ROUTE.search(line):
                is_view = True
                continue
            if is_view and self._FUNC.search(line):
                func_start = i
                is_view = False
            elif func_start and re.match(r"^(?:def |class |@)", line) and i > func_start:
                length = i - func_start
                if length > 50:
                    issues.append(self._make_issue(
                        ctx, line=func_start + 1,
                        message=f"View function is {length} lines — extract business logic to service layer.",
                        suggestion="Move logic to a service module, keep the view thin.",
                    ))
                func_start = 0

        return issues
