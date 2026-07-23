"""
JWT authentication and authorization for DevHunt ML Service.

Provides verify_token dependency for securing API endpoints.
"""

import hmac
import logging
import os

import jwt
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

logger = logging.getLogger(__name__)

security = HTTPBearer()

JWT_SECRET = os.getenv("JWT_SECRET")
JWT_ISSUER = os.getenv("JWT_ISSUER", "DevHunt.AuthService")
AUTH_SERVICE_URL = os.getenv("AUTH_SERVICE_URL", "http://auth-service:5001")

# REC-09: Pre-shared token for CoreApi → MLService internal calls (service-to-service auth)
ML_SERVICE_TOKEN = os.getenv("ML_SERVICE_TOKEN")

ENVIRONMENT = os.getenv("ENVIRONMENT", "development").lower()

# SECURITY FIX (SEC-014, R3): Require JWT_SECRET in production
if ENVIRONMENT == "production":
    if not JWT_SECRET or len(JWT_SECRET) < 32:
        raise ValueError(
            "JWT_SECRET must be set to a strong value (32+ characters) in production. "
            "Authentication cannot be disabled in production environment. "
            "Set JWT_SECRET environment variable properly."
        )
    logger.info("JWT authentication enabled for production")
elif not JWT_SECRET:
    logger.warning(
        "JWT_SECRET not set. Running in development mode with authentication disabled."
    )


def decode_jwt_token(token: str) -> dict:
    """Decode and validate JWT token, returning payload or raising HTTPException."""
    try:
        payload = jwt.decode(
            token,
            JWT_SECRET,
            algorithms=["HS256"],
            issuer=JWT_ISSUER,
            options={"verify_signature": True},
        )
        return {
            "user_id": payload.get("sub") or payload.get("userId"),
            "role": payload.get("role"),
        }
    except jwt.ExpiredSignatureError:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, detail="Token expired"
        )
    except jwt.InvalidTokenError:
        logger.warning("Invalid token rejected")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid token"
        )


def verify_token(
    credentials: HTTPAuthorizationCredentials = Depends(security),
) -> dict:
    """Verify JWT token or internal service token from Authorization header."""
    token = credentials.credentials

    # REC-09: Accept pre-shared service token for CoreApi → MLService calls.
    # DEV-127: constant-time comparison to avoid leaking the token via timing.
    if ML_SERVICE_TOKEN and hmac.compare_digest(token, ML_SERVICE_TOKEN):
        return {"user_id": "internal-service", "role": "admin"}

    if not JWT_SECRET:
        # Only allowed in non-production environments
        logger.warning(
            "JWT_SECRET not set, authentication disabled (development only)"
        )
        return {"user_id": "anonymous"}

    return decode_jwt_token(token)
