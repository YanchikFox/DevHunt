from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  React / Next.js rules (RN-*)
#  Universal rules for any React or Next.js project.
# ===========================================================================


# ---------------------------------------------------------------------------
# RN-S01: dangerouslySetInnerHTML without sanitization
# ---------------------------------------------------------------------------
@register
class DangerousInnerHtmlRule(BaseRule):
    rule_id = "RN-S01"
    name = "dangerouslySetInnerHTML"
    description = "dangerouslySetInnerHTML without sanitization — XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    cwe_id = "CWE-79"

    _DANGEROUS = re.compile(r"dangerouslySetInnerHTML")
    _SANITIZE = re.compile(r"DOMPurify|sanitize|purify|xss", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._DANGEROUS.search(line):
                start = max(0, i - 5)
                end = min(len(ctx.lines), i + 6)
                window = "\n".join(ctx.lines[start:end])
                if not self._SANITIZE.search(window):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="dangerouslySetInnerHTML without DOMPurify/sanitizer.",
                        suggestion="Sanitize with DOMPurify.sanitize() before rendering.",
                    ))
        return issues


# ---------------------------------------------------------------------------
# RN-S02: Exposing server secrets in client components
# ---------------------------------------------------------------------------
@register
class ServerSecretInClientRule(BaseRule):
    rule_id = "RN-S02"
    name = "Server secret in client component"
    description = "process.env without NEXT_PUBLIC_ prefix in client component leaks server secrets."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    cwe_id = "CWE-200"

    _USE_CLIENT = re.compile(r"""^["']use client["']""")
    _SERVER_ENV = re.compile(r"process\.env\.(?!NEXT_PUBLIC_|NODE_ENV)\w+")

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.lines:
            return []
        # Only applies to files with 'use client' directive
        if not self._USE_CLIENT.search(ctx.lines[0].strip()):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if line.strip().startswith("//"):
                continue
            match = self._SERVER_ENV.search(line)
            if match:
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message=f"Server-only env var '{match.group(0)}' in 'use client' component.",
                    suggestion="Use NEXT_PUBLIC_ prefix for client-side env vars, or move to a server component.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-S03: Unsafe href in Link (javascript: protocol)
# ---------------------------------------------------------------------------
@register
class UnsafeHrefRule(RegexRule):
    rule_id = "RN-S03"
    name = "Unsafe href"
    description = "javascript: protocol in href creates XSS risk."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""href\s*=\s*["']javascript:"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Remove javascript: URIs. Use onClick handlers instead."
    cwe_id = "CWE-79"


# ---------------------------------------------------------------------------
# RN-Q01: useState with complex object (should use useReducer)
# ---------------------------------------------------------------------------
@register
class ComplexUseStateRule(BaseRule):
    rule_id = "RN-Q01"
    name = "Complex useState"
    description = "useState with deeply nested object — consider useReducer."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]

    _USE_STATE = re.compile(r"useState\s*<\s*\{[^}]*\{")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._USE_STATE.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="useState with nested object — consider useReducer for complex state.",
                    suggestion="Use useReducer for objects with >3 fields or nested structures.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-Q02: useEffect with missing cleanup
# ---------------------------------------------------------------------------
@register
class UseEffectMissingCleanupRule(BaseRule):
    rule_id = "RN-Q02"
    name = "useEffect missing cleanup"
    description = "useEffect with subscription/listener but no cleanup return."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _SUBSCRIBE = re.compile(
        r"addEventListener|subscribe|setInterval|setTimeout|on\(|socket\."
    )
    _CLEANUP = re.compile(r"return\s*(?:\(\s*\)\s*=>|\(\s*\)\s*\{|function)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        content = ctx.content
        # Find useEffect blocks
        for match in re.finditer(r"useEffect\s*\(", content):
            pos = match.end()
            depth = 1
            while pos < len(content) and depth > 0:
                if content[pos] == "(":
                    depth += 1
                elif content[pos] == ")":
                    depth -= 1
                pos += 1
            block = content[match.start():pos]
            if self._SUBSCRIBE.search(block) and not self._CLEANUP.search(block):
                line_num = content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="useEffect with subscription/listener but no cleanup function.",
                    suggestion="Return a cleanup function to unsubscribe/remove listeners.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-Q03: Direct DOM manipulation in React
# ---------------------------------------------------------------------------
@register
class DirectDomManipulationRule(RegexRule):
    rule_id = "RN-Q03"
    name = "Direct DOM manipulation"
    description = "Direct DOM manipulation bypasses React reconciliation."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"document\.(?:getElementById|querySelector|getElementsBy|createElement)\s*\("
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use React refs (useRef) or state instead of direct DOM manipulation."


# ---------------------------------------------------------------------------
# RN-Q04: Prop drilling (passing >4 props through)
# ---------------------------------------------------------------------------
@register
class PropDrillingRule(BaseRule):
    rule_id = "RN-Q04"
    name = "Excessive props"
    description = "Component receives too many props — consider context or composition."
    severity = Severity.LOW
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]

    _PROPS = re.compile(r"(?:interface|type)\s+\w+Props\s*(?:=\s*)?{([^}]*)}", re.DOTALL)

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        issues: list[Issue] = []
        for match in self._PROPS.finditer(ctx.content):
            body = match.group(1)
            prop_count = len([l for l in body.split("\n") if ":" in l and not l.strip().startswith("//")])
            if prop_count > 10:
                line_num = ctx.content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message=f"Component props interface has {prop_count} props (max 10).",
                    suggestion="Use React Context, composition, or group related props into objects.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-Q05: setState in useEffect without deps (infinite loop)
