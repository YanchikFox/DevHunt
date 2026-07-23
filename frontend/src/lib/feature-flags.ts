const env = process.env

// USE_MOCKS: false by default, true only if explicitly set to "true"
// This prevents accidental mock usage when the env var is undefined or empty
export const USE_MOCKS = env.NEXT_PUBLIC_USE_MOCKS === "true"
export const USE_PROXY = env.NEXT_PUBLIC_USE_PROXY === "true"
export const WS_URL = env.NEXT_PUBLIC_WS_URL ?? ""
