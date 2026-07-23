from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
# SECURITY (PY-S01 .. PY-S16)
# ===========================================================================

@register
class EvalUsageRule(RegexRule):
    rule_id = "PY-S01"
    name = "eval() usage"
    description = "eval() executes arbitrary code and is a security risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\beval\s*\("
    exclude_pattern = r"^\s*#|ast\.literal_eval|safe_eval|literal_eval"
    fix_suggestion = "Use ast.literal_eval() for safe evaluation or refactor to avoid eval."
    cwe_id = "CWE-95"


@register
class ExecUsageRule(RegexRule):
    rule_id = "PY-S02"
    name = "exec() usage"
    description = "exec() executes arbitrary code and is a security risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\bexec\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Refactor to avoid dynamic code execution."
    cwe_id = "CWE-95"


@register
class SqlStringFormatRule(RegexRule):
    rule_id = "PY-S03"
    name = "SQL string formatting"
    description = "String formatting in SQL query creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""(?:execute|cursor\.execute|\.raw)\s*\(\s*(?:f["']|["'].*?%|["'].*?\.format)"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use parameterized queries: cursor.execute('SELECT ... WHERE id=%s', (id,))."
    cwe_id = "CWE-89"


@register
class PickleUsageRule(RegexRule):
    rule_id = "PY-S04"
    name = "Pickle deserialization"
    description = "pickle.load/loads can execute arbitrary code from untrusted data."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"pickle\.(?:load|loads)\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use JSON or msgpack for serialization. Never unpickle untrusted data."
    cwe_id = "CWE-502"


@register
class HardcodedPasswordRule(RegexRule):
    rule_id = "PY-S05"
    name = "Hardcoded password"
    description = "Possible hardcoded password or secret in source code."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""(?i)(?:password|secret|api_?key|token|private_?key)\s*=\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*#|\.example|test|mock|fake|placeholder"
    fix_suggestion = "Use environment variables: os.environ['SECRET_KEY']."
    cwe_id = "CWE-798"


@register
class ShellInjectionRule(RegexRule):
    rule_id = "PY-S06"
    name = "Shell injection risk"
    description = "subprocess with shell=True or os.system() creates shell injection risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"subprocess\.\w+\(.*shell\s*=\s*True"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use subprocess.run() with a list of args and shell=False."
    cwe_id = "CWE-78"


@register
class OsSystemRule(RegexRule):
    rule_id = "PY-S07"
    name = "os.system() usage"
    description = "os.system() executes shell commands and is vulnerable to injection."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\bos\.system\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use subprocess.run() with argument list instead."
    cwe_id = "CWE-78"


@register
class OsPopenRule(RegexRule):
    rule_id = "PY-S08"
    name = "os.popen() deprecated"
    description = "os.popen() is deprecated and vulnerable to shell injection."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\bos\.popen\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use subprocess.run() with capture_output=True."
    cwe_id = "CWE-78"


@register
class YamlUnsafeLoadRule(RegexRule):
    rule_id = "PY-S09"
    name = "YAML unsafe load"
    description = "yaml.load() without SafeLoader can execute arbitrary code."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\byaml\.load\s*\("
    exclude_pattern = r"^\s*#|Loader\s*="
    fix_suggestion = "Use yaml.safe_load() or pass Loader=yaml.SafeLoader."
    cwe_id = "CWE-502"


@register
class Jinja2NoAutoescapeRule(RegexRule):
    rule_id = "PY-S10"
    name = "Jinja2 without autoescape"
    description = "Jinja2 Environment without autoescape enables XSS."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"Environment\s*\((?!.*autoescape)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Set autoescape=True in Jinja2 Environment."
    cwe_id = "CWE-79"


@register
class PathTraversalRule(RegexRule):
    rule_id = "PY-S11"
    name = "Path traversal via user input"
    description = "File open with user-controlled path creates traversal risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"open\s*\(.*(?:request\.|input\(|argv)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Validate paths with os.path.realpath() + allowlist check."
    cwe_id = "CWE-22"


@register
class WeakHashRule(RegexRule):
    rule_id = "PY-S12"
    name = "Weak hash for passwords"
    description = "MD5/SHA1 are cryptographically broken for password hashing."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\bhashlib\.(md5|sha1)\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use bcrypt, scrypt, or argon2 for password hashing."
    cwe_id = "CWE-328"


@register
class InsecureRandomRule(RegexRule):
    rule_id = "PY-S13"
    name = "Insecure random for security"
    description = "random module is not cryptographically secure."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\brandom\.(random|randint|choice|randrange)\s*\("
    exclude_pattern = r"^\s*#|test_|seed"
    fix_suggestion = "Use secrets module for security-sensitive randomness."
    cwe_id = "CWE-330"


@register
class SslVerifyDisabledRule(RegexRule):
    rule_id = "PY-S14"
    name = "SSL verification disabled"
    description = "Disabling SSL verification allows MITM attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"verify\s*=\s*False"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Never disable SSL verification in production."
    cwe_id = "CWE-295"


@register
class XxeRule(RegexRule):
    rule_id = "PY-S15"
    name = "XML parsing (XXE risk)"
    description = "Standard XML parser may be vulnerable to XXE attacks."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"\bxml\.etree\.ElementTree\.parse\s*\(|\.fromstring\s*\("
    exclude_pattern = r"^\s*#|defusedxml"
    fix_suggestion = "Use defusedxml library to prevent XXE attacks."
    cwe_id = "CWE-611"


@register
class RegexDosRule(RegexRule):
    rule_id = "PY-S16"
    name = "Regex DoS risk"
    description = "Complex regex with nested quantifiers can cause catastrophic backtracking."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.PYTHON]
    pattern = r"""re\.(compile|match|search|findall)\s*\(["'].*(\.\*){2,}"""
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Simplify regex; avoid nested quantifiers like (.*)*."
    cwe_id = "CWE-1333"


# ===========================================================================
# QUALITY / RELIABILITY (PY-Q01 .. PY-Q14)
# ===========================================================================

@register
class BareExceptRule(RegexRule):
    rule_id = "PY-Q01"
    name = "Bare except"
    description = "Bare 'except:' catches all exceptions including SystemExit and KeyboardInterrupt."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.PYTHON]
    pattern = r"^\s*except\s*:"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use 'except Exception:' to avoid catching SystemExit/KeyboardInterrupt."


@register
class AssertInProductionRule(RegexRule):
    rule_id = "PY-Q02"
    name = "Assert in production"
    description = "assert statements are removed with python -O. Don't use for validation."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.PYTHON]
    pattern = r"^\s*assert\s+(?!.*#\s*noqa)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use 'if not condition: raise ValueError(...)' for production validation."

    def check(self, ctx: FileContext) -> list[Issue]:
        if "test" in ctx.path.name.lower():
            return []
        return super().check(ctx)


@register
class MutableDefaultArgRule(BaseRule):
    rule_id = "PY-Q03"
    name = "Mutable default argument"
    description = "Mutable default argument (list, dict, set) is shared between calls."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.PYTHON]

    _PATTERN = re.compile(r"def\s+\w+\s*\([^)]*=\s*(\[\]|\{\}|set\(\))")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            if self._PATTERN.search(line):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="Mutable default argument. Use None and assign inside function.",
                    suggestion="def func(items=None): items = items or []",
                ))
        return issues


