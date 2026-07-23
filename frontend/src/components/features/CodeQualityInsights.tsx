"use client"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog"
import { CheckCircle2, AlertTriangle, Info, Zap, Loader2, ChevronDown, ChevronRight, FileCode, Shield, Gauge, Wrench, Bug, ExternalLink, Search, EyeOff, Eye, X, Flag } from "lucide-react";
import {
  useLatestCodeAnalysis,
  useAnalysisConfig,
  useDismissIssue,
  useUndismissIssue,
  type CodeAnalysisResult,
  type SeverityCounts,
  type AnalysisIssue,
  type DismissedIssue,
} from "@/lib/api/queries/code-analysis";
import { useCallback, useMemo, useState } from "react"

/**
 * Represents a single code quality metric.
 */
export interface QualityMetric {
  /** Name of the metric (e.g., "Test Coverage") */
  name: string
  /** Current value of the metric */
  value: number
  /** Target threshold value */
  threshold: number
  /** Status of the metric */
  status: "good" | "warning" | "poor"
  /** Description of what the metric measures */
  description: string
  /** List of actionable suggestions to improve the metric */
  suggestions?: string[]
}

/**
 * Props for the CodeQualityInsights component.
 */
export interface CodeQualityInsightsProps {
  /** List of quality metrics to display (fallback when no projectId) */
  metrics?: QualityMetric[]
  /** Project ID to fetch real analysis data */
  projectId?: string
}

/**
 * Derive quality metrics from a real code analysis result.
 */
function deriveMetricsFromAnalysis(result: CodeAnalysisResult): QualityMetric[] {
  const sev = result.severityCounts ?? ({} as SeverityCounts)
  const criticalCount = sev.critical ?? 0
  const highCount = sev.high ?? 0
  const totalFiles = result.totalFiles || 1
  const totalSecurity = (result.categoryCounts?.["security"] ?? 0)

  // Ratio-based scoring (like SonarQube) — score = 100 * e^(-ratio * k)
  // This gives smooth decay: 0 issues → 100%, few → 70-90%, many → 30-50%, extreme → 0-20%

  // Security: issues per file, harsher coefficient
  const secPerFile = totalSecurity / totalFiles
  const securityScore = Math.round(100 * Math.exp(-secPerFile * 2))

  // Code health: total issues per file
  const issuesPerFile = result.totalIssues / totalFiles
  const codeHealthScore = Math.round(100 * Math.exp(-issuesPerFile * 0.15))

  const metrics: QualityMetric[] = [
    {
      name: "Security Score",
      value: securityScore,
      threshold: 80,
      status: securityScore >= 70 ? "good" : securityScore >= 40 ? "warning" : "poor",
      description: `${totalSecurity} security issues found (${criticalCount} critical, ${highCount} high)`,
      suggestions: criticalCount > 0
        ? ["Fix critical vulnerabilities immediately", "Review security rule violations"]
        : highCount > 0
          ? ["Address high-severity security issues"]
          : undefined,
    },
    {
      name: "Code Health",
      value: codeHealthScore,
      threshold: 70,
      status: codeHealthScore >= 70 ? "good" : codeHealthScore >= 40 ? "warning" : "poor",
      description: `${result.totalIssues} issues across ${result.totalFiles} files (${issuesPerFile.toFixed(1)} per file)`,
      suggestions: codeHealthScore < 70
        ? ["Focus on files with highest issue density", "Enable auto-fix suggestions"]
        : undefined,
    },
  ]

  // Add category breakdowns as separate metrics
  const cats = result.categoryCounts ?? {}
  if (cats["performance"] !== undefined) {
    const perfIssues = cats["performance"]
    const perfPerFile = perfIssues / totalFiles
    const perfScore = Math.round(100 * Math.exp(-perfPerFile * 1.5))
    metrics.push({
      name: "Performance",
      value: perfScore,
      threshold: 80,
      status: perfScore >= 70 ? "good" : perfScore >= 40 ? "warning" : "poor",
      description: `${perfIssues} performance issues detected`,
    })
  }

  if (cats["maintainability"] !== undefined || cats["complexity"] !== undefined) {
    const maintIssues = (cats["maintainability"] ?? 0) + (cats["complexity"] ?? 0)
    const maintPerFile = maintIssues / totalFiles
    const maintScore = Math.round(100 * Math.exp(-maintPerFile * 0.4))
    metrics.push({
      name: "Maintainability",
      value: maintScore,
      threshold: 70,
      status: maintScore >= 70 ? "good" : maintScore >= 40 ? "warning" : "poor",
      description: `${maintIssues} maintainability/complexity issues`,
      suggestions: maintScore < 70
        ? ["Reduce nesting depth", "Extract complex methods into smaller functions"]
        : undefined,
    })
  }

  return metrics
}



