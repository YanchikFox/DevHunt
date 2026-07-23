import { ImageResponse } from "next/og"

export const size = {
  width: 1200,
  height: 630,
}

export const contentType = "image/png"

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: 64,
          background: "#0b1220",
          color: "#ffffff",
          fontFamily:
            "ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial",
        }}
      >
        <div style={{ fontSize: 72, fontWeight: 800, lineHeight: 1.05 }}>DevHunt</div>
        <div style={{ fontSize: 32, opacity: 0.9, marginTop: 16, maxWidth: 950 }}>
          Discover talent. Build projects. Ship together.
        </div>
      </div>
    ),
    size
  )
}