@register
class StarImportRule(RegexRule):
    rule_id = "PY-Q04"
    name = "Star import"
    description = "Wildcard import pollutes namespace and makes dependencies unclear."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]
    pattern = r"^from\s+\S+\s+import\s+\*"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Import specific names: from module import Class1, func2."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name == "__init__.py":
            return []
        return super().check(ctx)


@register
class FunctionTooLongRule(BaseRule):
    rule_id = "PY-Q05"
    name = "Function too long"
    description = "Function exceeds 100 lines."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _DEF = re.compile(r"^(\s*)def\s+(\w+)\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        i = 0
        while i < len(ctx.lines):
            match = self._DEF.match(ctx.lines[i])
            if match:
                indent = len(match.group(1))
                func_name = match.group(2)
                start = i
                j = i + 1
                while j < len(ctx.lines):
                    line = ctx.lines[j]
                    if line.strip() and not line.strip().startswith("#"):
                        line_indent = len(line) - len(line.lstrip())
                        if line_indent <= indent and self._DEF.match(line):
                            break
                        if line_indent <= indent and not line.strip().startswith(("@", ")", "]")):
                            if j > start + 1:
                                break
                    j += 1
                length = j - start
                if length > 100:
                    issues.append(self._make_issue(
                        ctx, line=start + 1,
                        message=f"Function '{func_name}' is {length} lines (max 100).",
                        suggestion="Break into smaller functions.",
                    ))
            i += 1
        return issues


@register
class NoTypeHintsRule(BaseRule):
    rule_id = "PY-Q06"
    name = "Missing return type hint"
    description = "Public function without return type annotation."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.PYTHON]

    _DEF = re.compile(r"^def\s+(?!_)(\w+)\s*\([^)]*\)\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            match = self._DEF.match(line.strip())
            if match and "->" not in line:
                issues.append(self._make_issue(
                    ctx, line=i,
                    message=f"Function '{match.group(1)}' has no return type annotation.",
                    suggestion="Add return type: def func() -> ReturnType:",
                ))
        return issues


@register
class BroadExceptionRule(RegexRule):
    rule_id = "PY-Q07"
    name = "Broad exception catch"
    description = "Catching Exception without 'as' variable makes debugging difficult."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PYTHON]
    pattern = r"^\s*except\s+Exception\s*:"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use 'except Exception as e:' to capture the exception for logging."


