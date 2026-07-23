"""
Recommendations router — project recommendation engine.

Extracted from main.py to keep it under 500 LoC.
Contains scoring algorithm, SQL queries, and API endpoints.
"""

import json
import logging
import uuid as uuid_lib
from datetime import datetime
from typing import List, Optional

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, status
from pydantic import BaseModel

from deps import get_db_pool, get_redis_client
from security import verify_token

logger = logging.getLogger(__name__)

router = APIRouter(tags=["Recommendations"])


# ============================================================================
# Models
# ============================================================================


class RecommendationRequest(BaseModel):
    """Request for generating recommendations."""

    user_id: str
    limit: int = 10
    exclude_project_ids: Optional[List[str]] = None


class RecommendationItem(BaseModel):
    """Single recommendation."""

    project_id: str
    match_score: float
    reasoning: dict
    similarity_factors: dict


class RecommendationResponse(BaseModel):
    """Response with recommendations."""

    user_id: str
    recommendations: List[RecommendationItem]
    generated_at: datetime


# ============================================================================
# Scoring Algorithm
# ============================================================================

DIFFICULTY_SCORES = {"beginner": 0.3, "intermediate": 0.6, "advanced": 1.0}


def calc_skill_match_factor(
    user_skills: list, tech_stack: list
) -> tuple[float, dict]:
    """Calculate skills match score (40% weight)."""
    if not user_skills or not tech_stack:
        return 0.0, {"ratio": 0.0, "matching": [], "required": []}
    matching_skills = set(user_skills) & set(tech_stack)
    ratio = len(matching_skills) / max(len(tech_stack), 1)
    return ratio * 0.4, {
        "ratio": ratio,
        "matching": list(matching_skills),
        "required": list(tech_stack),
    }


def calc_difficulty_factor(
    difficulty: str, user_experience: int
) -> tuple[float, dict]:
    """Calculate difficulty level score (20% weight)."""
    base_score = DIFFICULTY_SCORES.get(difficulty, 0.5)
    score = (
        base_score
        if user_experience >= 3
        else DIFFICULTY_SCORES.get("beginner", 0.3)
    )
    return score * 0.2, {
        "level": difficulty,
        "user_experience": user_experience,
    }


def calc_rating_factor(
    avg_rating: float, review_count: int
) -> tuple[float, dict]:
    """Calculate project rating score (20% weight)."""
    score = min(avg_rating / 5.0, 1.0) * 0.2
    return score, {"project_rating": avg_rating, "review_count": review_count}


def calc_team_size_factor(team_size: int) -> tuple[float, dict]:
    """Calculate team size score (10% weight) — preference for medium teams."""
    score = 1.0 if 3 <= team_size <= 8 else 0.5
    return score * 0.1, {"current": team_size, "optimal_range": "3-8"}


def calc_featured_factor(is_featured: bool) -> tuple[float, bool]:
    """Calculate featured project bonus (10% weight)."""
    return 0.1 if is_featured else 0.0, is_featured


def calculate_project_score(
    user_skills: list,
    user_experience: int,
    project_data: dict,
) -> tuple[float, dict]:
    """Calculate total match score and factors for a project."""
    factors = {}
    score = 0.0

    tech_stack = project_data.get("tech_stack", [])
    difficulty = project_data.get("difficulty", "beginner")
    avg_rating = project_data.get("avg_rating", 0.0)
    review_count = project_data.get("review_count", 0)
    team_size = project_data.get("team_size", 0)
    is_featured = project_data.get("is_featured", False)

    skill_score, factors["skill_match"] = calc_skill_match_factor(
        user_skills, tech_stack
    )
    score += skill_score

    diff_score, factors["difficulty"] = calc_difficulty_factor(
        difficulty, user_experience
    )
    score += diff_score

    rating_score, factors["rating"] = calc_rating_factor(avg_rating, review_count)
    score += rating_score

    team_score, factors["team_size"] = calc_team_size_factor(team_size)
    score += team_score

    featured_score, factors["featured"] = calc_featured_factor(is_featured)
    score += featured_score

    return min(score * 100, 100.0), factors


# ============================================================================
# SQL Queries
# ============================================================================

