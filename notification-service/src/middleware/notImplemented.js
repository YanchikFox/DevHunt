/**
 * Returns 501 for endpoints that are not implemented in the notification service (DEV-25).
 * In-app notification state is owned by Core API / PostgreSQL.
 *
 * @param {string} feature - Human-readable feature name for logs and clients.
 * @returns Express middleware.
 */
export function notImplemented(feature) {
  return (_req, res) => {
    res.status(501).json({
      error: "Not Implemented",
      message: `${feature} is not implemented in notification-service. Use Core API /api/notifications instead.`,
      code: "NOT_IMPLEMENTED",
    });
  };
}
