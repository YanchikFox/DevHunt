from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  Spring Boot / Spring Framework rules (SP-*)
#  Universal rules for any Spring Boot / Spring MVC / Spring Data project.
# ===========================================================================


# ---------------------------------------------------------------------------
# SP-S01: SQL injection via @Query string concatenation
# ---------------------------------------------------------------------------
@register
class SpSqlInjectionRule(RegexRule):
    rule_id = "SP-S01"
    name = "Spring SQL injection"
    description = "@Query with string concatenation — SQL injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"""@Query\s*\(\s*["'].*["']\s*\+"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use named parameters: @Query(\"SELECT u FROM User u WHERE u.name = :name\")."
    cwe_id = "CWE-89"


# ---------------------------------------------------------------------------
# SP-S02: Mass assignment (no @JsonIgnore on sensitive fields)
# ---------------------------------------------------------------------------
@register
class SpMassAssignmentRule(BaseRule):
    rule_id = "SP-S02"
    name = "Spring mass assignment"
    description = "Entity with password/role field without @JsonIgnore — mass assignment risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]
    cwe_id = "CWE-915"

    _ENTITY = re.compile(r"@(?:Entity|Document|Table)")
    _SENSITIVE = re.compile(r"(?:password|secret|token|role|isAdmin|permission)\s*[;=]", re.IGNORECASE)
    _PROTECTED = re.compile(r"@(?:JsonIgnore|JsonProperty\s*\(\s*access\s*=)")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._ENTITY.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._SENSITIVE.search(line):
                start = max(0, i - 3)
                window = "\n".join(ctx.lines[start:i + 1])
                if not self._PROTECTED.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Sensitive field without @JsonIgnore — mass assignment risk.",
                        suggestion="Add @JsonIgnore or use a DTO for request/response.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SP-S03: Hardcoded credentials in application.properties/yml
# ---------------------------------------------------------------------------
@register
class SpHardcodedCredsRule(RegexRule):
    rule_id = "SP-S03"
    name = "Hardcoded Spring credentials"
    description = "Password/secret hardcoded in Spring config file."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN, Language.YAML]
    pattern = r"""(?:password|secret|api[_-]?key)\s*[:=]\s*(?![\$\{])[^\s#]{4,}"""
    exclude_pattern = r"^\s*#|^\s*//|\.example|changeme|your-|xxx|placeholder"
    fix_suggestion = "Use env variables: spring.datasource.password=${DB_PASSWORD}."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# SP-S04: CORS wildcard
# ---------------------------------------------------------------------------
@register
class SpCorsWildcardRule(RegexRule):
    rule_id = "SP-S04"
    name = "Spring CORS wildcard"
    description = "allowedOrigins(\"*\") permits all origins — too permissive."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"""allowedOrigins\s*\(\s*["']\*["']\s*\)"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Restrict CORS to specific origins: .allowedOrigins(\"https://example.com\")."
    cwe_id = "CWE-942"


# ---------------------------------------------------------------------------
# SP-S05: Missing @PreAuthorize/@Secured
# ---------------------------------------------------------------------------
@register
class SpMissingAuthRule(BaseRule):
    rule_id = "SP-S05"
    name = "Missing authorization annotation"
    description = "REST controller without method-level security annotations."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]
    cwe_id = "CWE-862"

    _CONTROLLER = re.compile(r"@(?:RestController|Controller)")
    _AUTH = re.compile(r"@(?:PreAuthorize|Secured|RolesAllowed|IsAuthenticated)")
    _PUBLIC_MAPPING = re.compile(r"@(?:GetMapping|PostMapping|PutMapping|DeleteMapping|PatchMapping|RequestMapping)")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        if self._AUTH.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._PUBLIC_MAPPING.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Endpoint without authorization annotation — anyone can access.",
                    suggestion="Add @PreAuthorize(\"hasRole('USER')\") or configure in SecurityFilterChain.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# SP-S06: Exposed actuator endpoints
# ---------------------------------------------------------------------------
@register
class SpActuatorExposedRule(RegexRule):
    rule_id = "SP-S06"
    name = "Exposed actuator endpoints"
    description = "All actuator endpoints exposed — sensitive info disclosure."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML, Language.JAVA]
    pattern = r"""management\.endpoints\.web\.exposure\.include\s*[:=]\s*\*"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Expose only needed endpoints: management.endpoints.web.exposure.include=health,info."
    cwe_id = "CWE-200"


# ---------------------------------------------------------------------------
# SP-S07: Disabled CSRF
# ---------------------------------------------------------------------------
@register
class SpCsrfDisabledRule(RegexRule):
    rule_id = "SP-S07"
    name = "Spring CSRF disabled"
    description = "CSRF protection disabled — vulnerable unless API-only with token auth."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"csrf\s*\(\s*\)\s*\.disable\s*\(\s*\)|csrf\s*\{\s*disable\s*\(\s*\)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Keep CSRF enabled for form-based auth. Only disable for stateless API with Bearer tokens."
    cwe_id = "CWE-352"


# ---------------------------------------------------------------------------
# SP-Q01: N+1 query (missing @EntityGraph / JOIN FETCH)
# ---------------------------------------------------------------------------
@register
class SpNPlusOneRule(BaseRule):
    rule_id = "SP-Q01"
    name = "Spring N+1 query"
    description = "Repository method fetching entities with lazy relations without @EntityGraph."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.JAVA, Language.KOTLIN]

    _REPO = re.compile(r"interface\s+\w+Repository\s+extends")
    _FIND_ALL = re.compile(r"(?:findAll|findBy\w+)\s*\(")
    _ENTITY_GRAPH = re.compile(r"@EntityGraph")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._REPO.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._FIND_ALL.search(line):
                start = max(0, i - 3)
                window = "\n".join(ctx.lines[start:i])
                if not self._ENTITY_GRAPH.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Repository find method without @EntityGraph — possible N+1 queries.",
                        suggestion="Add @EntityGraph(attributePaths = {\"relation\"}) or use JOIN FETCH.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SP-Q02: Missing @Transactional on service method
