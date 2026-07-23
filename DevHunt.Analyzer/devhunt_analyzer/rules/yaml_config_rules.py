from __future__ import annotations

import re
from collections import Counter

from devhunt_analyzer.engine.models import Category, FileContext, Issue, Language, Severity
from devhunt_analyzer.engine.rule_registry import register
from devhunt_analyzer.rules.base import BaseRule, RegexRule


# ===========================================================================
#  Security rules (YC-S01 .. YC-S12)
# ===========================================================================


# ---------------------------------------------------------------------------
# YC-S01: Hardcoded secrets in YAML (password/secret/token/api_key values)
# ---------------------------------------------------------------------------
@register
class HardcodedSecretsYamlRule(BaseRule):
    rule_id = "YC-S01"
    name = "Hardcoded secret in YAML"
    description = "YAML file contains a hardcoded password, secret, token, or API key."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-798"

    _SECRET_KEY = re.compile(
        r"(?i)^[\s-]*(?:password|passwd|secret|token|api[_-]?key|private[_-]?key|"
        r"client[_-]?secret|access[_-]?key|auth[_-]?token|jwt[_-]?secret|"
        r"encryption[_-]?key|signing[_-]?key|database[_-]?password|db[_-]?password)"
        r"\s*:\s*(?P<val>\S.*)",
    )
    _SAFE_VALUE = re.compile(
        r"""^[\s"']*(\$\{|{{|changeme|placeholder|example|CHANGE_ME|"""
        r"<.*>|TODO|FIXME|_REPLACE_|null|~|true|false|\d{1,4}|"
        r"ref\+|vault://|gsm://|arn:aws|azurekeyvault://)",
        re.IGNORECASE,
    )

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue
            m = self._SECRET_KEY.match(line)
            if not m:
                continue
            val = m.group("val").strip().strip("\"'")
            if not val or len(val) < 4:
                continue
            if self._SAFE_VALUE.match(val):
                continue
            issues.append(self._make_issue(
                ctx,
                line=i,
                message=f"Possible hardcoded secret found: value for sensitive key.",
                suggestion=(
                    "Use environment variable references, sealed secrets, "
                    "or a secrets manager (Vault, SOPS, AWS SSM, Azure Key Vault)."
                ),
            ))
        return issues


# ---------------------------------------------------------------------------
# YC-S02: Privileged container in K8s (securityContext.privileged: true)
# ---------------------------------------------------------------------------
@register
class PrivilegedContainerRule(RegexRule):
    rule_id = "YC-S02"
    name = "Privileged container"
    description = "Container runs in privileged mode, disabling all security boundaries."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-250"
    pattern = r"^\s*privileged\s*:\s*true"
    exclude_pattern = r"^\s*#"
    message_template = "Privileged container detected: {match}"
    fix_suggestion = (
        "Remove 'privileged: true'. Grant only specific capabilities "
        "via securityContext.capabilities.add if needed."
    )


# ---------------------------------------------------------------------------
# YC-S03: Container running as root (runAsUser: 0)
# ---------------------------------------------------------------------------
@register
class RunAsRootRule(RegexRule):
    rule_id = "YC-S03"
    name = "Container running as root"
    description = "Container explicitly runs as root user (UID 0)."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-250"
    pattern = r"^\s*runAsUser\s*:\s*0\s*$"
    exclude_pattern = r"^\s*#"
    message_template = "Container configured to run as root (UID 0)."
    fix_suggestion = (
        "Set 'runAsUser' to a non-zero UID and add 'runAsNonRoot: true' "
        "in the securityContext."
    )


# ---------------------------------------------------------------------------
# YC-S04: Host network mode (hostNetwork: true)
# ---------------------------------------------------------------------------
@register
class HostNetworkRule(RegexRule):
    rule_id = "YC-S04"
    name = "Host network mode"
    description = "Pod uses the host network namespace, bypassing network isolation."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-668"
    pattern = r"^\s*hostNetwork\s*:\s*true"
    exclude_pattern = r"^\s*#"
    message_template = "Host network mode enabled: {match}"
    fix_suggestion = (
        "Remove 'hostNetwork: true'. Use Kubernetes Services and "
        "NetworkPolicies for proper network isolation."
    )


# ---------------------------------------------------------------------------
# YC-S05: Host PID namespace (hostPID: true)
# ---------------------------------------------------------------------------
@register
class HostPIDRule(RegexRule):
    rule_id = "YC-S05"
    name = "Host PID namespace"
    description = "Pod shares the host PID namespace, allowing visibility into host processes."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-668"
    pattern = r"^\s*hostPID\s*:\s*true"
    exclude_pattern = r"^\s*#"
    message_template = "Host PID namespace enabled: {match}"
    fix_suggestion = "Remove 'hostPID: true' unless absolutely required for the workload."


