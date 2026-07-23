from __future__ import annotations

import re

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  Security rules (DK-S01 .. DK-S10)
# ===========================================================================


# ---------------------------------------------------------------------------
# DK-S01: Running as root (no USER instruction)
# ---------------------------------------------------------------------------
@register
class NoUserInstructionRule(BaseRule):
    rule_id = "DK-S01"
    name = "Running as root"
    description = "Dockerfile has no USER instruction — container will run as root."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    cwe_id = "CWE-250"

    _USER = re.compile(r"^\s*USER\s+", re.IGNORECASE)
    _FROM = re.compile(r"^\s*FROM\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        has_user = False
        last_from_line = 0
        for i, line in enumerate(ctx.lines, start=1):
            if self._FROM.match(line):
                last_from_line = i
            if self._USER.match(line):
                has_user = True

        if not has_user and last_from_line > 0:
            return [self._make_issue(
                ctx,
                line=last_from_line,
                message="No USER instruction found. Container will run as root.",
                suggestion="Add 'USER <non-root-user>' before CMD/ENTRYPOINT.",
            )]
        return []


# ---------------------------------------------------------------------------
# DK-S02: Using latest tag
# ---------------------------------------------------------------------------
@register
class LatestTagRule(BaseRule):
    rule_id = "DK-S02"
    name = "Using latest tag"
    description = "FROM uses ':latest' or no tag — builds are not reproducible."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]

    _FROM = re.compile(
        r"^\s*FROM\s+(?:--platform=\S+\s+)?(?P<image>\S+)",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            m = self._FROM.match(line)
            if not m:
                continue
            image = m.group("image")
            # skip ARG references like $BASE_IMAGE
            if image.startswith("$") or image.startswith("{"):
                continue
            # "scratch" has no tag
            if image.lower() == "scratch":
                continue
            # Alias references (multi-stage: FROM builder AS final)
            if ":" not in image and "@" not in image:
                # No tag specified
                issues.append(self._make_issue(
                    ctx, line=i,
                    message=f"FROM '{image}' has no tag — defaults to ':latest'.",
                    suggestion="Pin a specific version tag, e.g. 'python:3.12-slim'.",
                ))
            elif image.endswith(":latest"):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message=f"FROM uses ':latest' tag — builds are not reproducible.",
                    suggestion="Pin a specific version tag, e.g. 'node:22-alpine'.",
                ))
        return issues


# ---------------------------------------------------------------------------
# DK-S03: COPY/ADD with wildcard (copies too much)
# ---------------------------------------------------------------------------
@register
class WildcardCopyRule(RegexRule):
    rule_id = "DK-S03"
    name = "Wildcard COPY/ADD"
    description = "COPY/ADD with broad wildcard may include sensitive or unnecessary files."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    pattern = r"^\s*(?:COPY|ADD)\s+(?:--[a-z]+=\S+\s+)*(?:\.\s+\.|[*]+\s)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Copy only the files you need, or ensure a .dockerignore is configured."
    cwe_id = "CWE-200"


# ---------------------------------------------------------------------------
# DK-S04: Exposed sensitive ports (22/SSH, 3389/RDP)
# ---------------------------------------------------------------------------
@register
class SensitivePortRule(RegexRule):
    rule_id = "DK-S04"
    name = "Sensitive port exposed"
    description = "Exposing SSH (22) or RDP (3389) ports is a security risk in containers."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    pattern = r"^\s*EXPOSE\s+.*\b(22|3389)\b"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Remove SSH/RDP port exposure. Use 'docker exec' for container access."
    cwe_id = "CWE-284"


# ---------------------------------------------------------------------------
# DK-S05: Hardcoded secrets in ENV/ARG
# ---------------------------------------------------------------------------
@register
class HardcodedSecretInEnvRule(RegexRule):
    rule_id = "DK-S05"
    name = "Hardcoded secret in ENV/ARG"
    description = "ENV or ARG contains a hardcoded password, token, or secret."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    pattern = r"""(?i)^\s*(?:ENV|ARG)\s+\S*(?:PASSWORD|SECRET|TOKEN|API_KEY|PRIVATE_KEY|CLIENT_SECRET)\S*\s*=\s*["']?[^\s"'$]{4,}"""
    exclude_pattern = r"^\s*#|changeme|placeholder|example|TODO"
    fix_suggestion = "Use build secrets (--mount=type=secret) or runtime environment variables."
    cwe_id = "CWE-798"


