import { NextResponse } from "next/server"
import { auth } from "@/auth"
import { getServerAccessToken } from "@/lib/auth/server-session"

/**
 * Returns a short-lived access token for SignalR/WebSocket clients.
 * Tokens are read from the server-side JWT and never exposed via useSession().
 */
export async function GET() {
  const session = await auth()
  if (!session?.user?.id) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
  }

  const token = await getServerAccessToken()
  if (!token) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
  }

  return NextResponse.json({ token })
}