USER_PROFILE_QUERY = """
    SELECT
        u.id,
        u."Role",
        u."Experience",
        u."Rating",
        array_agg(DISTINCT s."Name") as skills,
        array_agg(DISTINCT us."ProficiencyLevel") as proficiency_levels
    FROM "Users" u
    LEFT JOIN "UserSkills" us ON us."UserId" = u.id
    LEFT JOIN "Skills" s ON s."Id" = us."SkillId"
    WHERE u.id = $1 AND u."IsActive" = true
    GROUP BY u.id
"""

PROJECTS_QUERY = """
    SELECT
        p.id,
        p."Title",
        p."ShortDescription",
        p."DifficultyLevel",
        p."Status",
        p."Visibility",
        p."Featured",
        p."OwnerId",
        array_agg(DISTINCT pt."TechStack") as tech_stack,
        array_agg(DISTINCT pr."Role") as required_roles,
        COUNT(DISTINCT tm.id) as team_size,
        COUNT(DISTINCT r.id) as review_count,
        COALESCE(AVG(r."Rating"), 0) as avg_rating
    FROM "Projects" p
    LEFT JOIN "ProjectTechStack" pt ON pt."ProjectId" = p.id
    LEFT JOIN "ProjectRole" pr ON pr."ProjectId" = p.id
    LEFT JOIN "TeamMembers" tm ON tm."ProjectId" = p.id AND tm."Status" = 'active'
    LEFT JOIN "Reviews" r ON r."ProjectId" = p.id
    WHERE p."Status" = 'recruiting'
        AND p."Visibility" = 'public'
        AND p.id != ALL($1::uuid[])
    GROUP BY p.id, p."Title", p."ShortDescription", p."DifficultyLevel",
             p."Status", p."Visibility", p."Featured", p."OwnerId"
    HAVING COUNT(DISTINCT tm.id) < 20
    ORDER BY p."Featured" DESC, p."CreatedAt" DESC
    LIMIT 100
"""


# ============================================================================
# Recommendation Engine
# ============================================================================


def extract_project_data(project: dict) -> dict:
    """Extract and normalize project data for scoring."""
    return {
        "tech_stack": project["tech_stack"] or [],
        "difficulty": project["DifficultyLevel"] or "beginner",
        "avg_rating": float(project["avg_rating"] or 0),
        "review_count": project["review_count"],
        "team_size": project["team_size"] or 0,
        "is_featured": project["Featured"],
    }


def create_recommendation_item(
    project: dict, user_skills: list, user_experience: int
) -> RecommendationItem:
    """Create a recommendation item from project data."""
    project_data = extract_project_data(project)
    match_score, factors = calculate_project_score(
        user_skills=user_skills,
        user_experience=user_experience,
        project_data=project_data,
    )
    difficulty = project_data["difficulty"]
    avg_rating = project_data["avg_rating"]
    skill_ratio = factors.get("skill_match", {}).get("ratio", 0)

    return RecommendationItem(
        project_id=str(project["id"]),
        match_score=round(match_score, 2),
        reasoning={
            "summary": f"Skills match: {skill_ratio:.0%}, Level: {difficulty}, Rating: {avg_rating:.1f}",
            "factors": factors,
        },
        similarity_factors=factors,
    )


async def fetch_user_and_projects(
    conn, user_id: str, exclude_ids: List[str]
) -> tuple[dict | None, list]:
    """Fetch user profile and candidate projects from database."""
    user_row = await conn.fetchrow(USER_PROFILE_QUERY, user_id)
    if not user_row:
        logger.warning("User %s not found", user_id)
        return None, []
    projects = await conn.fetch(PROJECTS_QUERY, exclude_ids)
    if not projects:
        logger.info("No projects found for recommendations")
        return user_row, []
    return user_row, projects


def extract_user_profile(user_row: dict) -> tuple[list, int]:
    """Extract user skills and experience from user row."""
    return user_row["skills"] or [], user_row["Experience"] or 0


def build_recommendations(
    projects: list, user_skills: list, user_experience: int, limit: int
) -> List[RecommendationItem]:
    """Build and sort recommendations from projects list."""
    recommendations = [
        create_recommendation_item(project, user_skills, user_experience)
        for project in projects
    ]
    recommendations.sort(key=lambda x: x.match_score, reverse=True)
    return recommendations[:limit]