# ---------------------------------------------------------------------------
@register
class SetStateInEffectNoDepsRule(BaseRule):
    rule_id = "RN-Q05"
    name = "setState in useEffect without deps"
    description = "setState inside useEffect without dependency array causes infinite re-render."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _SET_STATE = re.compile(r"\bset[A-Z]\w*\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        content = ctx.content
        for match in re.finditer(r"useEffect\s*\(", content):
            pos = match.end()
            depth = 1
            while pos < len(content) and depth > 0:
                if content[pos] == "(":
                    depth += 1
                elif content[pos] == ")":
                    depth -= 1
                pos += 1
            call = content[match.start():pos]
            # Check if no dep array (no comma at top level)
            inner = call[call.index("(") + 1:-1]
            cb_depth = 0
            comma_count = 0
            for ch in inner:
                if ch in "({[":
                    cb_depth += 1
                elif ch in ")}]":
                    cb_depth -= 1
                elif ch == "," and cb_depth == 0:
                    comma_count += 1
            if comma_count == 0 and self._SET_STATE.search(call):
                line_num = content[:match.start()].count("\n") + 1
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="setState in useEffect without dependency array — infinite re-render loop.",
                    suggestion="Add a dependency array to useEffect.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-Q06: Conditional hook call
# ---------------------------------------------------------------------------
@register
class ConditionalHookRule(BaseRule):
    rule_id = "RN-Q06"
    name = "Conditional hook call"
    description = "React hooks must not be called conditionally — violates Rules of Hooks."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _HOOK = re.compile(r"\b(use[A-Z]\w*)\s*\(")
    _CONDITION = re.compile(r"^\s*(?:if|else|switch|case|&&|\|\||return\s)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_condition = False
        cond_depth = 0
        for i, line in enumerate(ctx.lines):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            if self._CONDITION.search(stripped) and "{" in stripped:
                in_condition = True
                cond_depth = 0
            if in_condition:
                cond_depth += line.count("{") - line.count("}")
                if cond_depth <= 0:
                    in_condition = False
                    continue
                hook_match = self._HOOK.search(line)
                if hook_match:
                    hook_name = hook_match.group(1)
                    # Skip custom hooks that might be utilities
                    if hook_name in ("useState", "useEffect", "useCallback", "useMemo",
                                     "useRef", "useContext", "useReducer", "useLayoutEffect"):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message=f"Hook '{hook_name}' called conditionally — violates Rules of Hooks.",
                            suggestion="Move hook call to the top level of the component.",
                        ))
        return issues


# ---------------------------------------------------------------------------
# RN-Q07: Missing React.memo for expensive child
# ---------------------------------------------------------------------------
@register
class MissingForwardRefDisplayNameRule(RegexRule):
    rule_id = "RN-Q07"
    name = "Missing displayName on forwardRef"
    description = "forwardRef component without displayName makes debugging harder."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"forwardRef\s*(?:<[^>]*>)?\s*\("
    exclude_pattern = r"displayName"
    fix_suggestion = "Add Component.displayName = 'ComponentName' after forwardRef."


