import { NextRequest, NextResponse } from "next/server"
import { isProxyPathAllowed } from "@/lib/security/proxy-allowlist"
import { getServerAccessToken } from "@/lib/auth/server-session"

type RouteParams = { params: Promise<{ path: string[] }> }
type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE" | "OPTIONS" | "HEAD"

function hasRawHeaders(h: unknown): h is { raw(): Record<string, string[]> } {
  return typeof h === "object" && h !== null && "raw" in h && typeof (h as Record<string, unknown>)["raw"] === "function"
}

export async function GET(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function POST(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function PUT(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function PATCH(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function DELETE(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function OPTIONS(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

export async function HEAD(request: NextRequest, context: RouteParams) {
  return proxyToCoreService(request, await context.params)
}

async function proxyToCoreService(request: NextRequest, params: { path: string[] }) {
  if (!isProxyPathAllowed("core", params.path ?? [])) {
    return new NextResponse(null, { status: 404 })
  }

  const search = request.nextUrl.search
  const targetUrl = buildTargetUrl(params.path ?? [], search)

  const headers = new Headers()
  request.headers.forEach((value, key) => {
    const lower = key.toLowerCase()
    if (
      lower === "host" ||
      lower === "connection" ||
      lower === "content-length" ||
      lower === "accept-encoding"
    ) {
      return
    }
    headers.set(key, value)
  })

  const cookieHeader = request.headers.get("cookie")
  if (cookieHeader) {
    headers.set("cookie", cookieHeader)
  }

  const accessToken = await getServerAccessToken(request)
  if (accessToken) {
    headers.set("authorization", `Bearer ${accessToken}`)
  }

  const hasBody = !["GET", "HEAD", "OPTIONS"].includes(request.method as HttpMethod)
  const body = hasBody ? await request.arrayBuffer() : undefined

  const response = await fetch(targetUrl, {
    method: request.method,
    headers,
    body,
    redirect: "manual",
  })

  const proxyResponse = new NextResponse(response.body, {
    status: response.status,
    statusText: response.statusText,
  })

  response.headers.forEach((value, key) => {
    const lower = key.toLowerCase()
    if (lower === "set-cookie" || lower === "content-encoding" || lower === "content-length") {
      return
    }
    proxyResponse.headers.set(key, value)
  })

  const setCookieHeaders =
    typeof response.headers.getSetCookie === "function"
      ? response.headers.getSetCookie()
      : hasRawHeaders(response.headers)
        ? (response.headers.raw()["set-cookie"] ?? [])
        : []

  for (const cookie of setCookieHeaders) {
    proxyResponse.headers.append("set-cookie", cookie)
  }

  return proxyResponse
}

function buildTargetUrl(pathSegments: string[], search: string) {
  let rawBase = process.env.CORE_SERVICE_URL || process.env.CORE_API_URL

  if (!rawBase) {
    const publicApiUrl = stripTrailingApi(process.env.NEXT_PUBLIC_API_URL)
    if (publicApiUrl?.startsWith("http")) {
      rawBase = publicApiUrl
    } else {
      const useDockerTarget =
        process.env.DOCKER_ENV === "true" || process.env.NEXT_PUBLIC_USE_PROXY === "true"
      rawBase = useDockerTarget ? "http://core-api:8080" : "http://localhost:7002"
    }
  }

  const base = rawBase.replace(/\/+$/, "")
  const normalizedBase = base.endsWith("/api") ? base : `${base}/api`
  const path = pathSegments.join("/")
  const pathSuffix = path.length ? `/${path}` : ""
  return `${normalizedBase}${pathSuffix}${search}`
}

function stripTrailingApi(value?: string | null) {
  if (!value) return undefined
  return value.endsWith("/api") ? value.slice(0, -4) : value
}
