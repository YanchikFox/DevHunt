import { NextRequest, NextResponse } from "next/server"
import { isProxyPathAllowed } from "@/lib/security/proxy-allowlist"
import { getServerAccessToken } from "@/lib/auth/server-session"

function hasRawHeaders(h: unknown): h is { raw(): Record<string, string[]> } {
  return typeof h === "object" && h !== null && "raw" in h && typeof (h as Record<string, unknown>)["raw"] === "function"
}

function buildRequestHeaders(request: NextRequest, accessToken: string | null): Headers {
  const headers = new Headers()
  request.headers.forEach((value, key) => {
    if (key.toLowerCase() !== "host" && key.toLowerCase() !== "connection") {
      headers.set(key, value)
    }
  })
  const cookieHeader = request.headers.get("cookie")
  if (cookieHeader) headers.set("cookie", cookieHeader)
  if (accessToken) headers.set("authorization", `Bearer ${accessToken}`)
  return headers
}

function applySetCookies(response: Response, proxyResponse: NextResponse): void {
  response.headers.forEach((value, key) => {
    if (key.toLowerCase() !== "set-cookie") proxyResponse.headers.set(key, value)
  })
  const setCookieHeaders =
    typeof response.headers.getSetCookie === "function"
      ? response.headers.getSetCookie()
      : hasRawHeaders(response.headers)
        ? (response.headers.raw()["set-cookie"] ?? [])
        : []
  setCookieHeaders.forEach((cookie: string) => proxyResponse.headers.append("set-cookie", cookie))
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params
  return proxyToAuthService(request, path)
}

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params
  return proxyToAuthService(request, path)
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params
  return proxyToAuthService(request, path)
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params
  return proxyToAuthService(request, path)
}

async function proxyToAuthService(request: NextRequest, pathSegments: string[]) {
  if (!isProxyPathAllowed("auth", pathSegments)) {
    return new NextResponse(null, { status: 404 })
  }

  const authServiceUrl =
    process.env.AUTH_SERVICE_URL ||
    (process.env.DOCKER_ENV === "true" || process.env.NEXT_PUBLIC_USE_PROXY === "true"
      ? "http://auth-service:8080"
      : "http://localhost:7001")

  const path = pathSegments.join("/")
  const url = `${authServiceUrl}/api/${path}`

  // Get request body if present
  let body: string | undefined
  if (request.method !== "GET" && request.method !== "HEAD") {
    try {
      body = await request.text()
    } catch (e) {
      console.warn("[Proxy] Failed to read request body", e)
    }
  }

  const accessToken = await getServerAccessToken(request)
  const headers = buildRequestHeaders(request, accessToken)

  try {
    // @ts-expect-error - duplex is needed for streaming but standard fetch types might complain
    const response = await fetch(url, { method: request.method, headers, body, duplex: 'half' })
    const responseBody = await response.text()
    const proxyResponse = new NextResponse(responseBody, {
      status: response.status,
      statusText: response.statusText,
    })
    applySetCookies(response, proxyResponse)
    return proxyResponse
  } catch (error) {
    console.error("[Proxy] Error forwarding request:", error)
    return NextResponse.json({ error: "Internal Server Error", details: String(error) }, { status: 500 })
  }
}