# ---------------------------------------------------------------------------
# DK-S06: Using ADD instead of COPY
# ---------------------------------------------------------------------------
@register
class AddInsteadOfCopyRule(BaseRule):
    rule_id = "DK-S06"
    name = "ADD instead of COPY"
    description = "ADD has implicit tar extraction and URL fetch. Prefer COPY for local files."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]

    _ADD = re.compile(r"^\s*ADD\s+(?:--[a-z]+=\S+\s+)*(?P<src>\S+)", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._ADD.match(line)
            if not m:
                continue
            src = m.group("src")
            # ADD is acceptable for tar extraction and remote URLs
            if src.startswith("http://") or src.startswith("https://"):
                continue
            if src.endswith((".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tar.xz")):
                continue
            issues.append(self._make_issue(
                ctx, line=i,
                message="Use COPY instead of ADD for local files.",
                suggestion="Replace ADD with COPY unless you need tar auto-extraction.",
            ))
        return issues


# ---------------------------------------------------------------------------
# DK-S07: curl/wget piped to shell
# ---------------------------------------------------------------------------
@register
class CurlPipeShellRule(RegexRule):
    rule_id = "DK-S07"
    name = "Pipe to shell"
    description = "Piping curl/wget to shell is dangerous — scripts cannot be verified."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    pattern = r"(?:curl|wget)\s+[^|]*\|\s*(?:sh|bash|zsh|dash|/bin/sh|/bin/bash)"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Download the script first, verify checksum, then execute."
    cwe_id = "CWE-494"


# ---------------------------------------------------------------------------
# DK-S08: Privileged instructions
# ---------------------------------------------------------------------------
@register
class PrivilegedInstructionRule(RegexRule):
    rule_id = "DK-S08"
    name = "Privileged instruction"
    description = "Using --privileged or --cap-add=ALL disables container isolation."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]
    pattern = r"--privileged|--cap-add\s*=\s*ALL"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Remove --privileged. Grant only required capabilities with --cap-add."
    cwe_id = "CWE-250"


# ---------------------------------------------------------------------------
# DK-S09: Missing HEALTHCHECK
# ---------------------------------------------------------------------------
@register
class MissingHealthcheckRule(BaseRule):
    rule_id = "DK-S09"
    name = "Missing HEALTHCHECK"
    description = "No HEALTHCHECK instruction — orchestrator cannot monitor container health."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]

    _HEALTHCHECK = re.compile(r"^\s*HEALTHCHECK\s+", re.IGNORECASE)
    _FROM = re.compile(r"^\s*FROM\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        has_healthcheck = any(self._HEALTHCHECK.match(line) for line in ctx.lines)
        if has_healthcheck:
            return []

        # Find last FROM line to report on
        last_from = 1
        for i, line in enumerate(ctx.lines, start=1):
            if self._FROM.match(line):
                last_from = i

        return [self._make_issue(
            ctx,
            line=last_from,
            message="No HEALTHCHECK instruction found.",
            suggestion="Add HEALTHCHECK --interval=30s CMD curl -f http://localhost/ || exit 1",
        )]


# ---------------------------------------------------------------------------
# DK-S10: apt-get without --no-install-recommends
# ---------------------------------------------------------------------------
@register
class AptGetNoInstallRecommendsRule(BaseRule):
    rule_id = "DK-S10"
    name = "apt-get without --no-install-recommends"
    description = "apt-get install without --no-install-recommends pulls unnecessary packages."
    severity = Severity.LOW
    category = Category.SECURITY
    languages = [Language.DOCKERFILE]

    _APT_INSTALL = re.compile(r"apt-get\s+install\b")
    _NO_RECOMMENDS = re.compile(r"--no-install-recommends")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            if self._APT_INSTALL.search(line) and not self._NO_RECOMMENDS.search(line):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="apt-get install without --no-install-recommends.",
                    suggestion="Add --no-install-recommends to reduce image size.",
                ))
        return issues


# ===========================================================================
#  Quality rules (DK-Q01 .. DK-Q10)
# ===========================================================================


# ---------------------------------------------------------------------------
# DK-Q01: Missing .dockerignore
# ---------------------------------------------------------------------------
@register
class MissingDockerignoreRule(BaseRule):
    rule_id = "DK-Q01"
    name = "Missing .dockerignore"
    description = "No .dockerignore file found alongside Dockerfile."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    def check(self, ctx: FileContext) -> list[Issue]:
        dockerfile_dir = ctx.path.parent
        dockerignore = dockerfile_dir / ".dockerignore"
        if not dockerignore.exists():
            return [self._make_issue(
                ctx, line=1,
                message="No .dockerignore found in the same directory as Dockerfile.",
                suggestion="Create a .dockerignore to exclude node_modules, .git, etc.",
            )]
        return []