# ---------------------------------------------------------------------------
# RN-Q08: Using index as key in dynamic list
# ---------------------------------------------------------------------------
@register
class ReactIndexKeyRule(RegexRule):
    rule_id = "RN-Q08"
    name = "Index as key"
    description = "Using array index as key causes issues with reordering, inserts, and deletes."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""key\s*=\s*\{\s*(?:index|idx|i)\s*\}"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use a unique identifier (item.id) instead of array index."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# RN-Q09: Hardcoded strings in JSX (i18n)
# ---------------------------------------------------------------------------
@register
class HardcodedJsxStringRule(BaseRule):
    rule_id = "RN-Q09"
    name = "Hardcoded JSX string"
    description = "Hardcoded user-facing string in JSX — use i18n for localization."
    severity = Severity.INFO
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]

    _JSX_TEXT = re.compile(r">\s*[A-ZА-Я][a-zа-яa-zA-Z\s]{10,}\s*<")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        issues: list[Issue] = []
        count = 0
        for i, line in enumerate(ctx.lines):
            if self._JSX_TEXT.search(line):
                count += 1
                if count == 1:
                    first_line = i + 1
        if count > 5:
            issues.append(self._make_issue(
                ctx, line=first_line,
                message=f"File has {count} hardcoded strings in JSX. Consider i18n.",
                suggestion="Use a translation library (next-intl, react-i18next) for user-facing text.",
            ))
        return issues


# ---------------------------------------------------------------------------
# RN-P01: Unnecessary re-renders from new object/array in JSX
# ---------------------------------------------------------------------------
@register
class InlineObjectPropRule(RegexRule):
    rule_id = "RN-P01"
    name = "Inline object in JSX prop"
    description = "Inline object/array literal in JSX prop causes re-renders on every render."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"(?:style|sx|options|data|config|columns|items)\s*=\s*\{\s*(?:\[|\{)"
    exclude_pattern = r"^\s*//|className"
    fix_suggestion = "Extract to useMemo or a constant outside the render."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# RN-P02: Large component without React.memo
# ---------------------------------------------------------------------------
@register
class LargeListItemNoMemoRule(BaseRule):
    rule_id = "RN-P02"
    name = "List item without memo"
    description = "Component used in .map() without React.memo may cause performance issues."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]

    _MAP_COMPONENT = re.compile(r"\.map\s*\([^)]*=>\s*(?:\(\s*)?<([A-Z]\w+)")
    _MEMO = re.compile(r"memo\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            match = self._MAP_COMPONENT.search(line)
            if match:
                component_name = match.group(1)
                # Check if the file exports the component wrapped in memo
                if not self._MEMO.search(ctx.content):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message=f"<{component_name}> used in .map() — consider React.memo for list items.",
                        suggestion=f"Wrap {component_name} with React.memo() to prevent unnecessary re-renders.",
                    ))
                    break  # Only one per file
        return issues


# ---------------------------------------------------------------------------
# RN-P03: Unoptimized images (no next/image)
# ---------------------------------------------------------------------------
@register
class UnoptimizedImageRule(RegexRule):
    rule_id = "RN-P03"
    name = "Unoptimized <img>"
    description = "Using <img> instead of next/image misses automatic optimization."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"<img\s"
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Use <Image> from 'next/image' for automatic optimization, lazy loading, etc."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        # Skip if we're not in a Next.js project (heuristic: check for next imports)
        if "next/" not in ctx.content and "Next" not in ctx.content:
            return []
        return super().check(ctx)


