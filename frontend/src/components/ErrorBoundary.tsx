"use client"

import React, { Component, ErrorInfo, ReactNode } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { AlertCircle, RefreshCw, Home } from "lucide-react"
import { Link } from "@/i18n/routing"
import { useTranslations } from "next-intl"

interface Props {
  children: ReactNode
  fallback?: ReactNode
  locale?: string
}

interface InnerProps extends Props {
  translations: {
    title: string
    description: string
    error: string
    details: string
    tryAgain: string
    goHome: string
  }
}

interface State {
  hasError: boolean
  error: Error | null
  errorInfo: ErrorInfo | null
}

/**
 * Global Error Boundary component for catching React errors.
 * UX: Provides user-friendly error UI instead of blank screen.
 * Based on Frontend UX Audit recommendations.
 */
class ErrorBoundaryInner extends Component<InnerProps, State> {
  constructor(props: InnerProps) {
    super(props)
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
    }
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      error,
      errorInfo: null,
    }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    if (process.env.NODE_ENV === "development") {
      console.error("ErrorBoundary caught an error:", error, errorInfo)
    }

    // In production, could send to error reporting service (Sentry, LogRocket, etc.)
    // Example: Sentry.captureException(error, { contexts: { react: errorInfo } })

    this.setState({
      error,
      errorInfo,
    })
  }

  handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
    })
  }

  render() {
    if (this.state.hasError) {
      // Custom fallback UI
      if (this.props.fallback) {
        return this.props.fallback
      }

      const { translations } = this.props

      return (
        <div className="flex min-h-screen items-center justify-center bg-background p-4">
          <Card className="w-full max-w-md">
            <CardHeader>
              <div className="flex items-center gap-2">
                <AlertCircle className="h-6 w-6 text-destructive" aria-hidden="true" />
                <CardTitle>{translations.title}</CardTitle>
              </div>
              <CardDescription>
                {translations.description}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {process.env.NODE_ENV === "development" && this.state.error && (
                <div className="rounded-md bg-muted p-3 text-sm">
                  <p className="font-semibold text-destructive">{translations.error}:</p>
                  <p className="mt-1 font-mono text-xs">{this.state.error.toString()}</p>
                  {this.state.errorInfo && (
                    <details className="mt-2">
                      <summary className="cursor-pointer text-xs">{translations.details}</summary>
                      <pre className="mt-2 overflow-auto text-xs">
                        {this.state.errorInfo.componentStack}
                      </pre>
                    </details>
                  )}
                </div>
              )}

              <div className="flex flex-col gap-2">
                <Button onClick={this.handleReset} className="w-full">
                  <RefreshCw className="mr-2 h-4 w-4" aria-hidden="true" />
                  {translations.tryAgain}
                </Button>
                <Button variant="outline" asChild className="w-full">
                  <Link href="/">
                    <Home className="mr-2 h-4 w-4" aria-hidden="true" />
                    {translations.goHome}
                  </Link>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      )
    }

    return this.props.children
  }
}

export function ErrorBoundary(props: Props) {
  const t = useTranslations("common")
  const navT = useTranslations("navigation")

  const translations = {
    title: t("somethingWentWrong"),
    description: t("unexpectedErrorDesc"),
    error: t("error"),
    details: t("details"),
    tryAgain: t("tryAgain"),
    goHome: t("goHome") || navT("home") || "Home",
  }

  return <ErrorBoundaryInner {...props} translations={translations} />
}
