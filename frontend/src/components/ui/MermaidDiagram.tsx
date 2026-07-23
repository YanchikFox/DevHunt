"use client"

import { useEffect, useRef, useState, useMemo, memo } from "react"
import DOMPurify from "dompurify"
import { AlertTriangle, Copy, Download, RefreshCw, Check } from "lucide-react"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"

interface MermaidDiagramProps {
    /** Mermaid diagram code */
    code: string
    /** Additional CSS classes */
    className?: string
    /** Show AI disclaimer */
    showDisclaimer?: boolean
    /** Callback to regenerate the diagram */
    onRegenerate?: () => void
    /** Whether regeneration is in progress */
    isRegenerating?: boolean
}

/**
 * Mermaid Diagram component.
 * Renders Mermaid diagram code with copy/download functionality.
 */
export const MermaidDiagram = memo(function MermaidDiagram({
    code,
    className,
    showDisclaimer = true,
    onRegenerate,
    isRegenerating = false,
}: MermaidDiagramProps) {
    const t = useTranslations()
    const containerRef = useRef<HTMLDivElement>(null)
    const [svg, setSvg] = useState<string>("")
    const [error, setError] = useState<string | null>(null)
    const [copied, setCopied] = useState(false)

    // SECURITY (DEV-92): sanitize the mermaid-generated SVG at the point of injection so the
    // dangerouslySetInnerHTML below is provably safe (mermaid already runs with securityLevel
    // "strict"; this is defense-in-depth and removes the value from any untrusted dataflow).
    const sanitizedSvg = useMemo(
        () => (svg ? DOMPurify.sanitize(svg, { USE_PROFILES: { svg: true, svgFilters: true } }) : ""),
        [svg],
    )

    // Dynamically import and render mermaid
    useEffect(() => {
        let isMounted = true

        const renderDiagram = async () => {
            if (!code) return

            try {
                // Dynamic import of mermaid
                const mermaid = (await import("mermaid")).default

                // Initialize mermaid with secure options
                // SECURITY: Use "strict" to prevent XSS via script injection in diagrams
                mermaid.initialize({
                    startOnLoad: false,
                    theme: "default",
                    securityLevel: "strict",
                    fontFamily: "inherit",
                })

                // Generate unique ID
                const id = `mermaid-${Date.now()}-${Math.random().toString(36).slice(2)}`

                const { svg: renderedSvg } = await mermaid.render(id, code)

                if (isMounted) {
                    setSvg(renderedSvg)
                    setError(null)
                }
            } catch (err) {
                console.error("Mermaid render error:", err)
                if (isMounted) {
                    setError(err instanceof Error ? err.message : "Failed to render diagram")
                    setSvg("")
                }
            }
        }

        renderDiagram()

        return () => {
            isMounted = false
        }
    }, [code])

    const handleCopyCode = async () => {
        try {
            await navigator.clipboard.writeText(code)
            setCopied(true)
            setTimeout(() => setCopied(false), 2000)
        } catch (err) {
            console.error("Copy failed:", err)
        }
    }

    const handleDownloadSvg = () => {
        if (!sanitizedSvg) return

        const blob = new Blob([sanitizedSvg], { type: "image/svg+xml" })
        const url = URL.createObjectURL(blob)
        const a = document.createElement("a")
        a.href = url
        a.download = "architecture-diagram.svg"
        a.click()
        URL.revokeObjectURL(url)
    }

    return (
        <div className={cn("flex flex-col rounded-lg border bg-card", className)}>
            {/* AI Disclaimer */}
            {showDisclaimer && (
                <div className="px-3 py-2 bg-amber-50 dark:bg-amber-950/30 border-b border-amber-200 dark:border-amber-800 rounded-t-lg flex items-center gap-2">
                    <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
                    <p className="text-xs text-amber-700 dark:text-amber-300">
                        {t("ai.diagramDisclaimer") || "AI-generated diagram. Review for accuracy."}
                    </p>
                </div>
            )}

            {/* Toolbar */}
            <div className="flex items-center justify-between px-3 py-2 border-b bg-muted/30">
                <span className="text-sm font-medium text-muted-foreground">
                    {t("ai.architectureDiagram") || "Architecture Diagram"}
                </span>
                <div className="flex items-center gap-1">
                    {onRegenerate && (
                        <Button
                            variant="ghost"
                            size="sm"
                            onClick={onRegenerate}
                            disabled={isRegenerating}
                            className="h-8 px-2"
                        >
                            <RefreshCw className={cn("h-4 w-4", isRegenerating && "animate-spin")} />
                        </Button>
                    )}
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={handleCopyCode}
                        className="h-8 px-2"
                    >
                        {copied ? <Check className="h-4 w-4 text-green-500" /> : <Copy className="h-4 w-4" />}
                    </Button>
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={handleDownloadSvg}
                        disabled={!sanitizedSvg}
                        className="h-8 px-2"
                        title="Download SVG"
                    >
                        <Download className="h-4 w-4" />
                    </Button>
                </div>
            </div>

            {/* Diagram Content */}
            <div
                ref={containerRef}
                className="p-4 overflow-auto min-h-[200px] flex items-center justify-center"
            >
                {error ? (
                    <div className="text-center text-destructive">
                        <AlertTriangle className="h-8 w-8 mx-auto mb-2 opacity-50" />
                        <p className="text-sm font-medium">Failed to render diagram</p>
                        <p className="text-xs mt-1 max-w-[300px]">{error}</p>
                        <details className="mt-3 text-left">
                            <summary className="text-xs text-muted-foreground cursor-pointer">
                                View raw code
                            </summary>
                            <pre className="mt-2 p-2 bg-muted rounded text-xs overflow-auto max-h-[200px]">
                                {code}
                            </pre>
                        </details>
                    </div>
                ) : sanitizedSvg ? (
                    <div
                        className="mermaid-container"
                        dangerouslySetInnerHTML={{ __html: sanitizedSvg }}
                    />
                ) : (
                    <div className="animate-pulse flex flex-col items-center gap-2">
                        <div className="w-32 h-32 bg-muted rounded" />
                        <div className="w-24 h-4 bg-muted rounded" />
                    </div>
                )}
            </div>

            {/* Code Preview (collapsed by default) */}
            {!error && code && (
                <details className="border-t">
                    <summary className="px-3 py-2 text-xs text-muted-foreground cursor-pointer hover:bg-muted/50 transition-colors">
                        {t("ai.viewCode") || "View Mermaid code"}
                    </summary>
                    <pre className="px-3 py-2 text-xs overflow-auto max-h-[200px] bg-muted/30">
                        {code}
                    </pre>
                </details>
            )}
        </div>
    )
})