# ---------------------------------------------------------------------------
# DK-Q02: Multiple CMD instructions
# ---------------------------------------------------------------------------
@register
class MultipleCmdRule(BaseRule):
    rule_id = "DK-Q02"
    name = "Multiple CMD instructions"
    description = "Only the last CMD takes effect. Multiple CMDs indicate a misconfiguration."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _CMD = re.compile(r"^\s*CMD\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        cmd_lines: list[int] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._CMD.match(line):
                cmd_lines.append(i)
        if len(cmd_lines) > 1:
            for line_num in cmd_lines[:-1]:
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="Multiple CMD instructions — only the last one takes effect.",
                    suggestion="Remove duplicate CMD instructions, keep only the final one.",
                ))
        return issues


# ---------------------------------------------------------------------------
# DK-Q03: Multiple ENTRYPOINT instructions
# ---------------------------------------------------------------------------
@register
class MultipleEntrypointRule(BaseRule):
    rule_id = "DK-Q03"
    name = "Multiple ENTRYPOINT instructions"
    description = "Only the last ENTRYPOINT takes effect. Multiple ones indicate a misconfiguration."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _ENTRYPOINT = re.compile(r"^\s*ENTRYPOINT\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        ep_lines: list[int] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._ENTRYPOINT.match(line):
                ep_lines.append(i)
        if len(ep_lines) > 1:
            for line_num in ep_lines[:-1]:
                issues.append(self._make_issue(
                    ctx, line=line_num,
                    message="Multiple ENTRYPOINT instructions — only the last one takes effect.",
                    suggestion="Remove duplicate ENTRYPOINT instructions, keep only the final one.",
                ))
        return issues


# ---------------------------------------------------------------------------
# DK-Q04: Shell form instead of exec form for CMD/ENTRYPOINT
# ---------------------------------------------------------------------------
@register
class ShellFormRule(BaseRule):
    rule_id = "DK-Q04"
    name = "Shell form CMD/ENTRYPOINT"
    description = "CMD/ENTRYPOINT in shell form does not receive signals properly."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _SHELL_FORM = re.compile(
        r"^\s*(?:CMD|ENTRYPOINT)\s+(?!\[)(.+)",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._SHELL_FORM.match(line)
            if m:
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="CMD/ENTRYPOINT uses shell form — PID 1 will be /bin/sh, not your process.",
                    suggestion='Use exec form: CMD ["executable", "arg1", "arg2"].',
                ))
        return issues


