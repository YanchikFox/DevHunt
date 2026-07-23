FROM python:3.13-slim AS base

ENV PYTHONUNBUFFERED=1
WORKDIR /app

# Install git for cloning repos
RUN apt-get update && \
    apt-get install -y --no-install-recommends git && \
    rm -rf /var/lib/apt/lists/*

COPY pyproject.toml ./
RUN pip install --no-cache-dir .

# Install Semgrep community scanner (separate layer for caching)
RUN pip install --no-cache-dir semgrep

COPY devhunt_analyzer/ devhunt_analyzer/

# Non-root user with home dir (Semgrep caches rules there)
RUN useradd -r -m -s /usr/sbin/nologin analyzer
USER analyzer

EXPOSE 8090

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8090/health')" || exit 1

CMD ["python", "-m", "devhunt_analyzer.api"]