# ---------------------------------------------------------------------------
# YC-S06: Writable root filesystem (no readOnlyRootFilesystem)
# ---------------------------------------------------------------------------
@register
class WritableRootFilesystemRule(BaseRule):
    rule_id = "YC-S06"
    name = "Writable root filesystem"
    description = "Container does not set readOnlyRootFilesystem: true, allowing writes to root FS."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-732"

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet|Job|CronJob|Pod)\s*$")
    _READONLY_ROOT = re.compile(r"^\s*readOnlyRootFilesystem\s*:\s*true")
    _CONTAINER = re.compile(r"^\s*-\s*name\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        content = ctx.content

        is_k8s = bool(self._KIND.search(content))
        if not is_k8s:
            return []

        has_readonly = bool(self._READONLY_ROOT.search(content))
        if has_readonly:
            return []

        # Find the first container definition to report on
        for i, line in enumerate(ctx.lines, start=1):
            if self._CONTAINER.match(line) and "containers" in ctx.content[:sum(len(l) + 1 for l in ctx.lines[:i])]:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message="Container does not set 'readOnlyRootFilesystem: true'.",
                    suggestion=(
                        "Add 'readOnlyRootFilesystem: true' under securityContext. "
                        "Use emptyDir volumes for writable paths."
                    ),
                ))
                break

        if not issues:
            issues.append(self._make_issue(
                ctx,
                line=1,
                message="K8s workload without 'readOnlyRootFilesystem: true'.",
                suggestion=(
                    "Add 'readOnlyRootFilesystem: true' under container securityContext."
                ),
            ))
        return issues


# ---------------------------------------------------------------------------
# YC-S07: Capability additions (SYS_ADMIN, NET_RAW, ALL)
# ---------------------------------------------------------------------------
@register
class DangerousCapabilityRule(RegexRule):
    rule_id = "YC-S07"
    name = "Dangerous capability added"
    description = "Container adds dangerous Linux capabilities (SYS_ADMIN, NET_RAW, ALL)."
    severity = Severity.CRITICAL
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-250"
    pattern = r"(?i)^\s*-\s*(SYS_ADMIN|NET_RAW|ALL|SYS_PTRACE|NET_ADMIN)\s*$"
    exclude_pattern = r"^\s*#"
    message_template = "Dangerous capability added: {match}"
    fix_suggestion = (
        "Remove dangerous capabilities. Follow least-privilege principle — "
        "only add the specific capabilities required by the workload."
    )