# ---------------------------------------------------------------------------
@register
class SpMissingTransactionalRule(BaseRule):
    rule_id = "SP-Q02"
    name = "Missing @Transactional"
    description = "Service method with multiple repository writes without @Transactional."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.JAVA, Language.KOTLIN]

    _SERVICE = re.compile(r"@Service")
    _SAVE = re.compile(r"\.save(?:All|AndFlush)?\s*\(")
    _TRANSACTIONAL = re.compile(r"@Transactional")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._SERVICE.search(ctx.content):
            return []
        saves = list(self._SAVE.finditer(ctx.content))
        if len(saves) < 2:
            return []
        if self._TRANSACTIONAL.search(ctx.content):
            return []
        line_num = ctx.content[:saves[0].start()].count("\n") + 1
        return [self._make_issue(
            ctx, line=line_num,
            message="Multiple repository saves without @Transactional — data inconsistency risk.",
            suggestion="Add @Transactional to the method or class.",
        )]


# ---------------------------------------------------------------------------
# SP-Q03: Using field injection instead of constructor
# ---------------------------------------------------------------------------
@register
class SpFieldInjectionRule(BaseRule):
    rule_id = "SP-Q03"
    name = "Spring field injection"
    description = "@Autowired on field — use constructor injection for testability."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA]

    _AUTOWIRED = re.compile(r"@Autowired")
    _FIELD = re.compile(r"^\s*(?:private|protected)\s+\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._AUTOWIRED.search(line):
                next_line = ctx.lines[i + 1] if i + 1 < len(ctx.lines) else ""
                if self._FIELD.search(next_line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="@Autowired field injection — use constructor injection.",
                        suggestion="Use constructor injection with @RequiredArgsConstructor (Lombok).",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SP-Q04: Catching generic Exception
# ---------------------------------------------------------------------------
@register
class SpCatchGenericRule(RegexRule):
    rule_id = "SP-Q04"
    name = "Spring catch generic Exception"
    description = "Catching base Exception hides bugs — catch specific types."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"catch\s*\(\s*Exception\s+\w+\s*\)"
    exclude_pattern = r"^\s*//|@ExceptionHandler"
    fix_suggestion = "Catch specific exceptions (DataAccessException, HttpClientErrorException, etc.)."


# ---------------------------------------------------------------------------
# SP-Q05: Fat controller (business logic in controller)
# ---------------------------------------------------------------------------
@register
class SpFatControllerRule(BaseRule):
    rule_id = "SP-Q05"
    name = "Spring fat controller"
    description = "Controller with repository injection — move logic to service layer."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA, Language.KOTLIN]

    _CONTROLLER = re.compile(r"@(?:RestController|Controller)")
    _REPO_INJECTION = re.compile(r"(?:private|final)\s+\w+Repository\s+\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        for i, line in enumerate(ctx.lines):
            if self._REPO_INJECTION.search(line):
                return [self._make_issue(
                    ctx, line=i + 1,
                    message="Repository injected directly into controller — use a service layer.",
                    suggestion="Move data access to a @Service class.",
                )]
        return []


# ---------------------------------------------------------------------------
# SP-Q06: Missing @Valid on request body
# ---------------------------------------------------------------------------
@register
class SpMissingValidRule(RegexRule):
    rule_id = "SP-Q06"
    name = "Missing @Valid on @RequestBody"
    description = "@RequestBody without @Valid — bean validation constraints not enforced."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"@RequestBody\s+(?!@Valid)"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Add @Valid before the parameter: @Valid @RequestBody MyDto dto."


# ---------------------------------------------------------------------------
# SP-Q07: Returning Optional.get() without isPresent check
# ---------------------------------------------------------------------------
@register
class SpOptionalGetRule(RegexRule):
    rule_id = "SP-Q07"
    name = "Optional.get() without check"
    description = "Calling .get() on Optional without isPresent — NoSuchElementException risk."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.JAVA]
    pattern = r"\.get\s*\(\s*\)\s*(?!;.*isPresent)"
    exclude_pattern = r"^\s*//|orElse|orElseThrow|isPresent|ifPresent"
    fix_suggestion = "Use .orElseThrow(() -> new NotFoundException(...))."


# ---------------------------------------------------------------------------
# SP-Q08: Thread.sleep in production code
# ---------------------------------------------------------------------------
@register
class SpThreadSleepRule(RegexRule):
    rule_id = "SP-Q08"
    name = "Thread.sleep in production"
    description = "Thread.sleep() blocks the thread — use scheduled tasks or async."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.JAVA, Language.KOTLIN]
    pattern = r"Thread\.sleep\s*\("
    exclude_pattern = r"^\s*//|@Test|test"
    fix_suggestion = "Use @Scheduled, CompletableFuture.delayedExecutor, or async patterns."


