from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class EvalUsageRule(RegexRule):
    rule_id = "RB-S01"
    name = "eval() usage"
    description = "eval executes arbitrary Ruby code."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\beval\s*[\(]"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Avoid eval. Use safe alternatives like send() with validated method names."
    cwe_id = "CWE-95"


@register
class SystemCommandRule(RegexRule):
    rule_id = "RB-S02"
    name = "System command execution"
    description = "Shell command execution with potential user input."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\b(?:system|exec|%x|`)\s*[\(\{]?.*?#\{"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use array form of system(): system('cmd', arg1, arg2) to avoid shell injection."
    cwe_id = "CWE-78"


@register
class SqlInjectionRule(RegexRule):
    rule_id = "RB-S03"
    name = "SQL injection risk"
    description = "String interpolation in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"""(?:where|find_by_sql|execute|select)\s*\(\s*["'](?i:SELECT|INSERT|UPDATE|DELETE).*?#\{"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use parameterized queries: where('name = ?', name)."
    cwe_id = "CWE-89"


@register
class HardcodedSecretsRule(RegexRule):
    rule_id = "RB-S04"
    name = "Hardcoded secret"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"""(?i)(?:password|secret|api_?key|token)\s*=\s*['"][^'"\s]{8,}['"]"""
    exclude_pattern = r"^\s*#|test|mock|example|spec"
    fix_suggestion = "Use ENV['SECRET_KEY'] or Rails credentials."
    cwe_id = "CWE-798"


@register
class MassAssignmentRule(RegexRule):
    rule_id = "RB-S05"
    name = "Mass assignment risk"
    description = "params.permit! or unfiltered params in mass assignment."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"params\.permit!|\.new\s*\(\s*params\s*\)|\.update\s*\(\s*params\s*\)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use strong parameters: params.require(:model).permit(:field1, :field2)."
    cwe_id = "CWE-915"


@register
class PutsInProductionRule(RegexRule):
    rule_id = "RB-Q01"
    name = "puts/p in production"
    description = "puts/p/pp should not be in production code."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUBY]
    pattern = r"^\s+(?:puts|pp?)\s+"
    exclude_pattern = r"^\s*#|spec|test"
    fix_suggestion = "Use Rails.logger or a proper logging framework."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "spec" in ctx.path.name.lower() or "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


@register
class OpenRedirectRule(RegexRule):
    rule_id = "RB-S06"
    name = "Open redirect"
    description = "Redirect using user-controlled parameter."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"redirect_to\s+params\[|redirect_to\s+request\."
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Validate redirect URL or use redirect_to with allow_other_host: false."
    cwe_id = "CWE-601"


@register
class DeprecatedMethodRule(RegexRule):
    rule_id = "RB-Q02"
    name = "Deprecated method"
    description = "Using deprecated Ruby/Rails method."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.RUBY]
    pattern = r"\.exists?\s*\(\s*\)|\.update_attributes\s*\(|\.save!\s*\(\s*validate:\s*false\s*\)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use modern alternatives: .exists? → .exist?, .update_attributes → .update."


# --- NEW SECURITY RULES ---


@register
class InsecureYamlLoadRule(RegexRule):
    rule_id = "RB-S07"
    name = "Insecure YAML.load"
    description = "YAML.load can deserialize arbitrary Ruby objects, enabling remote code execution."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\bYAML\.load\s*[\(\[](?!.*safe)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Replace YAML.load with YAML.safe_load to prevent arbitrary object deserialization."
    cwe_id = "CWE-502"


@register
class HardcodedDatabaseUrlRule(RegexRule):
    rule_id = "RB-S08"
    name = "Hardcoded database URL"
    description = "Database connection string embedded in source code."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"""(?i)(?:database_url|db_url|connection_string|DATABASE_URL)\s*=\s*['"](?:postgres|mysql|sqlite|mongodb|redis)://[^'"\s]{4,}['"]"""
    exclude_pattern = r"^\s*#|test|spec|example"
    fix_suggestion = "Use ENV['DATABASE_URL'] or Rails credentials to store connection strings."
    cwe_id = "CWE-798"


@register
class RenderInlineUserInputRule(RegexRule):
    rule_id = "RB-S09"
    name = "render inline with user input"
    description = "render inline: evaluates ERB from a string, risking XSS if user-controlled data is included."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\brender\s+inline:\s*.*?(?:params\[|request\.|@\w+)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Avoid render inline: with user-supplied data. Use a template file and sanitize output."
    cwe_id = "CWE-79"


@register
class SendWithUserInputRule(RegexRule):
    rule_id = "RB-S10"
    name = "send/public_send with user-controlled method name"
    description = "Calling send() or public_send() with a user-controlled argument enables arbitrary method dispatch."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\b(?:send|public_send)\s*\(\s*params\["
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Whitelist allowed method names before passing to send(): ALLOWED = %i[foo bar]; send(ALLOWED.find { |m| m == params[:action] })."
    cwe_id = "CWE-94"


@register
class InsecureCookieRule(RegexRule):
    rule_id = "RB-S11"
    name = "Insecure cookie"
    description = "Cookie set without :secure or :httponly flags, making it vulnerable to interception and XSS theft."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\bcookies\s*\[.*?\]\s*=\s*(?!\{)|\bcookies\[.*?\]\s*=\s*\{(?!.*(?:secure|httponly))"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Set cookies with security flags: cookies[:name] = { value: val, secure: true, httponly: true }."
    cwe_id = "CWE-614"


@register
class CsrfSkipRule(RegexRule):
    rule_id = "RB-S12"
    name = "CSRF protection disabled"
    description = "skip_before_action :verify_authenticity_token disables Rails CSRF protection for the controller."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\bskip_before_action\s*:verify_authenticity_token"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Remove skip_before_action :verify_authenticity_token. For API-only controllers use protect_from_forgery with: :null_session."
    cwe_id = "CWE-352"


@register
class DangerousSendFileRule(RegexRule):
    rule_id = "RB-S13"
    name = "Dangerous send_file with user-controlled path"
    description = "send_file with a user-supplied path enables path traversal, allowing access to arbitrary server files."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.RUBY]
    pattern = r"\bsend_file\s*\(?\s*(?:params\[|request\.|\"[^\"]*#\{)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Resolve and validate the path against a safe base directory using File.expand_path and ensure it starts with the allowed prefix."
    cwe_id = "CWE-22"


# --- NEW QUALITY RULES ---


@register
class TodoFixmeCommentRule(RegexRule):
    rule_id = "RB-Q03"
    name = "TODO/FIXME comment"
    description = "Unresolved TODO or FIXME comment found in code."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.RUBY]
    pattern = r"#\s*(?:TODO|FIXME|HACK|XXX)\b"
    exclude_pattern = None
    fix_suggestion = "Resolve the TODO/FIXME or create a tracking issue and reference it in the comment."


@register
class RescueExceptionRule(RegexRule):
    rule_id = "RB-Q04"
    name = "Rescue Exception (too broad)"
    description = "rescue Exception catches all exceptions including SignalException, SyntaxError, and NoMemoryError, masking critical failures."
    severity = Severity.HIGH
    category = Category.QUALITY
    languages = [Language.RUBY]
    pattern = r"\brescue\s+Exception\b"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Rescue StandardError or a specific exception class instead: rescue StandardError => e."


@register
class MethodTooLongRule(BaseRule):
    rule_id = "RB-Q05"
    name = "Method too long"
    description = "Method body exceeds 30 lines, violating Ruby convention for readable, single-purpose methods."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUBY]
    fix_suggestion = "Extract logical sections into private helper methods to keep each method under 30 lines."

    _DEF_RE = re.compile(r"^\s*def\s+\w+")
    _END_RE = re.compile(r"^\s*end\b")
    _MAX_LINES = 30

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        method_start: int | None = None
        method_line: int | None = None

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if self._DEF_RE.match(line):
                if depth == 0:
                    method_start = i
                    method_line = i
                depth += 1
            elif self._END_RE.match(line) and depth > 0:
                depth -= 1
                if depth == 0 and method_start is not None:
                    length = i - method_start - 1
                    if length > self._MAX_LINES:
                        issues.append(self._make_issue(
                            ctx,
                            line=method_line,
                            column=0,
                            end_line=i,
                            message=f"Method is {length} lines long (limit: {self._MAX_LINES}).",
                            suggestion=self.fix_suggestion,
                        ))
                    method_start = None
                    method_line = None

        return issues


@register
class ClassTooLongRule(BaseRule):
    rule_id = "RB-Q06"
    name = "Class too long"
    description = "Class body exceeds 300 lines, indicating it likely has too many responsibilities."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUBY]
    fix_suggestion = "Split the class into smaller, focused classes following the Single Responsibility Principle."

    _CLASS_RE = re.compile(r"^\s*class\s+\w+")
    _END_RE = re.compile(r"^\s*end\b")
    _MAX_LINES = 300

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        class_start: int | None = None
        class_line: int | None = None

        for i, line in enumerate(ctx.lines, start=1):
            if self._CLASS_RE.match(line):
                if depth == 0:
                    class_start = i
                    class_line = i
                depth += 1
            elif self._END_RE.match(line) and depth > 0:
                depth -= 1
                if depth == 0 and class_start is not None:
                    length = i - class_start - 1
                    if length > self._MAX_LINES:
                        issues.append(self._make_issue(
                            ctx,
                            line=class_line,
                            column=0,
                            end_line=i,
                            message=f"Class is {length} lines long (limit: {self._MAX_LINES}).",
                            suggestion=self.fix_suggestion,
                        ))
                    class_start = None
                    class_line = None

        return issues


@register
class MultipleReturnTypesRule(BaseRule):
    rule_id = "RB-Q07"
    name = "Multiple return types"
    description = "Method returns both nil/false and a value type, creating inconsistent return contract."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUBY]
    fix_suggestion = "Use a consistent return type: return a Result/Option object, raise an exception, or always return the same type."

    _DEF_RE = re.compile(r"^\s*def\s+\w+")
    _END_RE = re.compile(r"^\s*end\b")
    _RETURN_NIL_RE = re.compile(r"\breturn\s+(?:nil|false)\b")
    _RETURN_VAL_RE = re.compile(r"\breturn\s+(?!nil\b|false\b)\S")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        method_start: int | None = None
        has_nil_return = False
        has_val_return = False

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if self._DEF_RE.match(line):
                if depth == 0:
                    method_start = i
                    has_nil_return = False
                    has_val_return = False
                depth += 1
            elif self._END_RE.match(line) and depth > 0:
                depth -= 1
                if depth == 0 and method_start is not None:
                    if has_nil_return and has_val_return:
                        issues.append(self._make_issue(
                            ctx,
                            line=method_start,
                            column=0,
                            end_line=i,
                            message="Method has mixed return types (nil/false and a value).",
                            suggestion=self.fix_suggestion,
                        ))
                    method_start = None
            elif depth > 0 and method_start is not None:
                if self._RETURN_NIL_RE.search(line):
                    has_nil_return = True
                if self._RETURN_VAL_RE.search(line):
                    has_val_return = True

        return issues


@register
class NestedConditionalsRule(BaseRule):
    rule_id = "RB-Q08"
    name = "Nested conditionals too deep"
    description = "Conditional nesting exceeds 3 levels, making logic hard to follow and test."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.RUBY]
    fix_suggestion = "Use early returns (guard clauses) or extract nested branches into separate methods."

    _OPEN_RE = re.compile(r"^\s*(?:if|unless|case|while|until|for|do)\b")
    _CLOSE_RE = re.compile(r"^\s*end\b")
    _MAX_DEPTH = 3

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        reported: set[int] = set()

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue
            if self._OPEN_RE.match(line):
                depth += 1
                if depth > self._MAX_DEPTH and i not in reported:
                    reported.add(i)
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=len(line) - len(line.lstrip()),
                        message=f"Conditional nesting depth is {depth} (limit: {self._MAX_DEPTH}).",
                        suggestion=self.fix_suggestion,
                    ))
            elif self._CLOSE_RE.match(line):
                depth = max(0, depth - 1)

        return issues


@register
class NPlusOneQueryRule(BaseRule):
    rule_id = "RB-Q09"
    name = "N+1 query pattern"
    description = "Database query (.where/.find) called inside an iteration block, causing one query per iteration."
    severity = Severity.HIGH
    category = Category.QUALITY
    languages = [Language.RUBY]
    fix_suggestion = "Use eager loading with .includes(:association) or batch-load records before the loop with a single query."

    _LOOP_RE = re.compile(r"\.\s*(?:each|map|select|reject|find_each|each_with_object)\s*(?:do|\{)")
    _QUERY_RE = re.compile(r"\.\s*(?:where|find|find_by|find_by_id|find_by_sql|first|last|count|exists\?)\s*[\(\{]")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        loop_depth = 0

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue

            if self._LOOP_RE.search(line):
                loop_depth += 1

            if loop_depth > 0 and self._QUERY_RE.search(line):
                if not self._LOOP_RE.search(line):
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=len(line) - len(line.lstrip()),
                        message="Database query inside iteration block detected (N+1 risk).",
                        suggestion=self.fix_suggestion,
                    ))

            if stripped == "end" or stripped == "}":
                loop_depth = max(0, loop_depth - 1)

        return issues


# --- NEW PERFORMANCE RULES ---


@register
class EachPushInsteadOfMapRule(BaseRule):
    rule_id = "RB-P01"
    name = "each + push instead of map/select"
    description = "Building a result array with .each + .push/<<  is less expressive and slower than using .map or .select."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUBY]
    fix_suggestion = "Replace .each { |x| arr << transform(x) } with .map { |x| transform(x) }, or .select for filtering."

    _EACH_RE = re.compile(r"\.\s*each\s*(?:do\s*\||\{[^|]*\|)")
    _PUSH_RE = re.compile(r"(?:<<|\.push\s*\(|\.append\s*\()")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_each_block = False
        each_line: int = 0
        block_depth = 0

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue

            if self._EACH_RE.search(line):
                in_each_block = True
                each_line = i
                block_depth = 1
            elif in_each_block:
                if re.search(r"\bdo\b|\{", line):
                    block_depth += 1
                if re.search(r"\bend\b|\}", line):
                    block_depth -= 1
                    if block_depth <= 0:
                        in_each_block = False
                        block_depth = 0
                elif self._PUSH_RE.search(line):
                    issues.append(self._make_issue(
                        ctx,
                        line=each_line,
                        column=0,
                        end_line=i,
                        message="Array built with .each + push/<<. Use .map or .select instead.",
                        suggestion=self.fix_suggestion,
                    ))
                    in_each_block = False

        return issues


@register
class StringConcatInLoopRule(BaseRule):
    rule_id = "RB-P02"
    name = "String concatenation in loop"
    description = "Repeated String concatenation with += or << inside a loop creates many intermediate string objects."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.RUBY]
    fix_suggestion = "Collect parts in an Array and call .join at the end, or use StringIO for large buildups."

    _LOOP_RE = re.compile(r"\b(?:each|map|times|upto|downto|loop|while|until|for)\b.*(?:do|\{)")
    _CONCAT_RE = re.compile(r"""(\w+)\s*\+=\s*['"]|(\w+)\s*<<\s*['"]""")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        loop_depth = 0
        reported_vars: set[str] = set()

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue

            if self._LOOP_RE.search(line):
                loop_depth += 1
                reported_vars.clear()

            if loop_depth > 0:
                m = self._CONCAT_RE.search(line)
                if m:
                    var = m.group(1) or m.group(2)
                    if var and var not in reported_vars:
                        reported_vars.add(var)
                        issues.append(self._make_issue(
                            ctx,
                            line=i,
                            column=m.start(),
                            message=f"String concatenation with '{var}' inside a loop creates excessive object allocation.",
                            suggestion=self.fix_suggestion,
                        ))

            if stripped in ("end", "}"):
                loop_depth = max(0, loop_depth - 1)
                if loop_depth == 0:
                    reported_vars.clear()

        return issues
