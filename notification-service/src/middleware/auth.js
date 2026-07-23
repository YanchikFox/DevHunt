/**
 * JWT Authentication Middleware
 * Verifies JWT tokens from Authorization header
 */

import jwt from "jsonwebtoken";
import { logger } from "../utils/logger.js";

const JWT_SECRET = process.env.JWT_SECRET;
const JWT_ISSUER = process.env.JWT_ISSUER || "DevHunt.AuthService";
const ENVIRONMENT = (process.env.ENVIRONMENT || process.env.NODE_ENV || "development").toLowerCase();

if (!JWT_SECRET && ENVIRONMENT === "production") {
  throw new Error(
    "JWT_SECRET must be set in production. Authentication cannot be disabled; refusing to start without a signing key.",
  );
}

export function authenticateToken(req, res, next) {
  // Skip auth for health check
  if (req.path === "/health" || req.path === "/") {
    return next();
  }

  // Skip auth if JWT_SECRET not set (development)
  if (!JWT_SECRET) {
    logger.warn("JWT_SECRET not set, authentication disabled (development only)");
    req.user = { user_id: "anonymous", role: "user" };
    return next();
  }

  const authHeader = req.headers.authorization;
  const token = authHeader && authHeader.split(" ")[1]; // Bearer TOKEN

  if (!token) {
    return res.status(401).json({ error: "Authorization token required" });
  }

  try {
    const decoded = jwt.verify(token, JWT_SECRET, {
      issuer: JWT_ISSUER,
      algorithms: ["HS256"],
    });

    req.user = {
      user_id: decoded.sub || decoded.userId,
      role: decoded.role || "user",
    };

    return next();
  } catch (error) {
    if (error.name === "TokenExpiredError") {
      return res.status(401).json({ error: "Token expired" });
    }
    if (error.name === "JsonWebTokenError") {
      return res.status(401).json({ error: "Invalid token" });
    }
    logger.error("Token verification error:", error);
    return res.status(401).json({ error: "Token verification failed" });
  }
}

export function requireRole(...roles) {
  return (req, res, next) => {
    if (!req.user) {
      return res.status(401).json({ error: "Authentication required" });
    }

    if (!roles.includes(req.user.role)) {
      return res.status(403).json({ error: "Insufficient permissions" });
    }

    return next();
  };
}