# amazonq-ignore-next-line
async def generate_recommendations(
    user_id: str,
    limit: int = 10,
    exclude_ids: Optional[List[str]] = None,
    db_pool=None,
) -> List[RecommendationItem]:
    """Generate project recommendations for user.

    Algorithm:
    1. Get user profile (skills, experience, rating)
    2. Find projects with similar requirements
    3. Calculate match_score based on scoring factors
    4. Return top-N recommendations
    """
    try:
        async with db_pool.acquire() as conn:
            user_row, projects = await fetch_user_and_projects(
                conn, user_id, exclude_ids or []
            )
            if not user_row or not projects:
                return []
            user_skills, user_experience = extract_user_profile(user_row)
            return build_recommendations(
                projects, user_skills, user_experience, limit
            )
    except Exception as e:
        # REC-08: Full stack trace goes to logs (server-side only), not to HTTP response
        logger.error("Error generating recommendations for user %s: %s", user_id, e, exc_info=True)
        raise HTTPException(
            status_code=500,
            detail="Failed to generate recommendations. Please try again later.",
        )


# ============================================================================
# Cache Helpers
# ============================================================================


def check_user_access(user: dict, user_id: str) -> None:
    """Check if user has access to the requested resource."""
    if user.get("user_id") != user_id and user.get("role") not in [
        "admin",
        "curator",
    ]:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN, detail="Access denied"
        )


def _validate_uuid(value: str) -> str:
    """Validate UUID format and return canonical form to prevent cache key injection."""
    try:
        return str(uuid_lib.UUID(value))
    except (ValueError, AttributeError):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid user_id format",
        )


async def try_get_cached_recommendations(
    redis_client, user_id: str, limit: int
) -> Optional[RecommendationResponse]:
    """Try to get cached recommendations, returns None on miss or error."""
    # REC-10: Validate UUID to prevent cache key injection via special chars (`:`, `*`)
    safe_user_id = _validate_uuid(user_id)
    cache_key = f"recommendations:user:{safe_user_id}:limit:{limit}"
    try:
        cached_data = await redis_client.get(cache_key)
        if cached_data:
            logger.info("Cache HIT for user %s recommendations", user_id)
            cached_response = json.loads(cached_data)
            cached_response["generated_at"] = datetime.fromisoformat(
                cached_response["generated_at"]
            )
            return RecommendationResponse(**cached_response)
    except Exception as e:
        logger.warning("Redis cache read failed: %s, falling back to DB", e)
    return None


async def cache_recommendations(
    redis_client,
    user_id: str,
    limit: int,
    response: RecommendationResponse,
) -> None:
    """Cache recommendations with 1 hour TTL."""
    safe_user_id = _validate_uuid(user_id)
    cache_key = f"recommendations:user:{safe_user_id}:limit:{limit}"
    try:
        cache_data = response.model_dump()
        cache_data["generated_at"] = cache_data["generated_at"].isoformat()
        await redis_client.setex(
            cache_key, 3600, json.dumps(cache_data, default=str)
        )
        logger.info("Cached recommendations for user %s, TTL=3600s", user_id)
    except Exception as e:
        logger.warning("Failed to cache recommendations: %s", e)


# ============================================================================
# API Endpoints
# ============================================================================


@router.post(
    "/api/recommendations/generate", response_model=RecommendationResponse
)
async def generate_recommendations_endpoint(
    request: RecommendationRequest,
    db_pool=Depends(get_db_pool),
    user=Depends(verify_token),
):
    """Generate project recommendations for user.

    Called from Core API via MLServiceClient.
    """
    # REC-02: IDOR fix — verify caller owns the requested user_id or is admin/curator
    check_user_access(user, request.user_id)

    # REC-05: Clamp limit to prevent resource abuse from this endpoint too
    limit = min(max(request.limit, 1), 50)

    logger.info("Generating recommendations for user %s", request.user_id)
    recommendations = await generate_recommendations(
        user_id=request.user_id,
        limit=limit,
        exclude_ids=request.exclude_project_ids or [],
        db_pool=db_pool,
    )
    return RecommendationResponse(
        user_id=request.user_id,
        recommendations=recommendations,
        generated_at=datetime.utcnow(),
    )