# ---------------------------------------------------------------------------
# YC-S08: No network policy for K8s deployment
# ---------------------------------------------------------------------------
@register
class NoNetworkPolicyRule(BaseRule):
    rule_id = "YC-S08"
    name = "No NetworkPolicy"
    description = "Kubernetes Deployment/StatefulSet without a NetworkPolicy allows unrestricted traffic."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-284"

    _KIND_DEPLOYMENT = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet)\s*$")
    _NETWORK_POLICY = re.compile(r"^\s*kind\s*:\s*NetworkPolicy\s*$")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only flag Deployment / StatefulSet files that don't also contain a NetworkPolicy
        has_deployment = bool(self._KIND_DEPLOYMENT.search(ctx.content))
        has_network_policy = bool(self._NETWORK_POLICY.search(ctx.content))

        if not has_deployment or has_network_policy:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND_DEPLOYMENT.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="Deployment/StatefulSet without an accompanying NetworkPolicy.",
                    suggestion=(
                        "Create a NetworkPolicy to restrict ingress/egress traffic "
                        "for this workload."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-S09: ServiceAccount token auto-mount
# ---------------------------------------------------------------------------
@register
class AutomountServiceAccountTokenRule(BaseRule):
    rule_id = "YC-S09"
    name = "ServiceAccount token auto-mounted"
    description = "Pod auto-mounts the ServiceAccount token, which may be unnecessary."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-522"

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet|Job|CronJob|Pod)\s*$")
    _AUTOMOUNT_FALSE = re.compile(r"^\s*automountServiceAccountToken\s*:\s*false")

    def check(self, ctx: FileContext) -> list[Issue]:
        is_k8s_workload = bool(self._KIND.search(ctx.content))
        if not is_k8s_workload:
            return []

        has_automount_false = bool(self._AUTOMOUNT_FALSE.search(ctx.content))
        if has_automount_false:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="ServiceAccount token is auto-mounted. Disable if not needed.",
                    suggestion=(
                        "Set 'automountServiceAccountToken: false' in the pod spec "
                        "unless the pod needs access to the Kubernetes API."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-S10: Secrets exposed in plain environment variables
# ---------------------------------------------------------------------------
@register
class SecretsInPlainEnvRule(BaseRule):
    rule_id = "YC-S10"
    name = "Secret in plain env variable"
    description = "Secrets should be injected via secretKeyRef or volume mounts, not inline values."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-312"

    _ENV_NAME = re.compile(
        r"(?i)^\s*-?\s*name\s*:\s*\S*(?:PASSWORD|SECRET|TOKEN|API_KEY|PRIVATE_KEY|DB_PASS)\S*\s*$"
    )
    _PLAIN_VALUE = re.compile(r"^\s*value\s*:\s*\S")
    _VALUE_FROM = re.compile(r"^\s*valueFrom\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        for i, line in enumerate(lines, start=1):
            if not self._ENV_NAME.match(line):
                continue
            # Check the next few lines for 'value:' vs 'valueFrom:'
            for j in range(i, min(i + 3, len(lines))):
                next_line = lines[j]
                if self._VALUE_FROM.match(next_line):
                    break
                if self._PLAIN_VALUE.match(next_line):
                    issues.append(self._make_issue(
                        ctx,
                        line=i,
                        message=(
                            "Sensitive environment variable uses a plain 'value' instead "
                            "of 'valueFrom.secretKeyRef'."
                        ),
                        suggestion=(
                            "Use 'valueFrom.secretKeyRef' to reference a Kubernetes Secret, "
                            "or inject secrets via a secrets manager."
                        ),
                    ))
                    break
        return issues


# ---------------------------------------------------------------------------
# YC-S11: Insecure image registry (HTTP registry or no digest)
# ---------------------------------------------------------------------------
@register
class InsecureImageRegistryRule(BaseRule):
    rule_id = "YC-S11"
    name = "Insecure image reference"
    description = "Container image references an HTTP registry or uses a tag without digest pinning."
    severity = Severity.MEDIUM
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-494"

    _IMAGE_LINE = re.compile(r"^\s*-?\s*image\s*:\s*(?P<img>\S+)")
    _HTTP = re.compile(r"^http://")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._IMAGE_LINE.match(line)
            if not m:
                continue
            img = m.group("img").strip("\"'")
            if self._HTTP.match(img):
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Image uses insecure HTTP registry: '{img}'.",
                    suggestion="Use HTTPS registries only. Pin images by digest (@sha256:...).",
                ))
        return issues


# ---------------------------------------------------------------------------
# YC-S12: GitHub Actions using unpinned third-party actions
# ---------------------------------------------------------------------------
@register
class UnpinnedGitHubActionRule(BaseRule):
    rule_id = "YC-S12"
    name = "Unpinned GitHub Action"
    description = "GitHub Actions workflow uses a third-party action without pinning to a SHA."
    severity = Severity.HIGH
    category = Category.SECURITY
    languages = [Language.YAML]
    cwe_id = "CWE-829"

    _USES = re.compile(r"^\s*-?\s*uses\s*:\s*(?P<action>\S+)")
    _OFFICIAL_PREFIXES = ("actions/", "github/", "azure/", "docker/", "aws-actions/")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only apply to files that appear to be GitHub Actions workflows
        if ".github" not in str(ctx.path):
            return []

        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._USES.match(line)
            if not m:
                continue
            action = m.group("action").strip("\"'")

            # Skip local actions (./) and Docker images (docker://)
            if action.startswith("./") or action.startswith("docker://"):
                continue

            # Skip official actions (still recommended to pin, but lower risk)
            if any(action.startswith(prefix) for prefix in self._OFFICIAL_PREFIXES):
                continue

            # Check if pinned by SHA (contains @ followed by hex hash)
            if "@" in action:
                ref = action.split("@", 1)[1]
                if re.match(r"^[0-9a-f]{40}$", ref):
                    continue  # Properly pinned to SHA

            issues.append(self._make_issue(
                ctx,
                line=i,
                message=f"Third-party action '{action}' is not pinned to a commit SHA.",
                suggestion=(
                    "Pin to a full commit SHA: 'owner/action@<40-char-sha>'. "
                    "Use Dependabot or Renovate to keep pinned SHAs up-to-date."
                ),
            ))
        return issues


# ===========================================================================
#  Quality rules (YC-Q01 .. YC-Q10)
# ===========================================================================