# ---------------------------------------------------------------------------
# DK-Q05: Missing WORKDIR
# ---------------------------------------------------------------------------
@register
class MissingWorkdirRule(BaseRule):
    rule_id = "DK-Q05"
    name = "Missing WORKDIR"
    description = "No WORKDIR instruction — files will be placed in the root directory."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _WORKDIR = re.compile(r"^\s*WORKDIR\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        has_workdir = any(self._WORKDIR.match(line) for line in ctx.lines)
        if not has_workdir:
            return [self._make_issue(
                ctx, line=1,
                message="No WORKDIR instruction found — commands run in /.",
                suggestion="Add WORKDIR /app (or another appropriate directory).",
            )]
        return []


# ---------------------------------------------------------------------------
# DK-Q06: Too many RUN instructions (layer bloat)
# ---------------------------------------------------------------------------
@register
class TooManyRunLayersRule(BaseRule):
    rule_id = "DK-Q06"
    name = "Too many RUN instructions"
    description = "Excessive RUN instructions create unnecessary image layers."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _RUN = re.compile(r"^\s*RUN\s+", re.IGNORECASE)
    _MAX_RUN = 10

    def check(self, ctx: FileContext) -> list[Issue]:
        run_lines: list[int] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._RUN.match(line):
                run_lines.append(i)

        if len(run_lines) > self._MAX_RUN:
            return [self._make_issue(
                ctx,
                line=run_lines[0],
                message=f"Dockerfile has {len(run_lines)} RUN instructions (recommended max {self._MAX_RUN}).",
                suggestion="Combine related RUN instructions with '&&' to reduce layers.",
            )]
        return []


# ---------------------------------------------------------------------------
# DK-Q07: Separate apt-get update and apt-get install
# ---------------------------------------------------------------------------
@register
class SeparateAptGetUpdateRule(BaseRule):
    rule_id = "DK-Q07"
    name = "Separate apt-get update and install"
    description = "apt-get update in a separate RUN from apt-get install causes stale cache."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _RUN_APT_UPDATE = re.compile(r"^\s*RUN\s+.*apt-get\s+update", re.IGNORECASE)
    _APT_INSTALL = re.compile(r"apt-get\s+install", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if self._RUN_APT_UPDATE.match(line) and not self._APT_INSTALL.search(line):
                # Check for continuation lines (backslash)
                full_cmd = line
                j = i
                while full_cmd.rstrip().endswith("\\") and j < len(ctx.lines):
                    full_cmd += ctx.lines[j]
                    j += 1
                if not self._APT_INSTALL.search(full_cmd):
                    issues.append(self._make_issue(
                        ctx, line=i,
                        message="'apt-get update' in a separate RUN — install cache may be stale.",
                        suggestion="Combine: RUN apt-get update && apt-get install -y ... && rm -rf /var/lib/apt/lists/*",
                    ))
        return issues


# ---------------------------------------------------------------------------
# DK-Q08: Missing cache cleanup after apt-get
# ---------------------------------------------------------------------------
@register
class MissingCacheCleanupRule(BaseRule):
    rule_id = "DK-Q08"
    name = "Missing apt cache cleanup"
    description = "apt-get install without cache cleanup bloats final image."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _RUN_APT_INSTALL = re.compile(r"^\s*RUN\s+.*apt-get\s+install", re.IGNORECASE)
    _CLEANUP = re.compile(r"rm\s+-rf\s+/var/lib/apt|apt-get\s+clean", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if not self._RUN_APT_INSTALL.match(line):
                continue
            # Gather full multi-line RUN instruction
            full_cmd = line
            j = i  # 0-based index = i (since i is 1-based)
            while full_cmd.rstrip().endswith("\\") and j < len(ctx.lines):
                full_cmd += ctx.lines[j]
                j += 1
            if not self._CLEANUP.search(full_cmd):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message="apt-get install without cache cleanup in the same layer.",
                    suggestion="Add '&& rm -rf /var/lib/apt/lists/*' at the end of the RUN instruction.",
                ))
        return issues


# ---------------------------------------------------------------------------
# DK-Q09: Deprecated MAINTAINER instruction
# ---------------------------------------------------------------------------
@register
class DeprecatedMaintainerRule(RegexRule):
    rule_id = "DK-Q09"
    name = "Deprecated MAINTAINER"
    description = "MAINTAINER is deprecated. Use LABEL maintainer= instead."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]
    pattern = r"^\s*MAINTAINER\s+"
    exclude_pattern = r"^\s*#"
    fix_suggestion = 'Replace with: LABEL maintainer="name <email>"'


# ---------------------------------------------------------------------------
# DK-Q10: Missing LABEL instruction
# ---------------------------------------------------------------------------
@register
class MissingLabelRule(BaseRule):
    rule_id = "DK-Q10"
    name = "Missing LABEL instruction"
    description = "No LABEL instruction — image lacks metadata."
    severity = Severity.INFO
    category = Category.QUALITY
    languages = [Language.DOCKERFILE]

    _LABEL = re.compile(r"^\s*LABEL\s+", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        has_label = any(self._LABEL.match(line) for line in ctx.lines)
        if not has_label:
            return [self._make_issue(
                ctx, line=1,
                message="No LABEL instruction found — image has no metadata.",
                suggestion='Add LABEL maintainer="team" version="1.0" description="..."',
            )]
        return []


# ===========================================================================
#  Performance rules (DK-P01 .. DK-P05)
# ===========================================================================


# ---------------------------------------------------------------------------
# DK-P01: Not using multi-stage build for compiled languages
# ---------------------------------------------------------------------------
@register
class MissingMultiStageRule(BaseRule):
    rule_id = "DK-P01"
    name = "Missing multi-stage build"
    description = "Compiled language detected without multi-stage build — final image includes build tools."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.DOCKERFILE]

    _FROM = re.compile(r"^\s*FROM\s+", re.IGNORECASE)
    _COMPILED_INDICATORS = re.compile(
        r"\b(?:dotnet\s+publish|go\s+build|cargo\s+build|javac|mvn\s+package|gradle\s+build|gcc|g\+\+|make\s+build)\b",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        from_count = sum(1 for line in ctx.lines if self._FROM.match(line))
        has_compile = self._COMPILED_INDICATORS.search(ctx.content)

        if has_compile and from_count < 2:
            # Find the build command line for reporting
            for i, line in enumerate(ctx.lines, start=1):
                if self._COMPILED_INDICATORS.search(line):
                    return [self._make_issue(
                        ctx, line=i,
                        message="Build command detected without multi-stage build — final image includes build tools.",
                        suggestion="Use multi-stage build: compile in a builder stage, copy artifacts to a slim runtime stage.",
                    )]
        return []


# ---------------------------------------------------------------------------
# DK-P02: Copying node_modules / vendor into image
# ---------------------------------------------------------------------------
@register
class CopyNodeModulesRule(RegexRule):
    rule_id = "DK-P02"
    name = "Copying dependency directories"
    description = "Copying node_modules, vendor, or .venv into the image defeats dependency caching."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.DOCKERFILE]
    pattern = r"^\s*(?:COPY|ADD)\s+.*\b(node_modules|vendor|\.venv|__pycache__|packages)\b"
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Add these directories to .dockerignore. Install dependencies inside the image."