# ---------------------------------------------------------------------------
# SP-P01: Missing database index on query column
# ---------------------------------------------------------------------------
@register
class SpMissingIndexRule(BaseRule):
    rule_id = "SP-P01"
    name = "Missing JPA index"
    description = "Entity field used in @Query WHERE without @Index — slow queries."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.JAVA, Language.KOTLIN]

    _QUERY_WHERE = re.compile(r"""@Query\s*\(\s*["'].*WHERE\s+\w+\.(\w+)\s*=""")
    _INDEX = re.compile(r"@Index|@Indexed")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._QUERY_WHERE.finditer(ctx.content):
            field = match.group(1)
            if field.lower() not in ("id",):
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message=f"@Query filters on '{field}' — ensure a database index exists.",
                    suggestion=f"Add @Index on the entity class: @Table(indexes = @Index(columnList = \"{field}\")).",
                ))
        return issues


# ---------------------------------------------------------------------------
# SP-P02: Unbounded @OneToMany (no pagination)
# ---------------------------------------------------------------------------
@register
class SpUnboundedOneToManyRule(BaseRule):
    rule_id = "SP-P02"
    name = "Unbounded @OneToMany"
    description = "@OneToMany without size limit — loading thousands of related objects."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.JAVA, Language.KOTLIN]

    _ONE_TO_MANY = re.compile(r"@OneToMany")
    _FETCH_LAZY = re.compile(r"fetch\s*=\s*FetchType\.LAZY")
    _BATCH_SIZE = re.compile(r"@BatchSize")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._ONE_TO_MANY.search(line):
                window = "\n".join(ctx.lines[i:i + 3])
                if not self._FETCH_LAZY.search(window) and not self._BATCH_SIZE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="@OneToMany without LAZY fetch — may load thousands of objects eagerly.",
                        suggestion="Add fetch = FetchType.LAZY and @BatchSize(size = 25).",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SP-P03: Returning entity from controller (not DTO)
