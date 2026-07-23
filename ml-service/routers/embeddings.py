"""
Embeddings router — generates vector embeddings for code analysis RAG.

Uses fastembed (ONNX Runtime) with all-MiniLM-L6-v2 (384 dims, ~50MB, fast on CPU).
No PyTorch dependency — 30x lighter than sentence-transformers.

Endpoints:
  POST /api/embeddings/generate — batch embed a list of texts
  POST /api/embeddings/query    — embed a single query for similarity search
"""

import logging
import time
from typing import List

from fastapi import APIRouter, Depends, HTTPException
from security import verify_token
from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/embeddings", tags=["embeddings"], dependencies=[Depends(verify_token)])

# Lazy-loaded model (loads on first request, ~2s cold start)
_model = None

MODEL_NAME = "BAAI/bge-small-en-v1.5"  # 384 dims, great quality/size ratio


def _get_model():
    global _model
    if _model is None:
        logger.info("Loading embedding model %s...", MODEL_NAME)
        start = time.perf_counter()
        from fastembed import TextEmbedding

        _model = TextEmbedding(model_name=MODEL_NAME)
        elapsed = time.perf_counter() - start
        logger.info("Embedding model loaded in %.1fs", elapsed)
    return _model


# ── Request / Response Models ──


class EmbedRequest(BaseModel):
    """Batch embedding request — embed multiple texts at once."""

    texts: List[str] = Field(..., min_length=1, max_length=500)


class EmbedResponse(BaseModel):
    embeddings: List[List[float]]
    model: str = MODEL_NAME
    dimensions: int = 384


class QueryEmbedRequest(BaseModel):
    """Single query embedding — for similarity search."""

    query: str = Field(..., min_length=1, max_length=2000)


class QueryEmbedResponse(BaseModel):
    embedding: List[float]
    model: str = MODEL_NAME
    dimensions: int = 384


# ── Endpoints ──


@router.post("/generate", response_model=EmbedResponse)
async def generate_embeddings(req: EmbedRequest):
    """Generate embeddings for a batch of texts (max 500)."""
    try:
        model = _get_model()
        start = time.perf_counter()
        vectors = list(model.embed(req.texts))
        elapsed = time.perf_counter() - start

        logger.info(
            "Generated %d embeddings in %.2fs (%.1f texts/s)",
            len(req.texts),
            elapsed,
            len(req.texts) / elapsed if elapsed > 0 else 0,
        )

        return EmbedResponse(
            embeddings=[v.tolist() for v in vectors],
        )
    except Exception as e:
        logger.exception("Embedding generation failed: %s", e)
        raise HTTPException(status_code=500, detail="Embedding generation failed") from e


@router.post("/query", response_model=QueryEmbedResponse)
async def embed_query(req: QueryEmbedRequest):
    """Embed a single search query for similarity lookup."""
    try:
        model = _get_model()
        vectors = list(model.embed([req.query]))

        return QueryEmbedResponse(
            embedding=vectors[0].tolist(),
        )
    except Exception as e:
        logger.exception("Query embedding failed: %s", e)
        raise HTTPException(status_code=500, detail="Query embedding failed") from e