@router.get(
    "/api/recommendations/user/{user_id}",
    response_model=RecommendationResponse,
)
async def get_recommendations(
    user_id: str,
    limit: int = 10,
    db_pool=Depends(get_db_pool),
    redis_client=Depends(get_redis_client),
    user=Depends(verify_token),
):
    """Get recommendations for user (GET variant) with caching."""
    check_user_access(user, user_id)

    cached = await try_get_cached_recommendations(redis_client, user_id, limit)
    if cached:
        return cached

    logger.info(
        "Cache MISS for user %s, generating recommendations", user_id
    )
    recommendations = await generate_recommendations(
        user_id=user_id, limit=limit, exclude_ids=[], db_pool=db_pool
    )
    response = RecommendationResponse(
        user_id=user_id,
        recommendations=recommendations,
        generated_at=datetime.utcnow(),
    )
    await cache_recommendations(redis_client, user_id, limit, response)
    return response


async def _do_bulk_refresh(db_pool) -> None:
    """Background task: recalculate recommendations for all active users."""
    logger.info("Background bulk refresh started")
    try:
        async with db_pool.acquire() as conn:
            users_query = """
                SELECT id FROM "Users"
                WHERE "IsActive" = true
                LIMIT 1000
            """
            users = await conn.fetch(users_query)

        refreshed_count = 0
        for user_row in users:
            user_id = str(user_row["id"])
            try:
                await generate_recommendations(
                    user_id=user_id,
                    limit=10,
                    exclude_ids=[],
                    db_pool=db_pool,
                )
                refreshed_count += 1
            except Exception as e:
                logger.warning(
                    "Failed to refresh recommendations for user %s: %s",
                    user_id,
                    e,
                )

        logger.info("Background bulk refresh done: %s/%s users", refreshed_count, len(users))
    except Exception as e:
        logger.error("Background bulk refresh failed: %s", e, exc_info=True)


@router.post("/api/recommendations/refresh", status_code=202)
async def refresh_recommendations(
    background_tasks: BackgroundTasks,
    db_pool=Depends(get_db_pool),
    user=Depends(verify_token),
):
    """Recalculate recommendations for all active users (admin-only).

    REC-14: Returns 202 immediately; actual processing runs as a background task
    to prevent guaranteed HTTP timeout when refreshing up to 1000 users.
    """
    if user.get("role") not in ["admin"]:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Admin access required",
        )
    logger.info("Queuing bulk recommendations refresh for all users")
    background_tasks.add_task(_do_bulk_refresh, db_pool)
    return {
        "success": True,
        "message": "Bulk recommendations refresh queued as background task.",
        "timestamp": datetime.utcnow().isoformat(),
    }


@router.delete("/api/recommendations/cache/{user_id}")
async def invalidate_user_cache(
    user_id: str,
    redis_client=Depends(get_redis_client),
    user=Depends(verify_token),
):
    """Invalidate recommendation cache for a specific user.

    PERF-008: Called from Core API when user profile is updated.
    """
    if user.get("user_id") != user_id and user.get("role") not in [
        "admin",
        "curator",
    ]:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN, detail="Access denied"
        )

    try:
        # REC-10: Validate UUID before using in Redis pattern
        safe_user_id = _validate_uuid(user_id)
        pattern = f"recommendations:user:{safe_user_id}:*"

        # REC-12: Collect all matching keys first, then unlink in a single call
        # to avoid blocking the Redis event loop with N sequential DEL commands
        keys_to_delete = [key async for key in redis_client.scan_iter(match=pattern)]
        if keys_to_delete:
            await redis_client.unlink(*keys_to_delete)

        logger.info(
            "Invalidated %s cache keys for user %s",
            len(keys_to_delete),
            safe_user_id,
        )
        return {
            "success": True,
            "user_id": safe_user_id,
            "deleted_keys": len(keys_to_delete),
            "message": f"Cache invalidated for user {safe_user_id}",
        }
    except HTTPException:
        raise
    except Exception as e:
        logger.error(
            "Failed to invalidate cache for user %s: %s", user_id, e
        )
        raise HTTPException(
            status_code=500,
            detail="Cache invalidation failed. Please try again later.",
        )
