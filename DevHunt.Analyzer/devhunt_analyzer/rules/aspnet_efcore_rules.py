from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  ASP.NET Core / Entity Framework Core rules (EF-*)
#  Universal rules for any ASP.NET + EF Core project.
# ===========================================================================


# ---------------------------------------------------------------------------
# EF-S01: Raw SQL without parameterization
# ---------------------------------------------------------------------------
@register
class EfRawSqlRule(RegexRule):
    rule_id = "EF-S01"
    name = "Raw SQL injection risk"
    description = "FromSqlRaw/ExecuteSqlRaw with interpolation — SQL injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r'(?:FromSqlRaw|ExecuteSqlRaw|SqlQuery)\s*\(\s*(?:\$"|[^"]*\+)'
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use FromSqlInterpolated() or pass parameters separately."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# EF-S02: Mass assignment (binding entity directly from request)
# ---------------------------------------------------------------------------
@register
class MassAssignmentRule(RegexRule):
    rule_id = "EF-S02"
    name = "Mass assignment"
    description = "Binding entity directly from request body — use DTOs."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"public\s+(?:async\s+)?(?:Task<)?(?:IActionResult|ActionResult).*\(\s*\[FromBody\]\s*(?!.*Dto|.*Request|.*Command|.*Input|.*Model)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Accept a DTO/Request object instead of binding the entity directly."
    cwe_id = "CWE-915"


# ---------------------------------------------------------------------------
# EF-S03: Exposed connection string
# ---------------------------------------------------------------------------
@register
class ExposedConnectionStringRule(RegexRule):
    rule_id = "EF-S03"
    name = "Exposed connection string"
    description = "Connection string with credentials in source code."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"""(?i)(?:Server|Data Source|Host)\s*=.*?(?:Password|Pwd)\s*=\s*[^;"]"""
    exclude_pattern = r"^\s*//|appsettings\.Example|\.sample"
    fix_suggestion = "Use environment variables or user secrets for connection strings."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# EF-S04: Missing CORS restriction
# ---------------------------------------------------------------------------
@register
class MissingCorsRestrictionRule(RegexRule):
    rule_id = "EF-S04"
    name = "Open CORS policy"
    description = "AllowAnyOrigin in CORS policy — too permissive for production."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"\.AllowAnyOrigin\s*\(\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Restrict CORS to specific origins with .WithOrigins(...)."
    cwe_id = "CWE-942"


# ---------------------------------------------------------------------------
# EF-S05: Missing rate limiting
# ---------------------------------------------------------------------------
@register
class MissingRateLimitRule(BaseRule):
    rule_id = "EF-S05"
    name = "Missing rate limiting"
    description = "Public API endpoint without rate limiting attribute."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-770"

    _CONTROLLER = re.compile(r"\[ApiController\]")
    _RATE_LIMIT = re.compile(r"\[(?:EnableRateLimiting|RateLimiting|DisableRateLimiting)\]")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        if self._RATE_LIMIT.search(ctx.content):
            return []
        # Find controller class line
        match = re.search(r"\[ApiController\]", ctx.content)
        if match:
            line_num = ctx.content[:match.start()].count("\n") + 1
            return [self._make_issue(
                ctx, line=line_num,
                message="API controller without rate limiting.",
                suggestion="Add [EnableRateLimiting(\"policy\")] to controller or endpoints.",
            )]
        return []


# ---------------------------------------------------------------------------
# EF-Q01: N+1 query pattern (accessing navigation in loop)
# ---------------------------------------------------------------------------
@register
class NPlusOneRule(BaseRule):
    rule_id = "EF-Q01"
    name = "N+1 query pattern"
    description = "Accessing navigation property inside loop — causes N+1 queries."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _LOOP = re.compile(r"\b(?:foreach|for)\s*\(")
    _NAV_ACCESS = re.compile(r"\.\w+\.(?:Select|Where|Any|Count|First|ToList|OrderBy)\s*\(")

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
                if self._NAV_ACCESS.search(line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Navigation property accessed in loop — likely N+1 query.",
                        suggestion="Use .Include() or project with .Select() before the loop.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# EF-Q02: Missing .AsNoTracking() for read-only queries
# ---------------------------------------------------------------------------
@register
class MissingAsNoTrackingRule(BaseRule):
    rule_id = "EF-Q02"
    name = "Missing AsNoTracking()"
    description = "Read-only query without AsNoTracking() — unnecessary change tracking overhead."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _QUERY = re.compile(r"\.(?:Where|Select|OrderBy|Skip|Take|First|Single|ToList|ToArray)Async?\s*\(")
    _TRACKING = re.compile(r"\.AsNoTracking\s*\(\s*\)")
    _WRITE = re.compile(r"\.(?:Add|Update|Remove|SaveChanges|Attach)\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip if file has write operations (probably a write-heavy service)
        if self._WRITE.search(ctx.content):
            return []
        issues: list[Issue] = []
        if self._QUERY.search(ctx.content) and not self._TRACKING.search(ctx.content):
            for i, line in enumerate(ctx.lines):
                if self._QUERY.search(line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Read query without .AsNoTracking() — unnecessary tracking overhead.",
                        suggestion="Add .AsNoTracking() for read-only queries.",
                    ))
                    break
        return issues


# ---------------------------------------------------------------------------
# EF-Q03: SaveChanges inside loop
# ---------------------------------------------------------------------------
@register
class SaveChangesInLoopRule(BaseRule):
    rule_id = "EF-Q03"
    name = "SaveChanges in loop"
    description = "SaveChangesAsync() inside loop — batch operations instead."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _LOOP = re.compile(r"\b(?:foreach|for|while)\s*\(")
    _SAVE = re.compile(r"SaveChanges(?:Async)?\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_depth = 0

        for i, line in enumerate(ctx.lines):
            if self._LOOP.search(line):
                in_loop = True
                loop_depth = 0
            if in_loop:
                loop_depth += line.count("{") - line.count("}")
                if loop_depth <= 0:
                    in_loop = False
                    continue
                if self._SAVE.search(line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="SaveChangesAsync() inside loop — call once after the loop.",
                        suggestion="Batch all changes, then call SaveChangesAsync() once after the loop.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# EF-Q04: Missing Include for navigation property
# ---------------------------------------------------------------------------
@register
class MissingIncludeRule(BaseRule):
    rule_id = "EF-Q04"
    name = "Missing Include()"
    description = "Navigation property accessed without .Include() — lazy loading may cause N+1."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _QUERY_START = re.compile(r"_(?:context|db|dbContext)\.\w+")
    _NAV_SELECT = re.compile(r"\.Select\s*\([^)]*\.\w+\.\w+")
    _INCLUDE = re.compile(r"\.Include\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._QUERY_START.search(line) and self._NAV_SELECT.search(line):
                start = max(0, i - 5)
                window = "\n".join(ctx.lines[start:i + 1])
                if not self._INCLUDE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Navigation property in Select without .Include() — possible N+1.",
                        suggestion="Add .Include(x => x.Navigation) before the query.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# EF-Q05: Fat controller (logic in controller instead of service)
# ---------------------------------------------------------------------------
@register
class FatControllerRule(BaseRule):
    rule_id = "EF-Q05"
    name = "Fat controller"
    description = "Controller method with direct DbContext usage — move to service layer."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.CSHARP]

    _CONTROLLER = re.compile(r"class\s+\w+Controller\s*:")
    _DB_CONTEXT = re.compile(r"_(?:context|db|dbContext)\.\w+\.\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._DB_CONTEXT.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Direct DbContext access in controller — use a service layer.",
                    suggestion="Move data access to a service/repository and inject it.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# EF-Q06: Missing transaction for multi-table writes
# ---------------------------------------------------------------------------
@register
class MissingTransactionRule(BaseRule):
    rule_id = "EF-Q06"
    name = "Missing transaction"
    description = "Multiple SaveChanges calls without explicit transaction."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _SAVE = re.compile(r"SaveChanges(?:Async)?\s*\(")
    _TRANSACTION = re.compile(r"BeginTransaction|IDbContextTransaction|TransactionScope|UseTransaction")

    def check(self, ctx: FileContext) -> list[Issue]:
        saves = list(self._SAVE.finditer(ctx.content))
        if len(saves) < 2:
            return []
        if self._TRANSACTION.search(ctx.content):
            return []
        line_num = ctx.content[:saves[0].start()].count("\n") + 1
        return [self._make_issue(
            ctx, line=line_num,
            message="Multiple SaveChanges calls without explicit transaction — data inconsistency risk.",
            suggestion="Wrap in a transaction: using var tx = await _context.Database.BeginTransactionAsync().",
        )]


# ---------------------------------------------------------------------------
# EF-Q07: Catching generic Exception
# ---------------------------------------------------------------------------
@register
class CatchGenericExceptionRule(RegexRule):
    rule_id = "EF-Q07"
    name = "Catch generic Exception"
    description = "Catching base Exception hides bugs. Catch specific exception types."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"catch\s*\(\s*Exception\s+\w+\s*\)"
    exclude_pattern = r"^\s*//|catch\s*\(\s*Exception\s+\w+\s*\)\s*when"
    fix_suggestion = "Catch specific exceptions (DbUpdateException, OperationCanceledException, etc.)."


# ---------------------------------------------------------------------------
# EF-Q08: Controller returning entity (not DTO)
# ---------------------------------------------------------------------------
@register
class ControllerReturningEntityRule(BaseRule):
    rule_id = "EF-Q08"
    name = "Controller returns entity"
    description = "Controller returning EF entity exposes internal schema and may cause circular refs."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]

    _CONTROLLER = re.compile(r"class\s+\w+Controller\s*:")
    _RETURN_ENTITY = re.compile(
        r"return\s+(?:Ok|Created|StatusCode)\s*\(\s*(?:entity|item|result|model|user|project)\s*\)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._RETURN_ENTITY.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Controller returning entity directly — use a DTO.",
                    suggestion="Map entity to a DTO/Response object before returning.",
                ))
        return issues


# ---------------------------------------------------------------------------
# EF-Q09: Missing CancellationToken in controller action
# ---------------------------------------------------------------------------
@register
class ControllerMissingCtRule(BaseRule):
    rule_id = "EF-Q09"
    name = "Missing CancellationToken in action"
    description = "Async controller action without CancellationToken parameter."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _CONTROLLER = re.compile(r"class\s+\w+Controller\s*:")
    _ASYNC_ACTION = re.compile(
        r"public\s+async\s+Task<(?:IActionResult|ActionResult)>\s+\w+\s*\(([^)]*)\)"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        issues: list[Issue] = []
        for match in self._ASYNC_ACTION.finditer(ctx.content):
            params = match.group(1)
            if "CancellationToken" not in params:
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="Async action without CancellationToken — cannot cancel on client disconnect.",
                    suggestion="Add 'CancellationToken ct' parameter and pass to async calls.",
                ))
        return issues


# ---------------------------------------------------------------------------
# EF-Q10: DbContext registered as Singleton
# ---------------------------------------------------------------------------
@register
class DbContextSingletonRule(RegexRule):
    rule_id = "EF-Q10"
    name = "DbContext as Singleton"
    description = "DbContext registered as Singleton — not thread-safe, causes data corruption."
    severity = Severity.CRITICAL
    category = Category.RELIABILITY
    languages = [Language.CSHARP]
    pattern = r"AddSingleton\s*<\s*\w*(?:Context|DbContext)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use AddDbContext or AddScoped for DbContext — never Singleton."


# ---------------------------------------------------------------------------
# EF-Q11: Missing global exception handler
# ---------------------------------------------------------------------------
@register
class MissingExceptionHandlerRule(BaseRule):
    rule_id = "EF-Q11"
    name = "Missing global exception handler"
    description = "No global exception handler middleware — unhandled exceptions leak stack traces."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-209"

    _EXCEPTION_HANDLER = re.compile(
        r"UseExceptionHandler|UseMiddleware<.*Exception|app\.UseStatusCodePages"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "Program.cs":
            return []
        if not self._EXCEPTION_HANDLER.search(ctx.content):
            return [self._make_issue(
                ctx, line=1,
                message="No global exception handler — stack traces leak to clients in production.",
                suggestion="Add app.UseExceptionHandler() or custom exception middleware.",
            )]
        return []


# ---------------------------------------------------------------------------
# EF-Q12: Missing HTTPS redirection
# ---------------------------------------------------------------------------
@register
class MissingHttpsRedirectionRule(BaseRule):
    rule_id = "EF-Q12"
    name = "Missing HTTPS redirection"
    description = "No HTTPS redirection middleware in pipeline."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-319"

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "Program.cs":
            return []
        if "UseHttpsRedirection" not in ctx.content:
            return [self._make_issue(
                ctx, line=1,
                message="No HTTPS redirection in middleware pipeline.",
                suggestion="Add app.UseHttpsRedirection() to force HTTPS.",
            )]
        return []


# ---------------------------------------------------------------------------
# EF-P01: ToListAsync on entire table
# ---------------------------------------------------------------------------
@register
class LoadEntireTableRule(RegexRule):
    rule_id = "EF-P01"
    name = "Loading entire table"
    description = "ToListAsync() without Where/Take — loads entire table into memory."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]
    pattern = r"_(?:context|db|dbContext)\.\w+\s*\.ToList(?:Async)?\s*\("
    exclude_pattern = r"^\s*//|\.Where|\.Take|\.Skip|\.First"
    fix_suggestion = "Add .Where(), .Take(), or pagination before materializing."


# ---------------------------------------------------------------------------
# EF-P02: Selecting all columns when only some needed
# ---------------------------------------------------------------------------
@register
class SelectAllColumnsRule(BaseRule):
    rule_id = "EF-P02"
    name = "Select all columns"
    description = "Query returns full entities when only few fields are needed."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _QUERY = re.compile(r"_(?:context|db|dbContext)\.\w+\.Where\s*\([^)]+\)\s*\.ToList(?:Async)?\s*\(")
    _SELECT = re.compile(r"\.Select\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._QUERY.finditer(ctx.content):
            # Check if there's a .Select before ToList
            query_text = ctx.content[max(0, match.start() - 200):match.end()]
            if not self._SELECT.search(query_text):
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="Query materializes full entities — use .Select() to project only needed fields.",
                    suggestion="Add .Select(x => new { x.Id, x.Name }) to reduce data transfer.",
                ))
        return issues


# ---------------------------------------------------------------------------
# EF-P03: Multiple round-trips (could use single query)
# ---------------------------------------------------------------------------
@register
class MultipleRoundTripsRule(BaseRule):
    rule_id = "EF-P03"
    name = "Multiple DB round-trips"
    description = "Multiple sequential await on DB queries — consider batching."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _DB_AWAIT = re.compile(r"await\s+_(?:context|db|dbContext)\.\w+\.")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        db_lines: list[int] = []
        for i, line in enumerate(ctx.lines):
            if self._DB_AWAIT.search(line):
                db_lines.append(i)

        # Check for clusters of sequential DB calls
        for j in range(len(db_lines) - 2):
            if db_lines[j + 2] - db_lines[j] <= 5:
                issues.append(self._make_issue(
                    ctx, line=db_lines[j] + 1,
                    message="3+ sequential DB queries — consider combining into a single query or using Task.WhenAll.",
                    suggestion="Combine queries with joins/includes or parallelize with Task.WhenAll.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# EF-P04: String interpolation in logging (allocates even if not logged)
# ---------------------------------------------------------------------------
@register
class LogStringInterpolationRule(RegexRule):
    rule_id = "EF-P04"
    name = "Log string interpolation"
    description = "String interpolation in logger allocates even when log level is disabled."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]
    pattern = r"_logger\.(?:LogDebug|LogTrace|LogInformation|LogWarning)\s*\(\s*\$\""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use structured logging: _logger.LogInformation(\"User {Id} created\", userId)."


# ---------------------------------------------------------------------------
# EF-P05: Synchronous I/O in async pipeline
# ---------------------------------------------------------------------------
@register
class SyncIoInAsyncRule(BaseRule):
    rule_id = "EF-P05"
    name = "Sync I/O in async method"
    description = "Synchronous I/O call in async method blocks the thread pool."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _ASYNC_METHOD = re.compile(r"async\s+Task")
    _SYNC_IO = re.compile(
        r"\.(?:Read|Write|ToList|SaveChanges|FirstOrDefault|Single|Any|Count)\s*\("
        r"|File\.(?:ReadAll|WriteAll|Exists)\s*\("
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._ASYNC_METHOD.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            if self._SYNC_IO.search(line) and "await" not in line and "Async" not in line:
                # Make sure we're inside an async method
                start = max(0, i - 20)
                window = "\n".join(ctx.lines[start:i])
                if self._ASYNC_METHOD.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Synchronous I/O in async method — use async variant.",
                        suggestion="Use the async version (e.g., ToListAsync, ReadAllTextAsync).",
                    ))
        return issues


# ---------------------------------------------------------------------------
# EF-M01: Missing [Required] on non-nullable FK
# ---------------------------------------------------------------------------
@register
class MissingRequiredAttributeRule(BaseRule):
    rule_id = "EF-M01"
    name = "Missing [Required] on FK"
    description = "Non-nullable foreign key property without [Required] — EF may generate nullable column."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _FK_PROP = re.compile(r"public\s+(?:Guid|int|long)\s+(\w+Id)\s*\{")
    _REQUIRED = re.compile(r"\[Required\]")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only for entity/model files
        name = ctx.path.name.lower()
        if not any(s in name for s in ["entity", "model", ".cs"]):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            match = self._FK_PROP.search(line)
            if match:
                start = max(0, i - 3)
                window = "\n".join(ctx.lines[start:i])
                if not self._REQUIRED.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message=f"FK property '{match.group(1)}' without [Required] attribute.",
                        suggestion="Add [Required] or make the type non-nullable to enforce the constraint.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# EF-M02: Missing index on frequently queried column
# ---------------------------------------------------------------------------
@register
class MissingIndexHintRule(BaseRule):
    rule_id = "EF-M02"
    name = "Missing index hint"
    description = "Where clause on non-PK column without [Index] — may cause full table scan."
    severity = Severity.INFO
    category = Category.PERFORMANCE
    languages = [Language.CSHARP]

    _WHERE_FIELD = re.compile(r"\.Where\s*\(\s*\w+\s*=>\s*\w+\.(\w+)\s*==")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        fields_queried: set[str] = set()
        for match in self._WHERE_FIELD.finditer(ctx.content):
            field = match.group(1)
            if field not in ("Id", "id") and field not in fields_queried:
                fields_queried.add(field)
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message=f"Filtering on '{field}' — ensure a database index exists for performance.",
                    suggestion=f"Add [Index] on {field} in the entity or Fluent API configuration.",
                ))
        return issues


