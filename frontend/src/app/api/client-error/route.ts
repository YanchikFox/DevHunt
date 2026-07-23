import { NextRequest, NextResponse } from "next/server"

export async function POST(request: NextRequest) {
  try {
    const payload = await request.json().catch(() => null)

    if (process.env.NODE_ENV !== "production") {
      console.error(
        JSON.stringify({
          event: "ClientError",
          payload,
        })
      )
    }
  } catch (error) {
    if (process.env.NODE_ENV !== "production") {
      console.error(
        JSON.stringify({
          event: "ClientError-LoggingFailed",
          message: error instanceof Error ? error.message : String(error),
        })
      )
    }
  }

  return new NextResponse(null, { status: 204 })
}
