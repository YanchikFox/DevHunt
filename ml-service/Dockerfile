FROM python:3.11-slim-bookworm

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    postgresql-client \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install Poetry
ENV POETRY_VERSION=1.7.1
RUN pip install --no-cache-dir poetry==$POETRY_VERSION

# Copy dependency files
COPY pyproject.toml poetry.lock* ./
COPY requirements.txt .

# Install dependencies using Poetry or pip
# If poetry.lock exists, use Poetry; otherwise, use pip with requirements.txt
RUN if [ -f "poetry.lock" ]; then \
        poetry config virtualenvs.create false && \
        poetry install --no-interaction --no-ansi --no-dev; \
    else \
        echo "No poetry.lock found, installing from requirements.txt" && \
        pip install --no-cache-dir -r requirements.txt; \
    fi

# Create non-root user
RUN useradd -m -u 1000 appuser

# Copy application code
COPY . .
RUN chown -R appuser:appuser /app

# Expose port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

USER appuser

# Run application
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]

