from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


@register
class BufferOverflowRule(RegexRule):
    rule_id = "CC-S01"
    name = "Buffer overflow risk"
    description = "Unsafe string functions (gets, strcpy, sprintf) can cause buffer overflows."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:gets|strcpy|strcat|sprintf|vsprintf)\s*\("
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use safe alternatives: fgets, strncpy, snprintf, strncat."
    cwe_id = "CWE-120"


@register
class FormatStringRule(RegexRule):
    rule_id = "CC-S02"
    name = "Format string vulnerability"
    description = "User input as format string creates arbitrary read/write risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:printf|fprintf|sprintf|snprintf|syslog)\s*\(\s*(?:argv|input|buf|buffer|user|data|request)"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Always use format string: printf('%s', user_input) not printf(user_input)."
    cwe_id = "CWE-134"


@register
class UseAfterFreeRule(RegexRule):
    rule_id = "CC-S03"
    name = "Potential use-after-free"
    description = "Pointer used after free() without reassignment."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"free\s*\(\s*(\w+)\s*\)\s*;(?!\s*\1\s*=\s*NULL)"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Set pointer to NULL after free: free(ptr); ptr = NULL;"
    cwe_id = "CWE-416"


@register
class MallocWithoutFreeRule(RegexRule):
    rule_id = "CC-Q01"
    name = "malloc without NULL check"
    description = "malloc/calloc return value not checked for NULL."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.C, Language.CPP]
    pattern = r"\w+\s*=\s*(?:malloc|calloc|realloc)\s*\([^)]+\)\s*;"
    exclude_pattern = r"^\s*(?://|/\*|\*)|if\s*\("
    fix_suggestion = "Check return value: if (ptr == NULL) { handle error }."