# ---------------------------------------------------------------------------
# YC-Q01: Missing resource limits in K8s
# ---------------------------------------------------------------------------
@register
class MissingResourceLimitsRule(BaseRule):
    rule_id = "YC-Q01"
    name = "Missing resource limits"
    description = "Kubernetes container without resources.limits can consume unbounded resources."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet|Job|CronJob|Pod)\s*$")
    _LIMITS = re.compile(r"^\s*limits\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        is_k8s = bool(self._KIND.search(ctx.content))
        if not is_k8s:
            return []

        has_limits = bool(self._LIMITS.search(ctx.content))
        if has_limits:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="K8s workload has no 'resources.limits' defined.",
                    suggestion=(
                        "Add 'resources.limits.cpu' and 'resources.limits.memory' "
                        "to every container to prevent resource starvation."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-Q02: Missing liveness/readiness probes
# ---------------------------------------------------------------------------
@register
class MissingProbesRule(BaseRule):
    rule_id = "YC-Q02"
    name = "Missing liveness/readiness probes"
    description = "Kubernetes container without health probes may not be restarted on failure."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet)\s*$")
    _LIVENESS = re.compile(r"^\s*livenessProbe\s*:")
    _READINESS = re.compile(r"^\s*readinessProbe\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        is_k8s = bool(self._KIND.search(ctx.content))
        if not is_k8s:
            return []

        issues: list[Issue] = []
        has_liveness = bool(self._LIVENESS.search(ctx.content))
        has_readiness = bool(self._READINESS.search(ctx.content))

        if has_liveness and has_readiness:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                missing = []
                if not has_liveness:
                    missing.append("livenessProbe")
                if not has_readiness:
                    missing.append("readinessProbe")
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Missing health probes: {', '.join(missing)}.",
                    suggestion=(
                        "Add livenessProbe and readinessProbe to each container "
                        "for proper health monitoring and traffic routing."
                    ),
                ))
                break
        return issues


# ---------------------------------------------------------------------------
# YC-Q03: Using :latest tag in container image
# ---------------------------------------------------------------------------
@register
class LatestImageTagRule(BaseRule):
    rule_id = "YC-Q03"
    name = "Using :latest image tag"
    description = "Container image uses ':latest' or no tag, making deployments non-reproducible."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.YAML]

    _IMAGE_LINE = re.compile(r"^\s*-?\s*image\s*:\s*(?P<img>\S+)")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._IMAGE_LINE.match(line)
            if not m:
                continue
            img = m.group("img").strip("\"'")
            # Skip variables / templating
            if img.startswith("$") or "{{" in img:
                continue
            if img.endswith(":latest"):
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Image '{img}' uses ':latest' tag.",
                    suggestion="Pin to a specific version tag or digest for reproducibility.",
                ))
            elif ":" not in img and "@" not in img and "/" in img:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Image '{img}' has no tag — defaults to ':latest'.",
                    suggestion="Pin to a specific version tag or digest for reproducibility.",
                ))
        return issues


# ---------------------------------------------------------------------------
# YC-Q04: Missing restart policy in docker-compose
# ---------------------------------------------------------------------------
@register
class MissingRestartPolicyComposeRule(BaseRule):
    rule_id = "YC-Q04"
    name = "Missing restart policy in Compose"
    description = "Docker Compose service without a restart policy will not survive host restarts."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML]

    _SERVICES_HEADER = re.compile(r"^services\s*:")
    _SERVICE_NAME = re.compile(r"^  (\w[\w.-]*)\s*:")
    _RESTART = re.compile(r"^\s+restart\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        # Only apply to docker-compose files
        filename = ctx.path.name.lower()
        if "compose" not in filename and "docker" not in filename:
            if not self._SERVICES_HEADER.search(ctx.content):
                return []

        issues: list[Issue] = []
        in_services = False
        current_service: str | None = None
        current_service_line = 0
        has_restart = False

        for i, line in enumerate(ctx.lines, start=1):
            if self._SERVICES_HEADER.match(line):
                in_services = True
                continue
            if not in_services:
                continue
            # Top-level key exits services section
            if line and not line[0].isspace() and not line.startswith("#"):
                if current_service and not has_restart:
                    issues.append(self._make_issue(
                        ctx,
                        line=current_service_line,
                        message=f"Compose service '{current_service}' has no restart policy.",
                        suggestion="Add 'restart: unless-stopped' or 'restart: always'.",
                    ))
                in_services = False
                continue
            svc_m = self._SERVICE_NAME.match(line)
            if svc_m:
                if current_service and not has_restart:
                    issues.append(self._make_issue(
                        ctx,
                        line=current_service_line,
                        message=f"Compose service '{current_service}' has no restart policy.",
                        suggestion="Add 'restart: unless-stopped' or 'restart: always'.",
                    ))
                current_service = svc_m.group(1)
                current_service_line = i
                has_restart = False
                continue
            if self._RESTART.match(line):
                has_restart = True

        # Handle last service
        if current_service and not has_restart:
            issues.append(self._make_issue(
                ctx,
                line=current_service_line,
                message=f"Compose service '{current_service}' has no restart policy.",
                suggestion="Add 'restart: unless-stopped' or 'restart: always'.",
            ))
        return issues


# ---------------------------------------------------------------------------
# YC-Q05: Hardcoded replica count (should use HPA)
# ---------------------------------------------------------------------------
@register
class HardcodedReplicaCountRule(RegexRule):
    rule_id = "YC-Q05"
    name = "Hardcoded replica count"
    description = "Hardcoded replica count may limit auto-scaling flexibility."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML]
    pattern = r"^\s*replicas\s*:\s*\d+"
    exclude_pattern = r"^\s*#"
    message_template = "Hardcoded replica count: {match}."
    fix_suggestion = (
        "Consider using a HorizontalPodAutoscaler instead of a fixed replica count, "
        "or manage replicas externally (Argo CD, Flux, Kustomize overlays)."
    )


