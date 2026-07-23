import { getToken } from "next-auth/jwt"
import { headers as nextHeaders } from "next/headers"
import type { NextRequest } from "next/server"

/**
 * Reads the access token from the encrypted NextAuth JWT (server-only, not exposed to useSession).
 *
 * @param req - Optional request for route handlers; omit when using in Server Components,
 * where the incoming request headers are read via `next/headers` instead.
 * @returns Bearer access token or null when unauthenticated.
 */
export async function getServerAccessToken(req?: NextRequest): Promise<string | null> {
  const token = await getToken({
    req: req ?? { headers: await nextHeaders() },
    secret: process.env.AUTH_SECRET || process.env.NEXTAUTH_SECRET || process.env.JWT_KEY,
  })

  return typeof token?.accessToken === "string" ? token.accessToken : null
}