@register
class HardcodedCredentialsRule(RegexRule):
    rule_id = "CC-S04"
    name = "Hardcoded credentials"
    description = "Possible hardcoded password or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"""(?i)(?:password|secret|api_?key|token)\s*=\s*["'][^"'\s]{8,}["']"""
    exclude_pattern = r"^\s*(?://|/\*|\*)|test|mock|example"
    fix_suggestion = "Use environment variables or secure key storage."
    cwe_id = "CWE-798"


@register
class IntegerOverflowRule(RegexRule):
    rule_id = "CC-S05"
    name = "Integer overflow risk"
    description = "Arithmetic on user input without bounds check can overflow."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"(?:atoi|atol|strtol)\s*\(.*?(?:argv|input|buf|user|data)"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Check bounds after conversion and before arithmetic operations."
    cwe_id = "CWE-190"


@register
class RawNewDeleteRule(RegexRule):
    rule_id = "CC-Q02"
    name = "Raw new/delete"
    description = "Raw new/delete is error-prone. Use smart pointers."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CPP]
    pattern = r"\bnew\s+\w+(?:\s*\[|\s*\()|(?<!\w)delete\s+(?:\[\s*\])?\s*\w+"
    exclude_pattern = r"^\s*(?://|/\*|\*)|make_unique|make_shared"
    fix_suggestion = "Use std::unique_ptr or std::shared_ptr with std::make_unique/make_shared."


@register
class CStyleCastRule(RegexRule):
    rule_id = "CC-Q03"
    name = "C-style cast"
    description = "C-style casts bypass type safety. Use C++ cast operators."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.CPP]
    pattern = r"\(\s*(?:int|char|float|double|long|unsigned|void)\s*\*?\s*\)\s*\w+"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use static_cast<>, dynamic_cast<>, reinterpret_cast<>, or const_cast<>."


@register
class CommandInjectionRule(RegexRule):
    rule_id = "CC-S06"
    name = "Command injection"
    description = "system() or popen() with user input creates injection risk."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:system|popen)\s*\(.*?(?:argv|input|buf|user|data|request)"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Avoid system(). Use exec* family or validate inputs strictly."
    cwe_id = "CWE-78"


@register
class GlobalVariableRule(RegexRule):
    rule_id = "CC-Q04"
    name = "Global mutable variable"
    description = "Global mutable variables create coupling and thread-safety issues."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.C, Language.CPP]
    pattern = r"^(?:static\s+)?(?:int|char|float|double|long|unsigned|bool|std::string|std::vector)\s+\w+\s*[=;]"
    exclude_pattern = r"^\s*(?://|/\*|\*)|const\s|constexpr\s"
    fix_suggestion = "Pass as parameter or encapsulate in a class/struct."


# ---------------------------------------------------------------------------
# CC-S07: Double free
# ---------------------------------------------------------------------------

@register
class DoubleFreeRule(BaseRule):
    rule_id = "CC-S07"
    name = "Double free"
    description = "free() called twice on the same pointer without reassignment in between."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    cwe_id = "CWE-415"
    fix_suggestion = "Set pointer to NULL immediately after free() to prevent double-free."

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        comment_re = re.compile(r"^\s*(?://|/\*|\*)")
        free_re = re.compile(r"\bfree\s*\(\s*(\w+)\s*\)")
        assign_re = re.compile(r"\b(\w+)\s*=")

        freed: dict[str, int] = {}

        for i, line in enumerate(ctx.lines, start=1):
            if comment_re.search(line):
                continue

            assign_match = assign_re.search(line)
            if assign_match:
                var = assign_match.group(1)
                freed.pop(var, None)

            for match in free_re.finditer(line):
                var = match.group(1)
                if var in freed:
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=match.start(),
                        message=f"Double free of pointer '{var}' (first freed at line {freed[var]}).",
                        suggestion=self.fix_suggestion,
                    ))
                else:
                    freed[var] = i

        return issues


# ---------------------------------------------------------------------------
# CC-S08: Race condition (shared mutable state without mutex)
# ---------------------------------------------------------------------------

@register
class RaceConditionRule(BaseRule):
    rule_id = "CC-S08"
    name = "Race condition risk"
    description = "Global or static variable accessed without mutex/lock protection in a multi-threaded context."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    cwe_id = "CWE-362"
    fix_suggestion = "Protect shared mutable state with std::mutex, pthread_mutex, or std::atomic."

    _thread_api_re = re.compile(
        r"\b(?:pthread_create|std::thread|std::async|CreateThread|_beginthread)\b"
    )
    _shared_access_re = re.compile(
        r"^(?:static\s+)?(?:int|char|float|double|long|unsigned|bool)\s+\w+|"
        r"\bg_\w+\s*(?:[+\-*/%]?=|\+\+|--)"
    )
    _lock_re = re.compile(
        r"\b(?:std::lock_guard|std::unique_lock|std::scoped_lock|pthread_mutex_lock|EnterCriticalSection)\b"
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        comment_re = re.compile(r"^\s*(?://|/\*|\*)")

        uses_threads = any(
            self._thread_api_re.search(line)
            for line in ctx.lines
            if not comment_re.search(line)
        )
        if not uses_threads:
            return issues

        for i, line in enumerate(ctx.lines, start=1):
            if comment_re.search(line):
                continue
            if self._lock_re.search(line):
                continue
            match = self._shared_access_re.search(line)
            if match:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=match.start(),
                    message="Shared mutable state accessed without visible mutex/lock in a multi-threaded file.",
                    suggestion=self.fix_suggestion,
                ))
        return issues


# ---------------------------------------------------------------------------
# CC-S09: Insecure random
# ---------------------------------------------------------------------------

@register
class InsecureRandomRule(RegexRule):
    rule_id = "CC-S09"
    name = "Insecure random number generator"
    description = "rand()/srand() produces predictable output and must not be used for security-sensitive purposes."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:rand|srand)\s*\("
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use /dev/urandom, getrandom(), BCryptGenRandom(), or std::random_device for security contexts."
    cwe_id = "CWE-338"


# ---------------------------------------------------------------------------
# CC-S10: Stack buffer overflow via alloca/VLA with variable size
# ---------------------------------------------------------------------------

@register
class StackBufferOverflowRule(RegexRule):
    rule_id = "CC-S10"
    name = "Stack buffer overflow via variable-length allocation"
    description = "alloca() or VLA with a variable size derived from user input can overflow the stack."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:alloca)\s*\((?![^)]*sizeof\s*\()"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use heap allocation (malloc/std::vector) with explicit size validation."
    cwe_id = "CWE-121"


# ---------------------------------------------------------------------------
# CC-S11: Null dereference after allocation
# ---------------------------------------------------------------------------

@register
class NullDereferenceRule(BaseRule):
    rule_id = "CC-S11"
    name = "Potential null dereference"
    description = "Pointer returned by malloc/calloc/realloc is dereferenced without a NULL check."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    cwe_id = "CWE-476"
    fix_suggestion = "Check pointer for NULL before dereferencing: if (ptr == NULL) { ... }"

    _alloc_re = re.compile(r"(\w+)\s*=\s*(?:malloc|calloc|realloc)\s*\([^)]*\)\s*;")
    _null_check_re = re.compile(r"\bif\s*\(\s*(?:!?\s*(?P<var>\w+)\s*(?:==\s*NULL|!=\s*NULL)?)")
    _deref_re = re.compile(r"\b(?P<var>\w+)\s*(?:->|\[|\*)")
    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        unchecked: dict[str, int] = {}

        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue

            alloc_match = self._alloc_re.search(line)
            if alloc_match:
                unchecked[alloc_match.group(1)] = i
                continue

            null_match = self._null_check_re.search(line)
            if null_match and null_match.group("var"):
                unchecked.pop(null_match.group("var"), None)

            deref_match = self._deref_re.search(line)
            if deref_match:
                var = deref_match.group("var")
                if var in unchecked:
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=deref_match.start(),
                        message=f"Pointer '{var}' allocated at line {unchecked[var]} is dereferenced without a NULL check.",
                        suggestion=self.fix_suggestion,
                    ))
                    unchecked.pop(var)

        return issues


# ---------------------------------------------------------------------------
# CC-S12: TOCTOU file operations (access() then open())
# ---------------------------------------------------------------------------

@register
class ToctouFileRule(BaseRule):
    rule_id = "CC-S12"
    name = "TOCTOU race in file operations"
    description = "access() followed by open()/fopen() creates a time-of-check/time-of-use race condition."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    cwe_id = "CWE-367"
    fix_suggestion = "Drop access() check; open the file directly and handle ENOENT/EACCES errno."

    _access_re = re.compile(r"\baccess\s*\(")
    _open_re = re.compile(r"\b(?:open|fopen|creat)\s*\(")
    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        access_lines: list[int] = []

        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue
            if self._access_re.search(line):
                access_lines.append(i)
            elif self._open_re.search(line) and access_lines:
                for access_line in access_lines:
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        column=0,
                        message=f"open/fopen at line {i} follows access() at line {access_line} — TOCTOU race.",
                        suggestion=self.fix_suggestion,
                    ))
                access_lines.clear()

        return issues


# ---------------------------------------------------------------------------
# CC-S13: Insecure temp file
# ---------------------------------------------------------------------------

@register
class InsecureTempFileRule(RegexRule):
    rule_id = "CC-S13"
    name = "Insecure temporary file creation"
    description = "tmpnam()/tempnam() create predictable file names vulnerable to symlink attacks."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.C, Language.CPP]
    pattern = r"\b(?:tmpnam|tempnam)\s*\("
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use mkstemp() (POSIX) or tmpfile() which atomically creates and opens a temp file."
    cwe_id = "CWE-377"


# ---------------------------------------------------------------------------
# CC-Q05: TODO/FIXME comments
# ---------------------------------------------------------------------------

@register
class TodoFixmeRule(RegexRule):
    rule_id = "CC-Q05"
    name = "TODO/FIXME comment"
    description = "TODO or FIXME comment indicates unfinished or known-broken code."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    pattern = r"(?://|/\*)\s*(?:TODO|FIXME|HACK|XXX)\b"
    exclude_pattern = None
    fix_suggestion = "Resolve the TODO/FIXME or track it in the issue tracker and remove the comment."


# ---------------------------------------------------------------------------
# CC-Q06: Magic numbers
# ---------------------------------------------------------------------------

@register
class MagicNumberRule(RegexRule):
    rule_id = "CC-Q06"
    name = "Magic number"
    description = "Unexplained numeric literal in code reduces readability and maintainability."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    pattern = r"(?<![\"'#\w])(?<!\w)\b(?!0\b|1\b)(?:[2-9]\d{1,}|\d{4,})\b(?!\s*[;,\)]?\s*\/\/)"
    exclude_pattern = r"^\s*(?://|/\*|\*)|#define\s|const\s|constexpr\s|enum\s"
    fix_suggestion = "Replace magic number with a named constant (#define, const, or constexpr)."


# ---------------------------------------------------------------------------
# CC-Q07: Function too long (>100 lines, C/C++)
# ---------------------------------------------------------------------------

@register
class FunctionTooLongRule(BaseRule):
    rule_id = "CC-Q07"
    name = "Function too long"
    description = "Function body exceeds 100 lines, making it hard to understand and test."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    fix_suggestion = "Break the function into smaller, focused helper functions."

    _func_start_re = re.compile(
        r"^[\w\s\*&:<>]+\s+\w+\s*\([^;]*\)\s*(?:const\s*|noexcept\s*|override\s*)*\{"
    )
    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        func_start: int | None = None
        brace_depth = 0
        in_func = False

        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue

            open_braces = line.count("{")
            close_braces = line.count("}")

            if not in_func and self._func_start_re.match(line.strip()):
                func_start = i
                in_func = True
                brace_depth = open_braces - close_braces
            elif in_func:
                brace_depth += open_braces - close_braces
                if brace_depth <= 0:
                    length = i - (func_start or i)
                    if length > 100:
                        issues.append(self._make_issue(
                            ctx,
                            line=func_start or i,
                            column=0,
                            message=f"Function starting at line {func_start} is {length} lines long (limit: 100).",
                            suggestion=self.fix_suggestion,
                        ))
                    in_func = False
                    func_start = None
                    brace_depth = 0

        return issues


# ---------------------------------------------------------------------------
# CC-Q08: Deep nesting (>4 levels)
# ---------------------------------------------------------------------------

@register
class DeepNestingRule(BaseRule):
    rule_id = "CC-Q08"
    name = "Deep nesting"
    description = "Code nested more than 4 levels deep is hard to read, test, and maintain."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    fix_suggestion = "Extract deeply nested blocks into helper functions or invert conditions (early return)."

    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")
    _max_depth = 4

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        depth = 0
        reported_lines: set[int] = set()

        for i, line in enumerate(ctx.lines, start=1):
            if self._comment_re.search(line):
                continue
            depth += line.count("{") - line.count("}")
            if depth > self._max_depth and i not in reported_lines:
                reported_lines.add(i)
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    column=0,
                    message=f"Nesting depth {depth} exceeds limit of {self._max_depth}.",
                    suggestion=self.fix_suggestion,
                ))

        return issues


# ---------------------------------------------------------------------------
# CC-Q09: Goto usage
# ---------------------------------------------------------------------------

@register
class GotoRule(RegexRule):
    rule_id = "CC-Q09"
    name = "goto usage"
    description = "goto creates unstructured control flow and hampers readability."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    pattern = r"\bgoto\s+\w+"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Restructure with loops, break/continue, RAII, or extract cleanup into a function."
    cwe_id = "CWE-1120"


# ---------------------------------------------------------------------------
# CC-Q10: Empty catch block (C++)
# ---------------------------------------------------------------------------

@register
class EmptyCatchRule(BaseRule):
    rule_id = "CC-Q10"
    name = "Empty catch block"
    description = "catch block with no body silently swallows exceptions."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.CPP]
    fix_suggestion = "Log or handle the exception; at minimum document why it is intentionally ignored."

    _catch_re = re.compile(r"\bcatch\s*\(")
    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        n = len(lines)

        for i, line in enumerate(lines):
            if self._comment_re.search(line):
                continue
            if not self._catch_re.search(line):
                continue

            # Find the opening brace of the catch body
            brace_pos = -1
            search_start = i
            while search_start < n and brace_pos == -1:
                for ch_idx, ch in enumerate(lines[search_start]):
                    if ch == "{":
                        brace_pos = search_start
                        break
                search_start += 1

            if brace_pos == -1:
                continue

            # Check if body is empty: scan until matching closing brace
            depth = 0
            body_tokens: list[str] = []
            for j in range(brace_pos, min(brace_pos + 20, n)):
                stripped = lines[j].strip()
                if self._comment_re.search(lines[j]):
                    continue
                body_tokens.append(stripped)
                depth += stripped.count("{") - stripped.count("}")
                if depth <= 0 and j > brace_pos:
                    break

            body = " ".join(body_tokens)
            inner = re.sub(r"\{|\}", "", body).strip()
            if not inner or inner in {"", " "}:
                issues.append(self._make_issue(
                    ctx,
                    line=i + 1,
                    column=0,
                    message="Empty catch block silently swallows exceptions.",
                    suggestion=self.fix_suggestion,
                ))

        return issues


# ---------------------------------------------------------------------------
# CC-Q11: Include guard missing
# ---------------------------------------------------------------------------

@register
class IncludeGuardRule(BaseRule):
    rule_id = "CC-Q11"
    name = "Missing include guard"
    description = "Header file lacks #pragma once or #ifndef include guard, risking multiple-inclusion errors."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    fix_suggestion = "Add '#pragma once' at the top of the header, or wrap with '#ifndef / #define / #endif'."

    _pragma_re = re.compile(r"^\s*#\s*pragma\s+once\b")
    _ifndef_re = re.compile(r"^\s*#\s*ifndef\s+\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix.lower() not in {".h", ".hpp", ".hxx", ".hh"}:
            return []

        first_50 = ctx.lines[:50]
        for line in first_50:
            if self._pragma_re.search(line) or self._ifndef_re.search(line):
                return []

        return [self._make_issue(
            ctx,
            line=1,
            column=0,
            message=f"Header '{ctx.path.name}' has no include guard (#pragma once or #ifndef).",
            suggestion=self.fix_suggestion,
        )]


# ---------------------------------------------------------------------------
# CC-Q12: Unused parameter
# ---------------------------------------------------------------------------

@register
class UnusedParameterRule(RegexRule):
    rule_id = "CC-Q12"
    name = "Unused parameter"
    description = "Function parameter is unnamed or explicitly voided without [[maybe_unused]] annotation."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.C, Language.CPP]
    pattern = r"\(void\)\s*\w+"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use [[maybe_unused]] attribute (C++) or omit the parameter name in the definition."


# ---------------------------------------------------------------------------
# CC-Q13: Macro instead of constexpr/inline (C++ specific)
# ---------------------------------------------------------------------------

@register
class MacroInsteadOfConstexprRule(RegexRule):
    rule_id = "CC-Q13"
    name = "Macro instead of constexpr/inline"
    description = "#define used for a constant or function-like macro in C++ code; defeats type safety and scoping."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.CPP]
    pattern = r"^\s*#\s*define\s+[A-Z_][A-Z0-9_]*\s+(?:\d|\"|\()"
    exclude_pattern = r"^\s*(?://|/\*|\*)|_H_\b|_HPP_\b|GUARD"
    fix_suggestion = "Replace with constexpr for constants and inline/template functions for function-like macros."


# ---------------------------------------------------------------------------
# CC-P01: std::endl instead of '\n'
# ---------------------------------------------------------------------------

@register
class StdEndlRule(RegexRule):
    rule_id = "CC-P01"
    name = "std::endl flushes buffer unnecessarily"
    description = "std::endl flushes the output buffer on every call; prefer '\\n' for performance."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CPP]
    pattern = r"<<\s*std::endl\b"
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Replace std::endl with '\\n'. Call std::flush explicitly only when a flush is actually needed."


# ---------------------------------------------------------------------------
# CC-P02: Passing large objects by value
# ---------------------------------------------------------------------------

@register
class PassByValueRule(RegexRule):
    rule_id = "CC-P02"
    name = "Large object passed by value"
    description = "std::string, std::vector, or std::map passed by value causes an unnecessary copy."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.CPP]
    pattern = r"\b(?:std::string|std::vector|std::map|std::unordered_map|std::set|std::list)\s+\w+\s*(?=[,)])"
    exclude_pattern = r"^\s*(?://|/\*|\*)|const\s*&|&&|\breturn\b"
    fix_suggestion = "Pass by const reference (const std::vector<T>&) unless ownership transfer is needed (use move semantics)."


# ---------------------------------------------------------------------------
# CC-P03: push_back without reserve in loop
# ---------------------------------------------------------------------------

@register
class PushBackWithoutReserveRule(BaseRule):
    rule_id = "CC-P03"
    name = "push_back without reserve in loop"
    description = "Calling push_back inside a loop without a prior reserve() causes repeated reallocations."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.CPP]
    fix_suggestion = "Call vec.reserve(expected_size) before the loop to pre-allocate memory."

    _loop_re = re.compile(r"\b(?:for|while)\s*\(")
    _push_re = re.compile(r"\.\s*(?:push_back|emplace_back)\s*\(")
    _reserve_re = re.compile(r"\.\s*reserve\s*\(")
    _comment_re = re.compile(r"^\s*(?://|/\*|\*)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        n = len(lines)

        for i, line in enumerate(lines):
            if self._comment_re.search(line):
                continue
            if not self._loop_re.search(line):
                continue

            # Scan up to 5 lines before for a reserve() call
            pre_block = lines[max(0, i - 5): i]
            has_reserve = any(self._reserve_re.search(l) for l in pre_block)
            if has_reserve:
                continue

            # Scan up to 30 lines inside the loop for push_back
            loop_body = lines[i: min(i + 30, n)]
            for j, body_line in enumerate(loop_body):
                if self._comment_re.search(body_line):
                    continue
                pb_match = self._push_re.search(body_line)
                if pb_match:
                    issues.append(self._make_issue(
                        ctx,
                        line=i + j + 1,
                        column=pb_match.start(),
                        message="push_back/emplace_back inside loop without prior reserve() — may cause repeated reallocations.",
                        suggestion=self.fix_suggestion,
                    ))
                    break  # one issue per loop

        return issues


# ---------------------------------------------------------------------------
# CC-P04: C-string comparison with == instead of strcmp
# ---------------------------------------------------------------------------

@register
class CStringComparisonRule(RegexRule):
    rule_id = "CC-P04"
    name = "C-string compared with =="
    description = "Using == to compare char* pointers compares addresses, not string contents."
    severity = Severity.HIGH
    category = Category.PERFORMANCE
    languages = [Language.C, Language.CPP]
    pattern = r'\bchar\s*\*.*==\s*"[^"]*"|"[^"]*"\s*==\s*\bchar\s*\*'
    exclude_pattern = r"^\s*(?://|/\*|\*)"
    fix_suggestion = "Use strcmp(a, b) == 0 for C-strings, or std::string for C++ to enable == safely."
    cwe_id = "CWE-595"