const getStatusConfig = (status: string) => {
  switch (status) {
    case "good":
      return {
        color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400",
        icon: CheckCircle2,
        borderColor: "border-green-300 dark:border-green-700",
      }
    case "warning":
      return {
        color: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400",
        icon: AlertTriangle,
        borderColor: "border-yellow-300 dark:border-yellow-700",
      }
    case "poor":
      return {
        color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400",
        icon: AlertTriangle,
        borderColor: "border-red-300 dark:border-red-700",
      }
    default:
      return {
        color: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400",
        icon: Info,
        borderColor: "border-blue-300 dark:border-blue-700",
      }
  }
}

const SEVERITY_ORDER = ["critical", "high", "medium", "low", "info"] as const

const SEVERITY_CONFIG: Record<string, { label: string; color: string; dotColor: string }> = {
  critical: { label: "Critical", color: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300", dotColor: "bg-red-500" },
  high: { label: "High", color: "bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-300", dotColor: "bg-orange-500" },
  medium: { label: "Medium", color: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300", dotColor: "bg-yellow-500" },
  low: { label: "Low", color: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300", dotColor: "bg-blue-500" },
  info: { label: "Info", color: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300", dotColor: "bg-gray-400" },
}

const CATEGORY_CONFIG: Record<string, { label: string; icon: typeof Shield }> = {
  security: { label: "Security", icon: Shield },
  performance: { label: "Performance", icon: Gauge },
  quality: { label: "Quality", icon: CheckCircle2 },
  maintainability: { label: "Maintainability", icon: Wrench },
  reliability: { label: "Reliability", icon: Bug },
}

/** Normalize file path — strip repo prefix, show relative path */
function shortenFilePath(filePath: string): string {
  // Strip common prefixes like /tmp/devhunt-xxx/repo/ or absolute paths
  const parts = filePath.replace(/\\/g, "/")
  const repoIdx = parts.indexOf("/repo/")
  if (repoIdx !== -1) return parts.slice(repoIdx + 6)
  // Fallback: take last 3 segments
  const segments = parts.split("/")
  return segments.length > 3 ? segments.slice(-3).join("/") : parts
}

/** Single expandable issue row */
function IssueRow({
  issue,
  isDismissed,
  onDismiss,
  onUndismiss,
}: {
  issue: AnalysisIssue
  isDismissed?: boolean
  onDismiss?: (issue: AnalysisIssue, reason: string) => void
  onUndismiss?: (issue: AnalysisIssue) => void
}) {
  const [expanded, setExpanded] = useState(false)
  const [showDismissInput, setShowDismissInput] = useState(false)
  const [dismissReason, setDismissReason] = useState("")
  const sevConfig = SEVERITY_CONFIG[issue.severity] ?? SEVERITY_CONFIG.info
  const catConfig = CATEGORY_CONFIG[issue.category]
  const filePath = issue.file_path ?? issue.file ?? ""
  const shortPath = shortenFilePath(filePath)

  const handleToggle = useCallback(() => {
    setExpanded((prev) => !prev)
  }, [])

  const handleDismissClick = useCallback(() => {
    setShowDismissInput(true)
  }, [])

  const handleDismissSubmit = useCallback(() => {
    onDismiss?.(issue, dismissReason || "False positive")
    setShowDismissInput(false)
    setDismissReason("")
  }, [issue, dismissReason, onDismiss])

  const handleDismissCancel = useCallback(() => {
    setShowDismissInput(false)
    setDismissReason("")
  }, [])

  const handleUndismiss = useCallback(() => {
    onUndismiss?.(issue)
  }, [issue, onUndismiss])

  const handleReasonChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDismissReason(e.target.value)
  }, [])

  return (
    <div className={`border rounded-lg overflow-hidden transition-all hover:shadow-sm ${isDismissed ? "opacity-50" : ""}`}>
      <button
        type="button"
        onClick={handleToggle}
        className="w-full flex items-start gap-3 p-3 text-left hover:bg-muted/50 transition-colors"
      >
        <div className="mt-0.5 shrink-0">
          {expanded ? <ChevronDown className="h-4 w-4 text-muted-foreground" /> : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
        </div>
        <div className="flex-1 min-w-0 space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold ${sevConfig.color}`}>
              <span className={`w-1.5 h-1.5 rounded-full ${sevConfig.dotColor}`} />
              {sevConfig.label}
            </span>
            {catConfig && (
              <span className="text-[10px] text-muted-foreground font-medium uppercase tracking-wider">
                {catConfig.label}
              </span>
            )}
            {issue.cwe_id && (
              <span className="text-[10px] text-muted-foreground font-mono">{issue.cwe_id}</span>
            )}
            {isDismissed && (
              <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-muted text-muted-foreground">
                <EyeOff className="h-3 w-3" />
                Dismissed
              </span>
            )}
            <span className="text-[10px] text-muted-foreground font-mono ml-auto shrink-0">
              {issue.rule_name ?? issue.rule_id}
            </span>
          </div>
          <p className="text-sm text-foreground leading-snug line-clamp-2">{issue.message}</p>
          <div className="flex items-center gap-1 text-[11px] text-muted-foreground">
            <FileCode className="h-3 w-3" />
            <span className="font-mono truncate">{shortPath}</span>
            <span className="font-mono">:{issue.line}</span>
          </div>
        </div>
      </button>

      {expanded && (
        <div className="px-3 pb-3 pt-0 ml-7 space-y-3 border-t bg-muted/20">
          {/* Code snippet */}
          {issue.snippet && (
            <div className="mt-3">
              <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider mb-1">Code</p>
              <pre className="text-xs font-mono bg-zinc-950 text-zinc-200 dark:bg-zinc-900 rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-all leading-relaxed">
                <span className="text-zinc-500 select-none mr-3">{issue.line}</span>
                {issue.snippet}
              </pre>
            </div>
          )}

          {/* Full message */}
          <div className="mt-2">
            <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider mb-1">Description</p>
            <p className="text-sm text-foreground/90 leading-relaxed">{issue.message}</p>
          </div>

          {/* Fix suggestion */}
          {issue.suggestion && (
            <div>
              <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider mb-1">How to fix</p>
              <div className="text-sm text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-900/20 rounded-md p-2.5 leading-relaxed">
                {issue.suggestion}
              </div>
            </div>
          )}

          {/* Metadata */}
          <div className="flex items-center gap-3 text-[10px] text-muted-foreground pt-1">
            <span className="font-mono">{issue.rule_id}</span>
            {issue.cwe_id && <span className="font-mono">{issue.cwe_id}</span>}
            {issue.column != null && <span>Col {issue.column}</span>}
            {issue.end_line != null && <span>Lines {issue.line}-{issue.end_line}</span>}
          </div>

          {/* Dismiss / Undismiss action */}
          <div className="pt-2 border-t">
            {isDismissed ? (
              <button
                type="button"
                onClick={handleUndismiss}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-muted hover:bg-muted/80 text-foreground transition-colors"
              >
                <Eye className="h-3.5 w-3.5" />
                Re-open issue
              </button>
            ) : showDismissInput ? (
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  placeholder="Reason (e.g. False positive, Test fixture...)"
                  value={dismissReason}
                  onChange={handleReasonChange}
                  className="flex-1 px-2.5 py-1.5 text-xs rounded-md border bg-background focus:outline-none focus:ring-2 focus:ring-primary/30"
                  autoFocus
                />
                <button
                  type="button"
                  onClick={handleDismissSubmit}
                  className="px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
                >
                  Dismiss
                </button>
                <button
                  type="button"
                  onClick={handleDismissCancel}
                  className="px-2 py-1.5 text-xs rounded-md hover:bg-muted transition-colors"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>
            ) : (
              <button
                type="button"
                onClick={handleDismissClick}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md border hover:bg-muted text-muted-foreground transition-colors"
              >
                <Flag className="h-3.5 w-3.5" />
                Report false positive
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

/** How many full-detail issues to show per rule group (critical always shows all) */
const DETAIL_LIMIT = 10
/** How many groups to show initially */
const GROUPS_PAGE_SIZE = 15

/** Check if an issue matches a dismissed entry */
function isIssueDismissed(issue: AnalysisIssue, dismissed: DismissedIssue[]): boolean {
  const file = issue.file_path ?? issue.file ?? ""
  const shortFile = shortenFilePath(file)
  return dismissed.some(
    (d) =>
      d.ruleId === issue.rule_id &&
      (d.file == null || d.file === file || d.file === shortFile) &&
      (d.line == null || d.line === issue.line),
  )
}

/** A group of issues sharing the same rule_id */
interface RuleGroup {
  ruleId: string
  ruleName: string
  severity: string
  category: string
  message: string
  issues: AnalysisIssue[]
}

/** Compact file reference for collapsed issues */
function CompactFileList({
  issues,
  onDismiss,
}: {
  issues: AnalysisIssue[]
  onDismiss?: (issue: AnalysisIssue, reason: string) => void
}) {
  const [expanded, setExpanded] = useState(false)
  const displayIssues = expanded ? issues : issues.slice(0, 5)
  const hasMore = issues.length > 5

  const handleToggle = useCallback(() => {
    setExpanded((prev) => !prev)
  }, [])

  return (
    <div className="ml-7 mt-1 space-y-0.5">
      <p className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider mb-1">
        + {issues.length} more locations:
      </p>
      <div className="grid gap-0.5">
        {displayIssues.map((issue, idx) => {
          const filePath = issue.file_path ?? issue.file ?? ""
          const shortPath = shortenFilePath(filePath)
          const lineRange = issue.end_line ? `${issue.line}-${issue.end_line}` : `${issue.line}`
          return (
            <div key={`${filePath}-${issue.line}-${idx}`} className="flex items-center gap-2 group">
              <span className="text-xs font-mono text-muted-foreground truncate">
                <FileCode className="inline h-3 w-3 mr-1 opacity-60" />
                {shortPath}:<span className="text-foreground/70">{lineRange}</span>
              </span>
              {onDismiss && (
                <button
                  type="button"
                  onClick={() => onDismiss(issue, "False positive")}
                  className="opacity-0 group-hover:opacity-100 text-[10px] text-muted-foreground hover:text-primary transition-opacity"
                  title="Dismiss"
                >
                  <Flag className="h-3 w-3" />
                </button>
              )}
            </div>
          )
        })}
      </div>
      {hasMore && !expanded && (
        <button
          type="button"
          onClick={handleToggle}
          className="text-[11px] text-primary hover:underline mt-1"
        >
          Show all {issues.length} locations
        </button>
      )}
      {expanded && hasMore && (
        <button
          type="button"
          onClick={handleToggle}
          className="text-[11px] text-primary hover:underline mt-1"
        >
          Collapse
        </button>
      )}
    </div>
  )
}

/** A collapsible group of issues for a single rule */
function RuleGroupSection({
  group,
  dismissedIssues,
  onDismiss,
  onUndismiss,
}: {
  group: RuleGroup
  dismissedIssues: DismissedIssue[]
  onDismiss?: (issue: AnalysisIssue, reason: string) => void
  onUndismiss?: (issue: AnalysisIssue) => void
}) {
  const [collapsed, setCollapsed] = useState(true)
  const sevConfig = SEVERITY_CONFIG[group.severity] ?? SEVERITY_CONFIG.info
  const catConfig = CATEGORY_CONFIG[group.category]
  const isCritical = group.severity === "critical"

  // Critical: show all, otherwise show DETAIL_LIMIT
  const detailLimit = isCritical ? group.issues.length : DETAIL_LIMIT
  const detailIssues = group.issues.slice(0, detailLimit)
  const compactIssues = group.issues.slice(detailLimit)

  const handleToggleCollapsed = useCallback(() => {
    setCollapsed((prev) => !prev)
  }, [])

  return (
    <div className="border rounded-lg overflow-hidden">
      {/* Group header */}
      <button
        type="button"
        onClick={handleToggleCollapsed}
        className="w-full flex items-start gap-3 p-3 text-left hover:bg-muted/50 transition-colors"
      >
        <div className="mt-0.5 shrink-0">
          {collapsed ? <ChevronRight className="h-4 w-4 text-muted-foreground" /> : <ChevronDown className="h-4 w-4 text-muted-foreground" />}
        </div>
        <div className="flex-1 min-w-0 space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold ${sevConfig.color}`}>
              <span className={`w-1.5 h-1.5 rounded-full ${sevConfig.dotColor}`} />
              {sevConfig.label}
            </span>
            {catConfig && (
              <span className="text-[10px] text-muted-foreground font-medium uppercase tracking-wider">
                {catConfig.label}
              </span>
            )}
            <Badge variant="outline" className="text-[10px] px-1.5 py-0">
              {group.issues.length} {group.issues.length === 1 ? "issue" : "issues"}
            </Badge>
            <span className="text-[10px] text-muted-foreground font-mono ml-auto shrink-0">
              {group.ruleName}
            </span>
          </div>
          <p className="text-sm text-foreground leading-snug line-clamp-2">{group.message}</p>
        </div>
      </button>

      {/* Expanded content */}
      {!collapsed && (
        <div className="border-t bg-muted/10 p-2 space-y-2">
          {/* Full detail issues */}
          {detailIssues.map((issue, idx) => (
            <IssueRow
              key={`${issue.rule_id}-${issue.file ?? issue.file_path}-${issue.line}-${idx}`}
              issue={issue}
              isDismissed={isIssueDismissed(issue, dismissedIssues)}
              onDismiss={onDismiss}
              onUndismiss={onUndismiss}
            />
          ))}

          {/* Compact list for remaining issues */}
          {compactIssues.length > 0 && (
            <CompactFileList issues={compactIssues} onDismiss={onDismiss} />
          )}
        </div>
      )}
    </div>
  )
}

/** Full-screen issues dialog */
function IssuesDialog({
  open,
  onOpenChange,
  issues,
  dismissedIssues,
  repository,
  branch,
  onDismiss,
  onUndismiss,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  issues: AnalysisIssue[]
  dismissedIssues: DismissedIssue[]
  repository?: string
  branch?: string
  onDismiss?: (issue: AnalysisIssue, reason: string) => void
  onUndismiss?: (issue: AnalysisIssue) => void
}) {
  const [severityFilter, setSeverityFilter] = useState<string | null>(null)
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState("")
  const [visibleGroupCount, setVisibleGroupCount] = useState(GROUPS_PAGE_SIZE)
  const [showDismissed, setShowDismissed] = useState(false)

  const filteredIssues = useMemo(() => {
    let result = issues
    if (!showDismissed) {
      result = result.filter((i) => !isIssueDismissed(i, dismissedIssues))
    }
    if (severityFilter) {
      result = result.filter((i) => i.severity === severityFilter)
    }
    if (categoryFilter) {
      result = result.filter((i) => i.category === categoryFilter)
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase()
      result = result.filter(
        (i) =>
          i.message.toLowerCase().includes(q) ||
          (i.file_path ?? i.file ?? "").toLowerCase().includes(q) ||
          i.rule_id.toLowerCase().includes(q) ||
          (i.rule_name ?? "").toLowerCase().includes(q),
      )
    }
    return result
  }, [issues, dismissedIssues, severityFilter, categoryFilter, searchQuery, showDismissed])

  // Group issues by rule_id, sorted by severity then count
  const ruleGroups = useMemo(() => {
    const groupMap = new Map<string, RuleGroup>()
    for (const issue of filteredIssues) {
      const existing = groupMap.get(issue.rule_id)
      if (existing) {
        existing.issues.push(issue)
      } else {
        groupMap.set(issue.rule_id, {
          ruleId: issue.rule_id,
          ruleName: issue.rule_name ?? issue.rule_id,
          severity: issue.severity,
          category: issue.category,
          message: issue.message,
          issues: [issue],
        })
      }
    }
    const sevIdx = (s: string) => SEVERITY_ORDER.indexOf(s as typeof SEVERITY_ORDER[number])
    return Array.from(groupMap.values()).sort((a, b) => {
      const sevDiff = sevIdx(a.severity) - sevIdx(b.severity)
      if (sevDiff !== 0) return sevDiff
      return b.issues.length - a.issues.length
    })
  }, [filteredIssues])

  const visibleGroups = useMemo(
    () => ruleGroups.slice(0, visibleGroupCount),
    [ruleGroups, visibleGroupCount],
  )

  const dismissedCount = useMemo(
    () => issues.filter((i) => isIssueDismissed(i, dismissedIssues)).length,
    [issues, dismissedIssues],
  )

  const severitySummary = useMemo(() => {
    const counts: Record<string, number> = {}
    for (const issue of issues) {
      if (!showDismissed && isIssueDismissed(issue, dismissedIssues)) continue
      counts[issue.severity] = (counts[issue.severity] ?? 0) + 1
    }
    return counts
  }, [issues, dismissedIssues, showDismissed])

  const categorySummary = useMemo(() => {
    const counts: Record<string, number> = {}
    for (const issue of issues) {
      if (!showDismissed && isIssueDismissed(issue, dismissedIssues)) continue
      counts[issue.category] = (counts[issue.category] ?? 0) + 1
    }
    return counts
  }, [issues, dismissedIssues, showDismissed])

  const handleSeverityFilter = useCallback((sev: string) => {
    setSeverityFilter((prev) => (prev === sev ? null : sev))
    setVisibleGroupCount(GROUPS_PAGE_SIZE)
  }, [])

  const handleCategoryFilter = useCallback((cat: string) => {
    setCategoryFilter((prev) => (prev === cat ? null : cat))
    setVisibleGroupCount(GROUPS_PAGE_SIZE)
  }, [])

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
    setVisibleGroupCount(GROUPS_PAGE_SIZE)
  }, [])

  const handleShowMore = useCallback(() => {
    setVisibleGroupCount((prev) => prev + GROUPS_PAGE_SIZE)
  }, [])

  const handleToggleDismissed = useCallback(() => {
    setShowDismissed((prev) => !prev)
    setVisibleGroupCount(GROUPS_PAGE_SIZE)
  }, [])

  const handleClearFilters = useCallback(() => {
    setSeverityFilter(null)
    setCategoryFilter(null)
    setSearchQuery("")
    setVisibleGroupCount(GROUPS_PAGE_SIZE)
  }, [])

  const activeCount = issues.length - dismissedCount
  const hasActiveFilters = severityFilter || categoryFilter || searchQuery.trim()

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-5xl w-[95vw] h-[90vh] flex flex-col p-0 gap-0">
        {/* Header */}
        <DialogHeader className="px-6 pt-6 pb-4 border-b shrink-0">
          <DialogTitle className="flex items-center gap-2">
            <Bug className="h-5 w-5 text-primary" />
            Issue Details
            <Badge variant="outline" className="ml-2 text-xs">
              {activeCount} active
            </Badge>
            <Badge variant="outline" className="text-xs text-muted-foreground">
              {ruleGroups.length} rules
            </Badge>
            {dismissedCount > 0 && (
              <Badge variant="outline" className="text-xs text-muted-foreground">
                {dismissedCount} dismissed
              </Badge>
            )}
          </DialogTitle>
          <DialogDescription>
            {repository && <span>{repository}</span>}
            {branch && <span> · {branch}</span>}
            {hasActiveFilters && (
              <span> · Showing {filteredIssues.length} of {showDismissed ? issues.length : activeCount}</span>
            )}
          </DialogDescription>
        </DialogHeader>

        {/* Filters bar */}
        <div className="px-6 py-3 border-b shrink-0 space-y-2.5 bg-muted/30">
          {/* Search + show dismissed toggle */}
          <div className="flex items-center gap-2">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                type="text"
                placeholder="Search by message, file, or rule..."
                value={searchQuery}
                onChange={handleSearchChange}
                className="w-full pl-9 pr-3 py-2 text-sm rounded-lg border bg-background focus:outline-none focus:ring-2 focus:ring-primary/30"
              />
            </div>
            {dismissedCount > 0 && (
              <button
                type="button"
                onClick={handleToggleDismissed}
                className={`inline-flex items-center gap-1.5 px-3 py-2 text-xs font-medium rounded-lg border transition-all shrink-0 ${
                  showDismissed
                    ? "bg-primary/10 text-primary border-primary/30"
                    : "bg-background text-muted-foreground hover:bg-muted"
                }`}
              >
                {showDismissed ? <Eye className="h-3.5 w-3.5" /> : <EyeOff className="h-3.5 w-3.5" />}
                {showDismissed ? "Showing dismissed" : "Show dismissed"}
              </button>
            )}
          </div>

          {/* Severity filters */}
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Severity:</span>
            {SEVERITY_ORDER.map((sev) => {
              const count = severitySummary[sev]
              if (!count) return null
              const cfg = SEVERITY_CONFIG[sev]
              const isActive = severityFilter === sev
              return (
                <button
                  key={sev}
                  type="button"
                  onClick={() => handleSeverityFilter(sev)}
                  className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium transition-all ${
                    isActive
                      ? `${cfg.color} ring-2 ring-primary/30`
                      : "bg-background border text-muted-foreground hover:bg-muted"
                  }`}
                >
                  <span className={`w-1.5 h-1.5 rounded-full ${cfg.dotColor}`} />
                  {cfg.label} ({count})
                </button>
              )
            })}
          </div>

          {/* Category filters */}
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Category:</span>
            {Object.entries(categorySummary)
              .sort(([, a], [, b]) => b - a)
              .map(([cat, count]) => {
                const cfg = CATEGORY_CONFIG[cat]
                const isActive = categoryFilter === cat
                return (
                  <button
                    key={cat}
                    type="button"
                    onClick={() => handleCategoryFilter(cat)}
                    className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium transition-all ${
                      isActive
                        ? "bg-primary/10 text-primary ring-2 ring-primary/30"
                        : "bg-background border text-muted-foreground hover:bg-muted"
                    }`}
                  >
                    {cfg?.label ?? cat} ({count})
                  </button>
                )
              })}

            {hasActiveFilters && (
              <button
                type="button"
                onClick={handleClearFilters}
                className="text-[11px] text-primary hover:underline ml-2"
              >
                Clear all
              </button>
            )}
          </div>
        </div>

        {/* Scrollable grouped issues list */}
        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-2">
          {visibleGroups.map((group) => (
            <RuleGroupSection
              key={group.ruleId}
              group={group}
              dismissedIssues={dismissedIssues}
              onDismiss={onDismiss}
              onUndismiss={onUndismiss}
            />
          ))}

          {visibleGroupCount < ruleGroups.length && (
            <button
              type="button"
              onClick={handleShowMore}
              className="w-full py-2.5 text-sm font-medium text-primary hover:bg-primary/5 rounded-lg border border-dashed transition-colors"
            >
              Show more rules ({ruleGroups.length - visibleGroupCount} remaining)
            </button>
          )}

          {filteredIssues.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
              <Search className="h-8 w-8 mb-3 opacity-40" />
              <p className="text-sm font-medium">No issues match your filters</p>
              <p className="text-xs mt-1">Try adjusting the severity, category, or search query</p>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Displays a dashboard of code quality metrics with status indicators and suggestions.
 *
 * @example
 * ```tsx
 * <CodeQualityInsights
 *   metrics={[
 *     { name: "Coverage", value: 80, threshold: 70, status: "good", description: "..." }
 *   ]}
 * />
 * ```
 */
export function CodeQualityInsights({ metrics, projectId }: CodeQualityInsightsProps) {
  const { data: analysisResult, isLoading, isError: _isError } = useLatestCodeAnalysis(projectId)
  const { data: analysisConfig } = useAnalysisConfig(projectId)
  const dismissMutation = useDismissIssue(projectId)
  const undismissMutation = useUndismissIssue(projectId)

  const [issuesDialogOpen, setIssuesDialogOpen] = useState(false)

  const derivedMetrics = analysisResult
    ? deriveMetricsFromAnalysis(analysisResult)
    : metrics ?? null

  const issues = analysisResult?.issues ?? []
  const dismissedIssues = analysisConfig?.dismissedIssues ?? []

  const handleOpenIssues = useCallback(() => {
    setIssuesDialogOpen(true)
  }, [])

  const handleDismiss = useCallback((issue: AnalysisIssue, reason: string) => {
    const file = issue.file_path ?? issue.file
    dismissMutation.mutate({
      ruleId: issue.rule_id,
      file: file ? shortenFilePath(file) : undefined,
      line: issue.line,
      reason,
    })
  }, [dismissMutation])

  const handleUndismiss = useCallback((issue: AnalysisIssue) => {
    const file = issue.file_path ?? issue.file
    undismissMutation.mutate({
      ruleId: issue.rule_id,
      file: file ? shortenFilePath(file) : undefined,
      line: issue.line,
    })
  }, [undismissMutation])

  const renderCommitInfo = useCallback(() => {
    if (!analysisResult) return null
    return (
      <span className="mt-1 block text-xs text-muted-foreground">
        {analysisResult.branch && <span>Branch: <strong>{analysisResult.branch}</strong></span>}
        {analysisResult.commitSha && <span> · Commit: <code>{analysisResult.commitSha.slice(0, 7)}</code></span>}
        {analysisResult.createdAt && <span> · {new Date(analysisResult.createdAt).toLocaleDateString()}</span>}
      </span>
    )
  }, [analysisResult])

  if (isLoading && projectId) {
    return (
      <Card className="border-2">
        <CardContent className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          <span className="ml-2 text-muted-foreground">Analyzing code quality...</span>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Zap className="h-5 w-5 text-primary" />
          Code Quality Insights
          {analysisResult && (
            <Badge variant="outline" className="ml-auto text-xs">
              {analysisResult.totalFiles} files scanned
            </Badge>
          )}
        </CardTitle>
        <CardDescription>
          {analysisResult
            ? "Analysis powered by DevHunt Analyzer"
            : "Automated code analysis and suggestions"}
          {renderCommitInfo()}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {!derivedMetrics && (
          <p className="text-sm text-muted-foreground text-center py-8">
            No analysis data available yet. Sync your repository to trigger analysis.
          </p>
        )}
        {derivedMetrics?.map((metric) => {
          const config = getStatusConfig(metric.status)
          const Icon = config.icon
          const percentage =
            typeof metric.value === "number" && metric.value <= 100 ? metric.value : 100

          return (
            <div
              key={metric.name}
              className={`p-4 rounded-xl border-2 ${config.borderColor} transition-all hover:shadow-md`}
            >
              {/* Metric Header */}
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <Icon className="h-5 w-5" />
                  <span className="font-semibold text-sm">{metric.name}</span>
                </div>
                <Badge className={config.color}>
                  {typeof metric.value === "number" && metric.value > 100
                    ? `${Math.round((metric.value / metric.threshold) * 100)}%`
                    : `${metric.value}${metric.value <= 100 ? "%" : ""}`}
                </Badge>
              </div>

              {/* Progress Bar */}
              {metric.value <= 100 && (
                <div className="mb-3">
                  <div className="h-2 bg-muted rounded-full overflow-hidden">
                    <div
                      className={`h-full transition-all duration-500 ${
                        metric.status === "good"
                          ? "bg-gradient-to-r from-green-500 to-green-600"
                          : metric.status === "warning"
                            ? "bg-gradient-to-r from-yellow-500 to-yellow-600"
                            : "bg-gradient-to-r from-red-500 to-red-600"
                      }`}
                      style={{ width: `${percentage}%` }}
                    />
                  </div>
                </div>
              )}

              {/* Description */}
              <p className="text-sm text-muted-foreground mb-3">{metric.description}</p>

              {/* Suggestions */}
              {metric.suggestions && metric.suggestions.length > 0 && (
                <div className="mt-3 pt-3 border-t space-y-2">
                  <p className="text-xs font-medium text-muted-foreground">Suggestions:</p>
                  <ul className="space-y-1">
                    {metric.suggestions.map((suggestion) => (
                      <li
                        key={`suggestion-${metric.name}-${suggestion}`}
                        className="text-xs text-muted-foreground flex items-start gap-2"
                      >
                        <span className="text-primary mt-0.5">-</span>
                        <span>{suggestion}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )
        })}

        {/* View All Issues button */}
        {issues.length > 0 && (
          <button
            type="button"
            onClick={handleOpenIssues}
            className="w-full flex items-center justify-center gap-2 p-3 rounded-xl border-2 border-dashed border-primary/30 hover:border-primary/60 hover:bg-primary/5 transition-all"
          >
            <ExternalLink className="h-4 w-4 text-primary" />
            <span className="font-semibold text-sm text-primary">View All Issues</span>
            <Badge variant="outline" className="text-xs">
              {issues.length}
            </Badge>
          </button>
        )}

        {/* Full-screen issues dialog */}
        <IssuesDialog
          open={issuesDialogOpen}
          onOpenChange={setIssuesDialogOpen}
          issues={issues}
          dismissedIssues={dismissedIssues}
          repository={analysisResult?.repository}
          branch={analysisResult?.branch}
          onDismiss={handleDismiss}
          onUndismiss={handleUndismiss}
        />
      </CardContent>
    </Card>
  )
}