# ---------------------------------------------------------------------------
# EF-M03: Middleware order issue
# ---------------------------------------------------------------------------
@register
class MiddlewareOrderRule(BaseRule):
    rule_id = "EF-M03"
    name = "Middleware order issue"
    description = "Authentication/Authorization middleware in wrong order."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.CSHARP]
    cwe_id = "CWE-862"

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "Program.cs":
            return []

        auth_line = None
        authz_line = None
        for i, line in enumerate(ctx.lines):
            if "UseAuthentication" in line and auth_line is None:
                auth_line = i
            if "UseAuthorization" in line and authz_line is None:
                authz_line = i

        if auth_line is not None and authz_line is not None:
            if auth_line > authz_line:
                return [self._make_issue(
                    ctx, line=authz_line + 1,
                    message="UseAuthorization() before UseAuthentication() — authorization won't work.",
                    suggestion="Call UseAuthentication() before UseAuthorization().",
                )]
        return []


# ---------------------------------------------------------------------------
# EF-M04: Sensitive data in response headers
# ---------------------------------------------------------------------------
@register
class SensitiveHeaderRule(RegexRule):
    rule_id = "EF-M04"
    name = "Sensitive response header"
    description = "Server version/technology exposed in response headers."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.CSHARP]
    pattern = r"""(?:\.Headers\.Add|Response\.Headers)\s*\(\s*["'](?:Server|X-Powered-By|X-AspNet-Version)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Remove server version headers. Use app.UseHsts() and remove X-Powered-By."
    cwe_id = "CWE-200"


# ---------------------------------------------------------------------------
# EF-M05: Missing model validation
# ---------------------------------------------------------------------------
@register
class MissingModelValidationRule(BaseRule):
    rule_id = "EF-M05"
    name = "Missing ModelState validation"
    description = "Controller action without ModelState.IsValid check (when not using [ApiController])."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CSHARP]

    _CONTROLLER_BASE = re.compile(r":\s*Controller\b")
    _API_CONTROLLER = re.compile(r"\[ApiController\]")
    _FROM_BODY = re.compile(r"\[FromBody\]|\[FromForm\]")
    _MODEL_STATE = re.compile(r"ModelState\.IsValid")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Skip [ApiController] — auto-validates
        if self._API_CONTROLLER.search(ctx.content):
            return []
        # Only for MVC controllers
        if not self._CONTROLLER_BASE.search(ctx.content):
            return []
        if not self._FROM_BODY.search(ctx.content):
            return []
        if self._MODEL_STATE.search(ctx.content):
            return []
        return [self._make_issue(
            ctx, line=1,
            message="MVC controller accepts model without ModelState.IsValid check.",
            suggestion="Add if (!ModelState.IsValid) return BadRequest(ModelState);",
        )]
