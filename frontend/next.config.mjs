import createNextIntlPlugin from "next-intl/plugin"
import path from "path"
import { fileURLToPath } from "url"

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// Use standard next-intl structure
// This resolves to ./src/i18n/request.ts by default if not specified,
// but specifying it explicitly relative to project root is safer for Docker
const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts")

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Включаем standalone режим для оптимизации Docker образа
  output: "standalone",

  // SECURITY: Disable production source maps to protect source code
  // Source maps expose original code and can aid attackers
  productionBrowserSourceMaps: false,

  experimental: {
    optimizePackageImports: ["lucide-react", "@radix-ui/react-dialog"],
  },
  // Bundle optimization (swcMinify is default in Next.js 16)
  compiler: {
    // removeConsole: process.env.NODE_ENV === "production",
  },
  images: {
    // domains is deprecated, using remotePatterns instead
    remotePatterns: [
      {
        protocol: "https",
        hostname: "**.devhunt.io",
      },
      {
        protocol: "https",
        hostname: "api.dicebear.com",
      },
      {
        protocol: "http",
        hostname: "localhost",
        port: "8333",
      },
      {
        protocol: "http",
        hostname: "object-storage",
        port: "8333",
      },
    ],
  },
  // Allow cross-origin requests from ngrok (for development)
  // Note: This option may not exist in Next.js 14, but we'll keep it for future compatibility
  // The warning is non-blocking and can be ignored

  // Proxy API requests to backend services (when running in Docker)
  // This allows frontend to access backend through Next.js server-side proxy
  // Browser makes requests to /api-proxy/*, Next.js proxies to backend
  rewrites() {
    const useDockerTargets =
      process.env.DOCKER_ENV === "true" || process.env.NEXT_PUBLIC_USE_PROXY === "true"

    const coreApi = useDockerTargets ? "http://core-api:8080" : "http://localhost:7002"

    return {
      beforeFiles: [
        // Core/Auth BFF proxies are handled by app route handlers with explicit allowlists (DEV-21).
        // Proxy SignalR Hub (Localized and Root)
        {
          source: "/:locale/chatHub/:path*",
          destination: `${coreApi}/chatHub/:path*`,
        },
        {
          source: "/chatHub/:path*",
          destination: `${coreApi}/chatHub/:path*`,
        },
        {
          source: "/:locale/notificationHub/:path*",
          destination: `${coreApi}/notificationHub/:path*`,
        },
        {
          source: "/notificationHub/:path*",
          destination: `${coreApi}/notificationHub/:path*`,
        },
      ],
    }
  },
  // ESLint configuration moved to separate command
  typescript: {
    ignoreBuildErrors: false,
  },
  // Explicit webpack alias configuration for compatibility
  // Turbopack should read from tsconfig.json, but this ensures compatibility
  webpack: (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      "@": path.resolve(__dirname, "./src"),
    }
    return config
  },
}

export default withNextIntl(nextConfig)
