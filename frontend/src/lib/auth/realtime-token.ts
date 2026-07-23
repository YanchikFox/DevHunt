/**
 * Fetches the access token for realtime connections via the BFF route (server reads JWT).
 *
 * @returns Access token string or null when unauthenticated.
 */
export async function fetchRealtimeAccessToken(): Promise<string | null> {
  const response = await fetch("/api/realtime/token", {
    credentials: "include",
    cache: "no-store",
  })

  if (!response.ok) {
    return null
  }

  const payload: unknown = await response.json()
  if (!payload || typeof payload !== "object") {
    return null
  }

  const token = (payload as Record<string, unknown>).token
  return typeof token === "string" ? token : null
}