@register
class GlobalStatementRule(RegexRule):
    rule_id = "PY-Q08"
    name = "Global statement usage"
    description = "global keyword creates hidden coupling and makes testing difficult."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]
    pattern = r"^\s*global\s+\w+"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Pass state explicitly via parameters or use a class."


@register
class TodoFixmeRule(RegexRule):
    rule_id = "PY-Q09"
    name = "TODO/FIXME in code"
    description = "TODO or FIXME comment indicates unfinished work."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]
    pattern = r"#\s*(?:TODO|FIXME|HACK|XXX)\b"
    fix_suggestion = "Address the TODO or create a tracking issue."


@register
class PrintInProductionRule(RegexRule):
    rule_id = "PY-Q10"
    name = "print() in production"
    description = "print() should not be in production code. Use logging module."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.PYTHON]
    pattern = r"^\s*print\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use logging module: logging.info(), logging.debug()."

    def check(self, ctx: FileContext) -> list[Issue]:
        name = ctx.path.name.lower()
        if any(s in name for s in ["test_", "_test.py", "__main__", "conftest"]):
            return []
        return super().check(ctx)


@register
class EmptyExceptPassRule(RegexRule):
    rule_id = "PY-Q11"
    name = "Empty except with pass"
    description = "except block with only 'pass' silently swallows errors."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.PYTHON]
    pattern = r"except\s*.*:\s*\n\s*pass\s*$"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Log the exception or handle it explicitly."


@register
class NestedFunctionTooDeepRule(BaseRule):
    rule_id = "PY-Q12"
    name = "Nested function too deep"
    description = "Function defined inside another function more than 2 levels deep."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _DEF = re.compile(r"^(\s*)def\s+(\w+)\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        def_stack: list[int] = []
        for i, line in enumerate(ctx.lines, start=1):
            match = self._DEF.match(line)
            if match:
                indent = len(match.group(1))
                while def_stack and def_stack[-1] >= indent:
                    def_stack.pop()
                def_stack.append(indent)
                if len(def_stack) > 2:
                    issues.append(self._make_issue(
                        ctx, line=i,
                        message=f"Function '{match.group(2)}' nested {len(def_stack)} levels deep (max 2).",
                        suggestion="Extract inner functions to module level or a class.",
                    ))
        return issues


@register
class CyclomaticComplexityRule(BaseRule):
    rule_id = "PY-Q13"
    name = "High cyclomatic complexity"
    description = "Function has high cyclomatic complexity."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _DEF = re.compile(r"^(\s*)def\s+(\w+)\s*\(")
    _BRANCH = re.compile(r"\b(if|elif|for|while|except|and|or)\b")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        i = 0
        while i < len(ctx.lines):
            match = self._DEF.match(ctx.lines[i])
            if match:
                indent = len(match.group(1))
                func_name = match.group(2)
                start = i
                complexity = 1
                j = i + 1
                while j < len(ctx.lines):
                    line = ctx.lines[j]
                    if line.strip() and not line.strip().startswith("#"):
                        line_indent = len(line) - len(line.lstrip())
                        if line_indent <= indent and j > start + 1:
                            break
                    complexity += len(self._BRANCH.findall(line))
                    j += 1
                if complexity > 15:
                    issues.append(self._make_issue(
                        ctx, line=start + 1,
                        message=f"Function '{func_name}' has cyclomatic complexity {complexity} (max 15).",
                        suggestion="Simplify or split into smaller functions.",
                    ))
                i = j
                continue
            i += 1
        return issues


@register
class TooManyArgsRule(BaseRule):
    rule_id = "PY-Q14"
    name = "Too many arguments"
    description = "Function has more than 7 parameters."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.PYTHON]

    _DEF = re.compile(r"def\s+(\w+)\s*\(([^)]*)\)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for match in self._DEF.finditer(ctx.content):
            params = match.group(2).strip()
            if not params:
                continue
            param_list = [p.strip() for p in params.split(",") if p.strip() and p.strip() != "self" and p.strip() != "cls"]
            if len(param_list) > 7:
                line = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line,
                    message=f"Function '{match.group(1)}' has {len(param_list)} parameters (max 7).",
                    suggestion="Use a dataclass or **kwargs to group parameters.",
                ))
        return issues