# ---------------------------------------------------------------------------
# YC-Q06: Missing labels/annotations in K8s metadata
# ---------------------------------------------------------------------------
@register
class MissingLabelsRule(BaseRule):
    rule_id = "YC-Q06"
    name = "Missing K8s labels"
    description = "Kubernetes resource without standard labels makes management and selection difficult."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*\w+")
    _API = re.compile(r"^\s*apiVersion\s*:")
    _LABELS = re.compile(r"^\s*labels\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        has_api = bool(self._API.search(ctx.content))
        if not has_api:
            return []

        has_labels = bool(self._LABELS.search(ctx.content))
        if has_labels:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="K8s resource has no labels in metadata.",
                    suggestion=(
                        "Add standard labels: 'app.kubernetes.io/name', "
                        "'app.kubernetes.io/version', 'app.kubernetes.io/managed-by'."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-Q07: Duplicate keys in YAML
# ---------------------------------------------------------------------------
@register
class DuplicateKeysRule(BaseRule):
    rule_id = "YC-Q07"
    name = "Duplicate YAML keys"
    description = "Same key appears multiple times at the same indentation level."
    severity = Severity.MEDIUM
    category = Category.QUALITY
    languages = [Language.YAML]

    _KEY_LINE = re.compile(r"^(?P<indent>\s*)(?P<key>[\w][\w.-]*)\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        # Track keys per indentation level in a stack-like fashion
        seen: dict[tuple[int, str], int] = {}
        # (indent_level, key) -> first_line_number

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if not stripped or stripped.startswith("#") or stripped.startswith("-"):
                continue
            m = self._KEY_LINE.match(line)
            if not m:
                continue
            indent = len(m.group("indent"))
            key = m.group("key")

            # When indent decreases, clear deeper keys
            keys_to_remove = [k for k in seen if k[0] > indent]
            for k in keys_to_remove:
                del seen[k]

            compound_key = (indent, key)
            if compound_key in seen:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Duplicate key '{key}' (first seen at line {seen[compound_key]}).",
                    suggestion="Remove the duplicate key — YAML parsers use only the last occurrence.",
                ))
            else:
                seen[compound_key] = i

        return issues


# ---------------------------------------------------------------------------
# YC-Q08: Very long lines in YAML (>200 chars)
# ---------------------------------------------------------------------------
@register
class LongYamlLineRule(BaseRule):
    rule_id = "YC-Q08"
    name = "Very long YAML line"
    description = "YAML line exceeds 200 characters, reducing readability."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML]

    _MAX_LENGTH = 200

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        for i, line in enumerate(ctx.lines, start=1):
            if len(line.rstrip()) > self._MAX_LENGTH:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message=f"Line is {len(line.rstrip())} characters long (limit: {self._MAX_LENGTH}).",
                    suggestion="Use YAML multi-line strings (| or >) or break into multiple keys.",
                ))
        return issues


