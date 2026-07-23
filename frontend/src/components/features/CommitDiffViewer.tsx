"use client"

import { useState, useCallback } from "react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Code, Plus, Minus, FileText } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Represents a single line in a file diff.
 */
export interface DiffLine {
  /** Type of change: added, removed, or context (unchanged) */
  type: "added" | "removed" | "context"
  /** Content of the line */
  content: string
  /** Line number in the new version of the file */
  newLineNumber?: number
  /** Line number in the old version of the file */
  oldLineNumber?: number
}

/**
 * Represents a file changed in a commit.
 */
export interface DiffFile {
  /** Path to the file */
  path: string
  /** Number of lines added */
  additions: number
  /** Number of lines deleted */
  deletions: number
  /** List of diff lines */
  lines: DiffLine[]
}

/**
 * Represents a commit diff with metadata and file changes.
 */
export interface CommitDiff {
  /** Unique identifier of the commit */
  commitId: string
  /** Full commit hash */
  hash: string
  /** Commit message */
  message: string
  /** Author name */
  author: string
  /** Commit date */
  date: string
  /** List of changed files */
  files: DiffFile[]
}

/**
 * Props for the CommitDiffViewer component.
 */
export interface CommitDiffViewerProps {
  /** The commit diff data to display */
  diff: CommitDiff
}

/**
 * Displays a commit diff viewer with expandable file sections and syntax highlighting-like styling.
 *
 * @example
 * ```tsx
 * <CommitDiffViewer
 *   diff={{
 *     commitId: "1",
 *     hash: "abc1234",
 *     message: "Fix bug",
 *     author: "John Doe",
 *     date: "2023-01-01",
 *     files: [...]
 *   }}
 * />
 * ```
 */
export function CommitDiffViewer({ diff }: CommitDiffViewerProps) {
  const [expandedFiles, setExpandedFiles] = useState<Set<string>>(new Set())

  const toggleFile = useCallback((path: string) => {
    setExpandedFiles((prev) => {
      const newExpanded = new Set(prev)
      if (newExpanded.has(path)) {
        newExpanded.delete(path)
      } else {
        newExpanded.add(path)
      }
      return newExpanded
    })
  }, [])

  return (
    <Card className="border-2">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <CardTitle className="flex items-center gap-2 mb-2">
              <Code className="h-5 w-5 text-primary" />
              {diff.message}
            </CardTitle>
            <div className="flex items-center gap-4 text-sm text-muted-foreground">
              <span className="font-mono text-blue-600 dark:text-blue-400">
                {diff.hash.slice(0, 7)}
              </span>
              <span>{diff.author}</span>
              <span>{new Date(diff.date).toLocaleString()}</span>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Badge
              variant="secondary"
              className="bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400"
            >
              <Plus className="h-3 w-3 mr-1" />+
              {diff.files.reduce((acc, f) => acc + f.additions, 0)}
            </Badge>
            <Badge
              variant="secondary"
              className="bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400"
            >
              <Minus className="h-3 w-3 mr-1" />-
              {diff.files.reduce((acc, f) => acc + f.deletions, 0)}
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {diff.files.map((file) => (
          <DiffFileItem
            key={file.path}
            file={file}
            isExpanded={expandedFiles.has(file.path)}
            onToggle={toggleFile}
          />
        ))}
      </CardContent>
    </Card>
  )
}

interface DiffFileItemProps {
  file: DiffFile
  isExpanded: boolean
  onToggle: (path: string) => void
}

function DiffFileItem({ file, isExpanded, onToggle }: DiffFileItemProps) {
  const handleToggle = useCallback(() => {
    onToggle(file.path)
  }, [onToggle, file.path])

  return (
    <div className="border rounded-lg overflow-hidden">
      <Button
        variant="ghost"
        className="w-full justify-between p-3 h-auto hover:bg-muted/50"
        onClick={handleToggle}
      >
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <FileText className="h-4 w-4 text-muted-foreground flex-shrink-0" />
          <span className="font-mono text-sm truncate">{file.path}</span>
          <div className="flex items-center gap-2 ml-auto">
            <Badge
              variant="outline"
              className="text-xs bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400"
            >
              +{file.additions}
            </Badge>
            <Badge
              variant="outline"
              className="text-xs bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400"
            >
              -{file.deletions}
            </Badge>
          </div>
        </div>
      </Button>

      {isExpanded && (
        <div className="border-t bg-muted/30">
          <div className="p-4 space-y-1 font-mono text-xs">
            {file.lines.map((line, idx) => (
              <div
                key={`${file.path}-${line.newLineNumber ?? line.oldLineNumber ?? idx}-${line.content.slice(0, 20)}`}
                className={cn(
                  "flex gap-4 px-2 py-0.5",
                  line.type === "added" && "bg-green-500/10 dark:bg-green-500/5",
                  line.type === "removed" && "bg-red-500/10 dark:bg-red-500/5"
                )}
              >
                <div className="flex gap-2 text-muted-foreground w-16 flex-shrink-0">
                  {line.oldLineNumber && (
                    <span
                      className={cn(line.type === "removed" && "text-red-600 dark:text-red-400")}
                    >
                      {line.oldLineNumber}
                    </span>
                  )}
                  {line.newLineNumber && (
                    <span
                      className={cn(line.type === "added" && "text-green-600 dark:text-green-400")}
                    >
                      {line.newLineNumber}
                    </span>
                  )}
                </div>
                <span
                  className={cn(
                    line.type === "added" && "text-green-700 dark:text-green-300",
                    line.type === "removed" && "text-red-700 dark:text-red-300"
                  )}
                >
                  {line.type === "added" && "+"}
                  {line.type === "removed" && "-"}
                  {line.content}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