# ===========================================================================
# PERFORMANCE (PY-P01 .. PY-P05)
# ===========================================================================

@register
class FStringInLoggingRule(RegexRule):
    rule_id = "PY-P01"
    name = "f-string in logging"
    description = "f-string in logging call evaluates even when log level is disabled."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"(?:logging\.\w+|logger\.\w+)\s*\(\s*f[\"']"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use lazy formatting: logger.info('msg %s', value)."


@register
class LenInRangeRule(RegexRule):
    rule_id = "PY-P02"
    name = "len() in range()"
    description = "for i in range(len(x)) is unpythonic."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"for\s+\w+\s+in\s+range\s*\(\s*len\s*\("
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use 'for i, item in enumerate(collection)' instead."


@register
class ReCompileInLoopRule(BaseRule):
    rule_id = "PY-P03"
    name = "re.compile inside loop"
    description = "Compiling regex inside a loop is wasteful."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]

    _LOOP = re.compile(r"^\s*(for|while)\s+")
    _COMPILE = re.compile(r"re\.compile\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_indent = 0
        for i, line in enumerate(ctx.lines, start=1):
            match = self._LOOP.match(line)
            if match:
                in_loop = True
                loop_indent = len(line) - len(line.lstrip())
            elif in_loop and line.strip():
                cur_indent = len(line) - len(line.lstrip())
                if cur_indent <= loop_indent:
                    in_loop = False
            if in_loop and self._COMPILE.search(line):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="re.compile() inside loop. Move outside for efficiency.",
                    suggestion="Compile regex at module level: PATTERN = re.compile(...).",
                ))
        return issues


@register
class ImportInsideFunctionRule(RegexRule):
    rule_id = "PY-P04"
    name = "Import inside function"
    description = "Import inside function runs on every call. Move to module level."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]
    pattern = r"^\s{4,}import\s+\w+|^\s{4,}from\s+\w+"
    exclude_pattern = r"^\s*#|TYPE_CHECKING"
    fix_suggestion = "Move imports to module top level."


@register
class StringConcatInLoopRule(BaseRule):
    rule_id = "PY-P05"
    name = "String concat in loop"
    description = "String concatenation with += in loop creates O(n^2) behavior."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.PYTHON]

    _LOOP = re.compile(r"^\s*(for|while)\s+")
    _CONCAT = re.compile(r"\w+\s*\+=\s*[\"'f]|str\s*\+")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_loop = False
        loop_indent = 0
        for i, line in enumerate(ctx.lines, start=1):
            match = self._LOOP.match(line)
            if match:
                in_loop = True
                loop_indent = len(line) - len(line.lstrip())
            elif in_loop and line.strip():
                cur_indent = len(line) - len(line.lstrip())
                if cur_indent <= loop_indent:
                    in_loop = False
            if in_loop and self._CONCAT.search(line):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="String concatenation in loop. Use ''.join() or io.StringIO.",
                    suggestion="Collect strings in a list and join: ''.join(parts).",
                ))
        return issues