# ---------------------------------------------------------------------------
# YC-Q09: Missing health check in docker-compose service
# ---------------------------------------------------------------------------
@register
class MissingComposeHealthcheckRule(BaseRule):
    rule_id = "YC-Q09"
    name = "Missing Compose healthcheck"
    description = "Docker Compose service without a healthcheck cannot report readiness."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML]

    _SERVICES_HEADER = re.compile(r"^services\s*:")
    _SERVICE_NAME = re.compile(r"^  (\w[\w.-]*)\s*:")
    _HEALTHCHECK = re.compile(r"^\s+healthcheck\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        filename = ctx.path.name.lower()
        if "compose" not in filename and "docker" not in filename:
            if "services:" not in ctx.content:
                return []

        issues: list[Issue] = []
        in_services = False
        current_service: str | None = None
        current_service_line = 0
        has_healthcheck = False

        for i, line in enumerate(ctx.lines, start=1):
            if self._SERVICES_HEADER.match(line):
                in_services = True
                continue
            if not in_services:
                continue
            if line and not line[0].isspace() and not line.startswith("#"):
                if current_service and not has_healthcheck:
                    issues.append(self._make_issue(
                        ctx,
                        line=current_service_line,
                        message=f"Compose service '{current_service}' has no healthcheck.",
                        suggestion="Add a 'healthcheck' block with test, interval, and timeout.",
                    ))
                in_services = False
                continue
            svc_m = self._SERVICE_NAME.match(line)
            if svc_m:
                if current_service and not has_healthcheck:
                    issues.append(self._make_issue(
                        ctx,
                        line=current_service_line,
                        message=f"Compose service '{current_service}' has no healthcheck.",
                        suggestion="Add a 'healthcheck' block with test, interval, and timeout.",
                    ))
                current_service = svc_m.group(1)
                current_service_line = i
                has_healthcheck = False
                continue
            if self._HEALTHCHECK.match(line):
                has_healthcheck = True

        if current_service and not has_healthcheck:
            issues.append(self._make_issue(
                ctx,
                line=current_service_line,
                message=f"Compose service '{current_service}' has no healthcheck.",
                suggestion="Add a 'healthcheck' block with test, interval, and timeout.",
            ))
        return issues


# ---------------------------------------------------------------------------
# YC-Q10: TODO/FIXME in config files
# ---------------------------------------------------------------------------
@register
class TodoInConfigRule(RegexRule):
    rule_id = "YC-Q10"
    name = "TODO/FIXME in config"
    description = "Config file contains TODO/FIXME/HACK/XXX comments indicating unfinished work."
    severity = Severity.LOW
    category = Category.QUALITY
    languages = [Language.YAML, Language.JSON]
    pattern = r"(?i)#\s*(?:TODO|FIXME|HACK|XXX)\b"
    message_template = "Unresolved comment found: {match}"
    fix_suggestion = "Resolve the TODO/FIXME before deploying to production."


# ===========================================================================
#  Reliability rules (YC-R01 .. YC-R05)
# ===========================================================================


# ---------------------------------------------------------------------------
# YC-R01: No PodDisruptionBudget referenced
# ---------------------------------------------------------------------------
@register
class NoPodDisruptionBudgetRule(BaseRule):
    rule_id = "YC-R01"
    name = "No PodDisruptionBudget"
    description = "Deployment without a PodDisruptionBudget may lose all replicas during node drain."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.YAML]

    _KIND_DEPLOYMENT = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet)\s*$")
    _PDB = re.compile(r"^\s*kind\s*:\s*PodDisruptionBudget\s*$")

    def check(self, ctx: FileContext) -> list[Issue]:
        has_deployment = bool(self._KIND_DEPLOYMENT.search(ctx.content))
        has_pdb = bool(self._PDB.search(ctx.content))

        if not has_deployment or has_pdb:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND_DEPLOYMENT.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="Deployment/StatefulSet without a PodDisruptionBudget.",
                    suggestion=(
                        "Create a PodDisruptionBudget with minAvailable or maxUnavailable "
                        "to ensure availability during voluntary disruptions."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-R02: Single replica deployment (replicas: 1)
# ---------------------------------------------------------------------------
@register
class SingleReplicaRule(RegexRule):
    rule_id = "YC-R02"
    name = "Single replica deployment"
    description = "Deployment with only 1 replica has no redundancy — single point of failure."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.YAML]
    pattern = r"^\s*replicas\s*:\s*1\s*$"
    exclude_pattern = r"^\s*#"
    message_template = "Single replica deployment detected."
    fix_suggestion = (
        "Use at least 2 replicas for production workloads to ensure high availability. "
        "Consider a HorizontalPodAutoscaler for dynamic scaling."
    )