# ---------------------------------------------------------------------------
@register
class SpReturnEntityRule(BaseRule):
    rule_id = "SP-P03"
    name = "Spring return entity"
    description = "Controller returning JPA entity — circular refs, over-fetching, schema leak."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.JAVA, Language.KOTLIN]

    _CONTROLLER = re.compile(r"@(?:RestController|Controller)")
    _ENTITY = re.compile(r"@Entity")
    _RETURN_TYPE = re.compile(
        r"public\s+(?:ResponseEntity<|List<|Optional<)?(\w+?)>?\s+\w+\s*\("
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._CONTROLLER.search(ctx.content):
            return []
        # Collect entity names from imports
        entity_types: set[str] = set()
        for line in ctx.lines:
            match = re.search(r"import\s+[\w.]+\.(?:entity|model|domain)\.(\w+)\s*;", line)
            if match:
                entity_types.add(match.group(1))

        if not entity_types:
            return []

        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            match = self._RETURN_TYPE.search(line)
            if match and match.group(1) in entity_types:
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message=f"Returning entity '{match.group(1)}' from controller — use a DTO.",
                    suggestion="Map to a response DTO with ModelMapper or MapStruct.",
                ))
        return issues


# ---------------------------------------------------------------------------
# SP-P04: @Transactional(readOnly=false) on read-only method
# ---------------------------------------------------------------------------
@register
class SpReadOnlyTransactionalRule(BaseRule):
    rule_id = "SP-P04"
    name = "Non-readOnly @Transactional for reads"
    description = "@Transactional without readOnly=true on a get/find/list method."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.JAVA, Language.KOTLIN]

    _TRANSACTIONAL = re.compile(r"@Transactional(?!\s*\(\s*readOnly)")
    _READ_METHOD = re.compile(r"(?:public|protected)\s+\S+\s+(?:get|find|list|search|fetch|count)\w*\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._TRANSACTIONAL.search(line):
                next_line = ctx.lines[i + 1] if i + 1 < len(ctx.lines) else ""
                combined = line + next_line
                if self._READ_METHOD.search(combined):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="@Transactional without readOnly on read method — unnecessary locking.",
                        suggestion="Add @Transactional(readOnly = true) for read-only methods.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# SP-M01: Circular dependency
# ---------------------------------------------------------------------------
@register
class SpCircularDependencyRule(BaseRule):
    rule_id = "SP-M01"
    name = "Spring circular dependency"
    description = "@Lazy used to break circular dependency — redesign instead."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.JAVA, Language.KOTLIN]

    _LAZY = re.compile(r"@Lazy")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._LAZY.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="@Lazy used — likely hiding circular dependency.",
                    suggestion="Extract shared logic into a separate service to break the cycle.",
                ))
        return issues


# ---------------------------------------------------------------------------
# SP-M02: Mutable injected collection
# ---------------------------------------------------------------------------
@register
class SpMutableCollectionRule(BaseRule):
    rule_id = "SP-M02"
    name = "Mutable injected collection"
    description = "Injecting mutable collection — other beans can modify shared state."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.JAVA]

    _INJECT = re.compile(r"@(?:Autowired|Inject)")
    _COLLECTION = re.compile(r"^\s*(?:private|protected)\s+(?:List|Map|Set)<")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._INJECT.search(line):
                next_line = ctx.lines[i + 1] if i + 1 < len(ctx.lines) else ""
                if self._COLLECTION.search(next_line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Mutable collection injected — other beans can modify shared state.",
                        suggestion="Wrap with Collections.unmodifiableList() or use ImmutableList.",
                    ))
        return issues
