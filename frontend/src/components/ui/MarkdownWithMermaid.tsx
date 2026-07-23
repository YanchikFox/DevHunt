"use client"

import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { MermaidDiagram } from "./MermaidDiagram"

interface MarkdownWithMermaidProps {
  readonly content: string
}

export function MarkdownWithMermaid({ content }: MarkdownWithMermaidProps) {
  const parts = content.split(/(```mermaid\n[\s\S]*?```)/g)

  return (
    <div className="space-y-4">
      {parts.map((part, idx) => {
        const key = `md-part-${idx}`
        if (part.startsWith("```mermaid\n")) {
          const mermaidCode = part
            .replace(/^```mermaid\n/, "")
            .replace(/```$/, "")
            .trim()
          return (
            <div key={key} className="rounded-lg border bg-muted/30 p-4">
              <MermaidDiagram code={mermaidCode} />
            </div>
          )
        }
        const trimmed = part.trim()
        if (!trimmed) return null
        return (
          <div key={key} className="prose prose-sm dark:prose-invert max-w-none">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{trimmed}</ReactMarkdown>
          </div>
        )
      })}
    </div>
  )
}
