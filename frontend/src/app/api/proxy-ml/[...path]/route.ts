import { NextRequest, NextResponse } from "next/server"
import { auth } from "@/auth"
import { isProxyPathAllowed } from "@/lib/security/proxy-allowlist"
import { getServerAccessToken } from "@/lib/auth/server-session"

type RouteParams = { params: Promise<{ path: string[] }> }
type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE" | "OPTIONS" | "HEAD"

export async function GET(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function POST(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function PUT(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function PATCH(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function DELETE(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function OPTIONS(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

export async function HEAD(request: NextRequest, context: RouteParams) {
    return proxyToMLService(request, await context.params)
}

async function proxyToMLService(request: NextRequest, params: { path: string[] }) {
    if (!isProxyPathAllowed("ml", params.path ?? [])) {
        return new NextResponse(null, { status: 404 })
    }

    const session = await auth()
    if (!session?.user) {
        return NextResponse.json({ detail: "Unauthorized" }, { status: 401 })
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

    try {
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

        return proxyResponse
    } catch (error) {
        console.error("ML Service proxy error:", error)
        return NextResponse.json(
            { detail: "ML Service unavailable" },
            { status: 503 }
        )
    }
}

function buildTargetUrl(pathSegments: string[], search: string) {
    let rawBase = process.env.ML_SERVICE_URL

    if (!rawBase) {
        const useDockerTarget =
            process.env.DOCKER_ENV === "true" || process.env.NEXT_PUBLIC_USE_PROXY === "true"
        rawBase = useDockerTarget ? "http://ml-service:8000" : "http://localhost:8000"
    }

    const base = rawBase.replace(/\/+$/, "")
    const normalizedBase = base.endsWith("/api") ? base : `${base}/api`
    const path = pathSegments.join("/")
    const pathSuffix = path.length ? `/${path}` : ""
    return `${normalizedBase}${pathSuffix}${search}`
}