# ---------------------------------------------------------------------------
# RN-P04: Bundle-heavy import
# ---------------------------------------------------------------------------
@register
class BundleHeavyImportRule(RegexRule):
    rule_id = "RN-P04"
    name = "Heavy default import"
    description = "Importing entire library instead of specific module increases bundle size."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]
    pattern = r"""import\s+\w+\s+from\s+["'](?:lodash|moment|date-fns|@mui/material|@mui/icons-material|rxjs|@fortawesome/free-solid-svg-icons)["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Import specific module: import debounce from 'lodash/debounce'."


# ---------------------------------------------------------------------------
# RN-P05: Missing dynamic import for heavy component
# ---------------------------------------------------------------------------
@register
class MissingDynamicImportRule(BaseRule):
    rule_id = "RN-P05"
    name = "Missing dynamic import"
    description = "Heavy third-party component imported statically — use dynamic() or lazy()."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]

    _HEAVY_LIBS = re.compile(
        r"""from\s+["'](?:@monaco-editor|react-quill|chart\.js|recharts|"""
        r"""react-pdf|react-map-gl|three|@react-three|react-draft-wysiwyg|"""
        r"""react-markdown|@uiw/react-md-editor|ace-builds)"""
    )
    _DYNAMIC = re.compile(r"dynamic\s*\(|lazy\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix not in (".tsx", ".ts"):
            return []
        issues: list[Issue] = []
        if self._HEAVY_LIBS.search(ctx.content) and not self._DYNAMIC.search(ctx.content):
            for i, line in enumerate(ctx.lines):
                if self._HEAVY_LIBS.search(line):
                    issues.append(self._make_issue(
                        ctx, line=i + 1,
                        message="Heavy library imported statically — use dynamic() for code splitting.",
                        suggestion="Use next/dynamic or React.lazy() to load heavy components on demand.",
                    ))
                    break
        return issues


# ---------------------------------------------------------------------------
# RN-N01: Async component without Suspense boundary
# ---------------------------------------------------------------------------
@register
class AsyncComponentNoSuspenseRule(BaseRule):
    rule_id = "RN-N01"
    name = "Missing Suspense boundary"
    description = "Async Server Component used without Suspense boundary."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _ASYNC_COMPONENT = re.compile(r"export\s+(?:default\s+)?async\s+function\s+\w+")
    _SUSPENSE = re.compile(r"<Suspense|Suspense>")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        # Only for pages/layouts that use async components
        if ctx.path.name not in ("page.tsx", "layout.tsx"):
            return []
        issues: list[Issue] = []
        if self._ASYNC_COMPONENT.search(ctx.content):
            if not self._SUSPENSE.search(ctx.content):
                for i, line in enumerate(ctx.lines):
                    if self._ASYNC_COMPONENT.search(line):
                        issues.append(self._make_issue(
                            ctx, line=i + 1,
                            message="Async component without <Suspense> — no loading state for users.",
                            suggestion="Wrap async children in <Suspense fallback={<Loading />}>.",
                        ))
                        break
        return issues


# ---------------------------------------------------------------------------
# RN-N02: 'use client' with server-only APIs
# ---------------------------------------------------------------------------
@register
class UseClientWithServerApiRule(BaseRule):
    rule_id = "RN-N02"
    name = "'use client' with server API"
    description = "Client component using server-only APIs (cookies, headers, redirect)."
    severity = Severity.HIGH
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    _USE_CLIENT = re.compile(r"""^["']use client["']""")
    _SERVER_API = re.compile(
        r"""from\s+["']next/headers["']|"""
        r"""from\s+["']next/cookies["']|"""
        r"""\bcookies\s*\(\s*\)|headers\s*\(\s*\)"""
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        if not ctx.lines:
            return []
        if not self._USE_CLIENT.search(ctx.lines[0].strip()):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._SERVER_API.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Server-only API used in 'use client' component — will cause runtime error.",
                    suggestion="Move to a Server Component or use server actions.",
                ))
        return issues


# ---------------------------------------------------------------------------
# RN-N03: Missing loading.tsx for page with data fetching
# ---------------------------------------------------------------------------
@register
class MissingLoadingRule(BaseRule):
    rule_id = "RN-N03"
    name = "Missing loading.tsx"
    description = "Page with async data fetching has no loading.tsx for streaming."
    severity = Severity.INFO
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "page.tsx":
            return []
        if "async" not in ctx.content:
            return []
        loading_file = ctx.path.parent / "loading.tsx"
        if not loading_file.exists():
            return [self._make_issue(
                ctx, line=1,
                message="Async page has no loading.tsx for streaming/loading state.",
                suggestion="Create a loading.tsx file in the same directory.",
            )]
        return []


# ---------------------------------------------------------------------------
# RN-N04: Data fetching in client component (should be server)
# ---------------------------------------------------------------------------
@register
class ClientFetchRule(BaseRule):
    rule_id = "RN-N04"
    name = "Fetch in client component"
    description = "Data fetching in client component — prefer Server Components."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.TYPESCRIPT]

    _USE_CLIENT = re.compile(r"""^["']use client["']""")
    _FETCH_CALL = re.compile(r"\bfetch\s*\(\s*[\"'`]|useQuery\s*\(|useSWR\s*\(")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        if not ctx.lines:
            return []
        if not self._USE_CLIENT.search(ctx.lines[0].strip()):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._FETCH_CALL.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Data fetching in client component — consider moving to Server Component.",
                    suggestion="Use async Server Components for initial data, pass as props to client.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# RN-N05: 'use server' function without input validation