# ---------------------------------------------------------------------------
# DK-P03: Not leveraging build cache (dependency files should be copied first)
# ---------------------------------------------------------------------------
@register
class BuildCacheOrderRule(BaseRule):
    rule_id = "DK-P03"
    name = "Sub-optimal build cache usage"
    description = "Source files copied before dependency manifest — cache is busted on every code change."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.DOCKERFILE]

    _COPY_ALL = re.compile(r"^\s*COPY\s+(?:--[a-z]+=\S+\s+)*\.\s+", re.IGNORECASE)
    _COPY_MANIFEST = re.compile(
        r"^\s*COPY\s+(?:--[a-z]+=\S+\s+)*(?:package\*\.json|requirements\.txt|Gemfile|go\.mod|.*\.csproj|pom\.xml|Cargo\.toml|composer\.json)\b",
        re.IGNORECASE,
    )
    _RUN_INSTALL = re.compile(
        r"^\s*RUN\s+.*\b(?:npm\s+(?:install|ci)|yarn\s+install|pip\s+install|dotnet\s+restore|go\s+mod\s+download|bundle\s+install|composer\s+install|cargo\s+build)\b",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        copy_all_line: int | None = None
        manifest_line: int | None = None
        install_line: int | None = None

        for i, line in enumerate(ctx.lines, start=1):
            if self._COPY_ALL.match(line) and copy_all_line is None:
                copy_all_line = i
            if self._COPY_MANIFEST.match(line) and manifest_line is None:
                manifest_line = i
            if self._RUN_INSTALL.match(line) and install_line is None:
                install_line = i

        # If COPY . happens before COPY package.json (or there is no manifest copy at all)
        if copy_all_line and install_line:
            if manifest_line is None or copy_all_line < manifest_line:
                return [self._make_issue(
                    ctx,
                    line=copy_all_line,
                    message="Source copied before dependency manifest — build cache busted on every code change.",
                    suggestion="COPY dependency manifest first, RUN install, then COPY remaining source.",
                )]
        return []


# ---------------------------------------------------------------------------
# DK-P04: Installing dev dependencies in production image
# ---------------------------------------------------------------------------
@register
class DevDependenciesRule(RegexRule):
    rule_id = "DK-P04"
    name = "Dev dependencies in production"
    description = "Dev dependencies installed in production image increase size and attack surface."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.DOCKERFILE]
    pattern = (
        r"(?:npm\s+install\b(?!.*--(?:production|omit=dev))|"
        r"pip\s+install\b.*-r\s*requirements[-_]?dev|"
        r"composer\s+install\b(?!.*--no-dev)|"
        r"bundle\s+install\b(?!.*--without\s+development))"
    )
    exclude_pattern = r"^\s*#"
    fix_suggestion = "Use 'npm ci --omit=dev', 'pip install -r requirements.txt' (not dev), or equivalent."


# ---------------------------------------------------------------------------
# DK-P05: Large base image
# ---------------------------------------------------------------------------
@register
class LargeBaseImageRule(BaseRule):
    rule_id = "DK-P05"
    name = "Large base image"
    description = "Using a full OS base image — prefer slim or alpine variants."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.DOCKERFILE]

    _FROM = re.compile(
        r"^\s*FROM\s+(?:--platform=\S+\s+)?(?P<image>\S+)",
        re.IGNORECASE,
    )

    _LARGE_IMAGES = re.compile(
        r"^(?:ubuntu|debian|centos|fedora|amazonlinux|oraclelinux)(?::|$)",
        re.IGNORECASE,
    )
    _ALREADY_SLIM = re.compile(r"(?:slim|alpine|distroless|busybox|scratch)", re.IGNORECASE)

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            m = self._FROM.match(line)
            if not m:
                continue
            image = m.group("image")
            if self._ALREADY_SLIM.search(image):
                continue
            if self._LARGE_IMAGES.match(image):
                issues.append(self._make_issue(
                    ctx, line=i,
                    message=f"Large base image '{image}' — consider a slim or alpine variant.",
                    suggestion="Use an alpine or slim variant (e.g. 'python:3.12-slim', 'node:22-alpine').",
                ))
        return issues