# ---------------------------------------------------------------------------
# YC-R03: Missing imagePullPolicy
# ---------------------------------------------------------------------------
@register
class MissingImagePullPolicyRule(BaseRule):
    rule_id = "YC-R03"
    name = "Missing imagePullPolicy"
    description = "Container without explicit imagePullPolicy may use cached stale images."
    severity = Severity.LOW
    category = Category.RELIABILITY
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet|Job|CronJob|Pod)\s*$")
    _IMAGE_PULL_POLICY = re.compile(r"^\s*imagePullPolicy\s*:")
    _IMAGE_LINE = re.compile(r"^\s*-?\s*image\s*:\s*\S+")

    def check(self, ctx: FileContext) -> list[Issue]:
        is_k8s = bool(self._KIND.search(ctx.content))
        if not is_k8s:
            return []

        has_pull_policy = bool(self._IMAGE_PULL_POLICY.search(ctx.content))
        if has_pull_policy:
            return []

        # Find first image line to report
        for i, line in enumerate(ctx.lines, start=1):
            if self._IMAGE_LINE.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="Container has no 'imagePullPolicy' set.",
                    suggestion=(
                        "Set 'imagePullPolicy: Always' for mutable tags, "
                        "or 'IfNotPresent' for immutable/digest-pinned images."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-R04: No resource requests (K8s pod without resources.requests)
# ---------------------------------------------------------------------------
@register
class MissingResourceRequestsRule(BaseRule):
    rule_id = "YC-R04"
    name = "Missing resource requests"
    description = "Kubernetes container without resources.requests may be scheduled on overcommitted nodes."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*(Deployment|StatefulSet|DaemonSet|Job|CronJob|Pod)\s*$")
    _REQUESTS = re.compile(r"^\s*requests\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        is_k8s = bool(self._KIND.search(ctx.content))
        if not is_k8s:
            return []

        has_requests = bool(self._REQUESTS.search(ctx.content))
        if has_requests:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="K8s workload has no 'resources.requests' defined.",
                    suggestion=(
                        "Add 'resources.requests.cpu' and 'resources.requests.memory' "
                        "so the scheduler can make informed placement decisions."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-R05: EmptyDir volume without size limit
# ---------------------------------------------------------------------------
@register
class EmptyDirNoSizeLimitRule(BaseRule):
    rule_id = "YC-R05"
    name = "EmptyDir without size limit"
    description = "emptyDir volume without sizeLimit can consume all node disk space."
    severity = Severity.MEDIUM
    category = Category.RELIABILITY
    languages = [Language.YAML]

    _EMPTYDIR = re.compile(r"^\s*emptyDir\s*:\s*(\{\s*\}|$)")
    _SIZELIMIT = re.compile(r"^\s*sizeLimit\s*:")

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        lines = ctx.lines
        for i, line in enumerate(lines, start=1):
            if line.strip().startswith("#"):
                continue
            m = self._EMPTYDIR.match(line)
            if not m:
                continue
            # If emptyDir: {} — inline empty, no sizeLimit possible
            if m.group(1) and m.group(1).strip() == "{}":
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message="emptyDir volume has no sizeLimit.",
                    suggestion="Add 'sizeLimit' to emptyDir: 'emptyDir: { sizeLimit: 256Mi }'.",
                ))
                continue
            # Otherwise check subsequent lines for sizeLimit
            found_limit = False
            for j in range(i, min(i + 5, len(lines))):
                next_line = lines[j]
                if self._SIZELIMIT.match(next_line):
                    found_limit = True
                    break
                # If we hit a different key at same or lesser indent, stop
                if j > i - 1 and next_line.strip() and not next_line.strip().startswith("#"):
                    stripped = next_line.lstrip()
                    indent = len(next_line) - len(stripped)
                    orig_indent = len(line) - len(line.lstrip())
                    if indent <= orig_indent and ":" in stripped:
                        break
            if not found_limit:
                issues.append(self._make_issue(
                    ctx,
                    line=i,
                    message="emptyDir volume has no sizeLimit.",
                    suggestion="Add 'sizeLimit' to the emptyDir volume, e.g. 'sizeLimit: 256Mi'.",
                ))
        return issues


# ===========================================================================
#  Performance rules (YC-P01 .. YC-P03)
# ===========================================================================


# ---------------------------------------------------------------------------
# YC-P01: Large ConfigMap/Secret (>1MB indicator pattern)
# ---------------------------------------------------------------------------
@register
class LargeConfigMapSecretRule(BaseRule):
    rule_id = "YC-P01"
    name = "Potentially large ConfigMap/Secret"
    description = "ConfigMap or Secret with very large inline data may impact etcd performance."
    severity = Severity.MEDIUM
    category = Category.PERFORMANCE
    languages = [Language.YAML]

    _KIND = re.compile(r"^\s*kind\s*:\s*(ConfigMap|Secret)\s*$")
    # etcd limit is 1.5 MB; warn at 1 MB (rough heuristic: file size)
    _SIZE_THRESHOLD = 1_000_000  # 1 MB

    def check(self, ctx: FileContext) -> list[Issue]:
        is_configmap_or_secret = bool(self._KIND.search(ctx.content))
        if not is_configmap_or_secret:
            return []

        file_size = len(ctx.content.encode("utf-8"))
        if file_size < self._SIZE_THRESHOLD:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._KIND.match(line):
                size_kb = file_size // 1024
                return [self._make_issue(
                    ctx,
                    line=i,
                    message=(
                        f"ConfigMap/Secret file is ~{size_kb} KB. "
                        f"Large objects degrade etcd performance (limit: 1.5 MB)."
                    ),
                    suggestion=(
                        "Split into multiple smaller ConfigMaps/Secrets, "
                        "or use external config stores (ConfigMap generators, Vault, etc.)."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-P02: No HorizontalPodAutoscaler pattern
# ---------------------------------------------------------------------------
@register
class NoHPAPatternRule(BaseRule):
    rule_id = "YC-P02"
    name = "No HorizontalPodAutoscaler"
    description = "Deployment with static replicas and no HPA cannot auto-scale under load."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.YAML]

    _KIND_DEPLOYMENT = re.compile(r"^\s*kind\s*:\s*Deployment\s*$")
    _HPA = re.compile(r"^\s*kind\s*:\s*HorizontalPodAutoscaler\s*$")
    _REPLICAS = re.compile(r"^\s*replicas\s*:\s*\d+")

    def check(self, ctx: FileContext) -> list[Issue]:
        has_deployment = bool(self._KIND_DEPLOYMENT.search(ctx.content))
        has_hpa = bool(self._HPA.search(ctx.content))

        if not has_deployment or has_hpa:
            return []

        has_replicas = bool(self._REPLICAS.search(ctx.content))
        if not has_replicas:
            return []

        for i, line in enumerate(ctx.lines, start=1):
            if self._REPLICAS.match(line):
                return [self._make_issue(
                    ctx,
                    line=i,
                    message="Deployment uses static replicas with no HorizontalPodAutoscaler.",
                    suggestion=(
                        "Create a HorizontalPodAutoscaler targeting this Deployment "
                        "to enable auto-scaling based on CPU/memory or custom metrics."
                    ),
                )]
        return []


# ---------------------------------------------------------------------------
# YC-P03: Excessive environment variables (>20 env vars per container)
# ---------------------------------------------------------------------------
@register
class ExcessiveEnvVarsRule(BaseRule):
    rule_id = "YC-P03"
    name = "Excessive environment variables"
    description = "Container defines too many environment variables (>20), indicating config sprawl."
    severity = Severity.LOW
    category = Category.PERFORMANCE
    languages = [Language.YAML]

    _ENV_HEADER = re.compile(r"^\s*env\s*:\s*$")
    _ENV_ITEM = re.compile(r"^\s*-\s*name\s*:")
    _THRESHOLD = 20

    def check(self, ctx: FileContext) -> list[Issue]:
        issues: list[Issue] = []
        in_env = False
        env_start_line = 0
        env_indent = 0
        env_count = 0

        for i, line in enumerate(ctx.lines, start=1):
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue

            if self._ENV_HEADER.match(line):
                # Flush previous env block if any
                if in_env and env_count > self._THRESHOLD:
                    issues.append(self._make_issue(
                        ctx,
                        line=env_start_line,
                        message=f"Container has {env_count} environment variables (threshold: {self._THRESHOLD}).",
                        suggestion=(
                            "Use ConfigMaps or envFrom to reduce inline env vars. "
                            "Group related configuration into mounted config files."
                        ),
                    ))
                in_env = True
                env_start_line = i
                env_indent = len(line) - len(line.lstrip())
                env_count = 0
                continue

            if in_env:
                current_indent = len(line) - len(line.lstrip())
                # If indent is at or below the env header's indent, we've left the env block
                if current_indent <= env_indent and not self._ENV_ITEM.match(line):
                    if env_count > self._THRESHOLD:
                        issues.append(self._make_issue(
                            ctx,
                            line=env_start_line,
                            message=f"Container has {env_count} environment variables (threshold: {self._THRESHOLD}).",
                            suggestion=(
                                "Use ConfigMaps or envFrom to reduce inline env vars. "
                                "Group related configuration into mounted config files."
                            ),
                        ))
                    in_env = False
                    continue
                if self._ENV_ITEM.match(line):
                    env_count += 1

        # Handle end-of-file
        if in_env and env_count > self._THRESHOLD:
            issues.append(self._make_issue(
                ctx,
                line=env_start_line,
                message=f"Container has {env_count} environment variables (threshold: {self._THRESHOLD}).",
                suggestion=(
                    "Use ConfigMaps or envFrom to reduce inline env vars. "
                    "Group related configuration into mounted config files."
                ),
            ))

        return issues