# ---------------------------------------------------------------------------
@register
class ServerActionNoValidationRule(BaseRule):
    rule_id = "RN-N05"
    name = "Server action without validation"
    description = "Server action accepts user input without Zod/validation."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.TYPESCRIPT]
    cwe_id = "CWE-20"

    _USE_SERVER = re.compile(r"""["']use server["']""")
    _VALIDATION = re.compile(r"\.parse\(|\.safeParse\(|validate|schema\.|zod|yup|joi", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        if not self._USE_SERVER.search(ctx.content):
            return []
        # Check if there's any validation
        if self._VALIDATION.search(ctx.content):
            return []
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines):
            if self._USE_SERVER.search(line):
                issues.append(self._make_issue(
                    ctx, line=i + 1,
                    message="Server action with no input validation — validate all incoming data.",
                    suggestion="Use Zod schema.safeParse() to validate server action inputs.",
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# RN-N06: Missing metadata export on page
# ---------------------------------------------------------------------------
@register
class MissingMetadataRule(BaseRule):
    rule_id = "RN-N06"
    name = "Missing page metadata"
    description = "Page has no metadata or generateMetadata export — hurts SEO."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]

    _METADATA = re.compile(r"export\s+(?:const\s+metadata|async\s+function\s+generateMetadata|function\s+generateMetadata)")

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "page.tsx":
            return []
        if not self._METADATA.search(ctx.content):
            return [self._make_issue(
                ctx, line=1,
                message="Page has no metadata export — missing title/description for SEO.",
                suggestion="Export const metadata or generateMetadata() for SEO.",
            )]
        return []


# ---------------------------------------------------------------------------
# RN-N07: Importing from 'next/router' in App Router
# ---------------------------------------------------------------------------
@register
class NextRouterInAppRule(RegexRule):
    rule_id = "RN-N07"
    name = "next/router in App Router"
    description = "next/router is for Pages Router. Use next/navigation in App Router."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""from\s+["']next/router["']"""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Replace with 'next/navigation' (useRouter, usePathname, useSearchParams)."


# ---------------------------------------------------------------------------
# RN-N08: Missing error.tsx for route segment
# ---------------------------------------------------------------------------
@register
class MissingErrorBoundaryNextRule(BaseRule):
    rule_id = "RN-N08"
    name = "Missing error.tsx"
    description = "Route segment with page.tsx has no error.tsx boundary."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.TYPESCRIPT]

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "page.tsx":
            return []
        error_file = ctx.path.parent / "error.tsx"
        if not error_file.exists():
            return [self._make_issue(
                ctx, line=1,
                message="Page has no error.tsx boundary for error handling.",
                suggestion="Create an error.tsx ('use client') in the same directory.",
            )]
        return []


# ---------------------------------------------------------------------------
# RN-N09: Missing not-found.tsx
# ---------------------------------------------------------------------------
@register
class MissingNotFoundRule(BaseRule):
    rule_id = "RN-N09"
    name = "Missing not-found.tsx"
    description = "Dynamic route segment has no not-found.tsx for 404 handling."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.TYPESCRIPT]

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.name != "page.tsx":
            return []
        # Only for dynamic routes (parent dir has [param])
        parent = ctx.path.parent.name
        if not (parent.startswith("[") and parent.endswith("]")):
            return []
        not_found = ctx.path.parent / "not-found.tsx"
        if not not_found.exists():
            return [self._make_issue(
                ctx, line=1,
                message="Dynamic route has no not-found.tsx for 404 handling.",
                suggestion="Create not-found.tsx for a custom 404 page.",
            )]
        return []


# ---------------------------------------------------------------------------
# RN-N10: Direct DB access in Server Component
# ---------------------------------------------------------------------------
@register
class DirectDbInComponentRule(RegexRule):
    rule_id = "RN-N10"
    name = "Direct DB in component"
    description = "Direct database query in component file — use a separate data layer."
    severity = Severity.MEDIUM
    category = Category.MAINTAINABILITY
    languages = [Language.TYPESCRIPT]
    pattern = r"""(?:prisma\.|db\.|mongoose\.|sequelize\.)(?:find|query|select|create|update|delete|aggregate)\s*\("""
    exclude_pattern = r"^\s*//"
    fix_suggestion = "Move data access to a separate service/repository layer."

    def check(self, ctx: FileContext) -> list[Issue]:
        if ctx.path.suffix != ".tsx":
            return []
        return super().check(ctx)
