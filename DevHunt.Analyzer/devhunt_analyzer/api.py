"""
Lightweight HTTP API for DevHunt Analyzer.

Endpoints:
  POST /analyze   — Clone repo (or accept path) and return analysis JSON
  GET  /health    — Health check
  GET  /rules     — List all rules

Run standalone:  python -m devhunt_analyzer.api
Inside Docker:   CMD ["python", "-m", "devhunt_analyzer.api"]
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.parse import urlparse

# Force rule registration
import devhunt_analyzer.rules  # noqa: F401

from devhunt_analyzer.engine.rule_engine import run_analysis
from devhunt_analyzer.engine.rule_registry import list_rules

LISTEN_HOST = os.environ.get("ANALYZER_HOST", "0.0.0.0")
LISTEN_PORT = int(os.environ.get("ANALYZER_PORT", "8090"))
MAX_REPO_SIZE_MB = int(os.environ.get("ANALYZER_MAX_REPO_MB", "200"))
CLONE_TIMEOUT = int(os.environ.get("ANALYZER_CLONE_TIMEOUT", "120"))
ALLOWED_HOSTS = {"github.com", "gitlab.com", "bitbucket.org"}


def _validate_repo_url(url: str) -> str | None:
    """Return None if valid, error string otherwise."""
    try:
        parsed = urlparse(url)
    except Exception:
        return "Invalid URL format."
    if parsed.scheme not in ("https",):
        return "Only HTTPS repository URLs accepted."
    if parsed.hostname not in ALLOWED_HOSTS:
        return f"Host '{parsed.hostname}' not in allow-list: {ALLOWED_HOSTS}."
    if ".." in parsed.path or ";" in url or "|" in url or "&" in url:
        return "URL contains forbidden characters."
    return None


def _build_clone_url(url: str, token: str | None) -> str:
    """Inject auth token into HTTPS URL for private repo access."""
    if not token:
        return url
    parsed = urlparse(url)
    # https://x-access-token:TOKEN@github.com/owner/repo.git
    authed = parsed._replace(netloc=f"x-access-token:{token}@{parsed.hostname}")
    return authed.geturl()


def _clone_repo(url: str, dest: Path, token: str | None = None) -> str | None:
    """Clone a repo. Returns error string or None on success."""
    clone_url = _build_clone_url(url, token)
    env = os.environ.copy()
    # Prevent git from prompting for credentials
    env["GIT_TERMINAL_PROMPT"] = "0"
    try:
        subprocess.run(
            ["git", "clone", "--depth", "1", "--single-branch", "--", clone_url, str(dest)],
            capture_output=True,
            text=True,
            timeout=CLONE_TIMEOUT,
            check=True,
            env=env,
        )
    except subprocess.TimeoutExpired:
        return "Clone timed out."
    except subprocess.CalledProcessError as e:
        # Scrub token from error messages
        stderr = e.stderr[:200] if e.stderr else ""
        if token and token in stderr:
            stderr = stderr.replace(token, "***")
        return f"Clone failed: {stderr}"
    return None


class AnalyzerHandler(BaseHTTPRequestHandler):
    """HTTP request handler for the analyzer API."""

    def _send_json(self, status: int, data: dict) -> None:
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802
        if self.path == "/health":
            self._send_json(200, {"status": "ok"})
        elif self.path == "/rules":
            self._send_json(200, {"rules": list_rules(), "count": len(list_rules())})
        else:
            self._send_json(404, {"error": "Not found"})

    def do_POST(self) -> None:  # noqa: N802
        if self.path != "/analyze":
            self._send_json(404, {"error": "Not found"})
            return

        content_length = int(self.headers.get("Content-Length", 0))
        if content_length > 10_000:
            self._send_json(400, {"error": "Request body too large."})
            return

        # Authenticate internal callers via shared secret (fail-closed unless local dev)
        expected_token = os.environ.get("ANALYZER_API_SECRET")
        if not expected_token and os.environ.get("ANALYZER_AUTH_DISABLED", "").lower() != "true":
            self._send_json(503, {"error": "Analyzer authentication not configured."})
            return
        if expected_token:
            auth_header = self.headers.get("Authorization", "")
            if auth_header != f"Bearer {expected_token}":
                self._send_json(401, {"error": "Unauthorized."})
                return

        try:
            body = json.loads(self.rfile.read(content_length))
        except (json.JSONDecodeError, UnicodeDecodeError):
            self._send_json(400, {"error": "Invalid JSON."})
            return

        repo_url = body.get("repository_url", "").strip()
        if not repo_url:
            self._send_json(400, {"error": "repository_url is required."})
            return

        # Validate URL (prevent SSRF)
        err = _validate_repo_url(repo_url)
        if err:
            self._send_json(400, {"error": err})
            return

        # GitHub access token for private repos (passed by integration gateway)
        access_token = body.get("access_token")
        # Specific branch or commit SHA to analyze
        branch = body.get("branch")
        commit_sha = body.get("commit_sha")

        # Optional filters
        severity = body.get("min_severity")
        category = body.get("category")
        disabled = set(body.get("disabled_rules", []))
        exclude_patterns = body.get("exclude_patterns", [])
        workers = min(int(body.get("workers", 4)), 8)

        tmpdir = tempfile.mkdtemp(prefix="analyzer_")
        try:
            clone_err = _clone_repo(repo_url, Path(tmpdir) / "repo", token=access_token)
            if clone_err:
                self._send_json(422, {"error": clone_err})
                return

            # Checkout specific commit if requested
            repo_path = Path(tmpdir) / "repo"
            if commit_sha:
                try:
                    subprocess.run(
                        ["git", "fetch", "--depth", "1", "origin", commit_sha],
                        cwd=str(repo_path),
                        capture_output=True, text=True, timeout=60, check=True,
                    )
                    subprocess.run(
                        ["git", "checkout", commit_sha],
                        cwd=str(repo_path),
                        capture_output=True, text=True, timeout=30, check=True,
                    )
                except subprocess.CalledProcessError:
                    pass  # shallow clone already has HEAD, proceed with it

            report = run_analysis(
                target=repo_path,
                workers=workers,
                disabled_rules=disabled if disabled else None,
                exclude_patterns=exclude_patterns if exclude_patterns else None,
            )

            result = report.to_dict()

            # Attach metadata for traceability
            if commit_sha:
                result["commit_sha"] = commit_sha
            if branch:
                result["branch"] = branch

            # Post-filter
            if category:
                result["issues"] = [i for i in result["issues"] if i["category"] == category]
                result["total_issues"] = len(result["issues"])
            if severity:
                sev_order = ["critical", "high", "medium", "low", "info"]
                if severity in sev_order:
                    min_idx = sev_order.index(severity)
                    result["issues"] = [
                        i for i in result["issues"]
                        if sev_order.index(i["severity"]) <= min_idx
                    ]
                    result["total_issues"] = len(result["issues"])

            self._send_json(200, result)
        finally:
            shutil.rmtree(tmpdir, ignore_errors=True)

    def log_message(self, format: str, *args: object) -> None:
        # Structured logging
        print(f"[analyzer-api] {self.client_address[0]} {format % args}")


def main() -> None:
    server = HTTPServer((LISTEN_HOST, LISTEN_PORT), AnalyzerHandler)
    print(f"[analyzer-api] Listening on {LISTEN_HOST}:{LISTEN_PORT}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
