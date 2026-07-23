"""Authorization for the Prometheus /metrics endpoint."""

import os

from fastapi import HTTPException, Request

_MIN_TOKEN_LENGTH = 16


def _is_production() -> bool:
    return os.environ.get("ENVIRONMENT", "development").lower() == "production"


def _is_loopback(host: str | None) -> bool:
    if not host:
        return False
    if host.startswith("::ffff:"):
        host = host.removeprefix("::ffff:")
    return host in {"127.0.0.1", "::1"} or host.startswith("127.")


def verify_metrics_access(request: Request) -> None:
    """Raises HTTPException when the caller is not allowed to scrape metrics."""
    expected = os.environ.get("METRICS_TOKEN")

    if _is_production() and (not expected or len(expected) < _MIN_TOKEN_LENGTH):
        raise HTTPException(
            status_code=503,
            detail="METRICS_TOKEN must be configured in production",
        )

    if expected and request.headers.get("x-metrics-token") == expected:
        return

    client_host = request.client.host if request.client else None
    if _is_loopback(client_host):
        return

    raise HTTPException(
        status_code=403,
        detail="Forbidden: metrics require X-Metrics-Token or local access",
    )
