"use client"

import { useState, useCallback, useMemo } from "react"
import {
  useProjectIssues,
  useCreateProjectIssue,
  useCancelProjectIssue,
  ISSUE_TYPES,
  type IssueType,
} from "@/lib/api/queries/project-issues"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import {
  Loader2,
  Plus,
  AlertTriangle,
  Clock,
  CheckCircle,
  XCircle,
  ArrowUpCircle,
  Search,
  ShieldAlert,
} from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { formatDistanceToNow } from "date-fns"

// ── Status configuration ─────────────────────────────────────────────────────

const STATUS_CONFIG: Record<string, {
  label: string
  icon: React.ComponentType<{ className?: string }>
  color: string
}> = {
  open: {
    label: "Open",
    icon: AlertTriangle,
    color: "bg-blue-500/10 text-blue-600 border-blue-200 dark:text-blue-400",
  },
  investigating: {
    label: "Investigating",
    icon: Search,
    color: "bg-yellow-500/10 text-yellow-600 border-yellow-200 dark:text-yellow-400",
  },
  resolved: {
    label: "Resolved",
    icon: CheckCircle,
    color: "bg-green-500/10 text-green-600 border-green-200 dark:text-green-400",
  },
  dismissed: {
    label: "Dismissed",
    icon: XCircle,
    color: "bg-gray-500/10 text-gray-600 border-gray-200 dark:text-gray-400",
  },
  escalated: {
    label: "Escalated",
    icon: ArrowUpCircle,
    color: "bg-red-500/10 text-red-600 border-red-200 dark:text-red-400",
  },
}

const TYPE_LABELS: Record<string, string> = {
  technical: "Technical Issue",
  legal: "Legal Concern",
  conflict: "Team Conflict",
  abuse: "Abuse Report",
  violation: "Rule Violation",
  security: "Security Issue",
  other: "Other",
}

const PRIORITY_COLORS: Record<string, string> = {
  low: "bg-gray-500/10 text-gray-600 border-gray-200",
  medium: "bg-blue-500/10 text-blue-600 border-blue-200",
  high: "bg-orange-500/10 text-orange-600 border-orange-200",
  urgent: "bg-red-500/10 text-red-600 border-red-200",
}

// ── Issue Card ───────────────────────────────────────────────────────────────

interface IssueCardProps {
  readonly issue: {
    id: string
    title: string
    description: string
    type: string
    status: string
    priority: string
    createdAt: string
    reporterName?: string
    resolution?: string
  }
  readonly onCancel?: (issueId: string) => void
  readonly canCancel: boolean
}

function IssueCard({ issue, onCancel, canCancel }: IssueCardProps) {
  const statusCfg = STATUS_CONFIG[issue.status] ?? STATUS_CONFIG.open
  const StatusIcon = statusCfg.icon

  const timeAgo = useMemo(() => {
    try {
      return formatDistanceToNow(new Date(issue.createdAt), { addSuffix: true })
    } catch {
      return issue.createdAt
    }
  }, [issue.createdAt])

  const handleCancel = useCallback(() => onCancel?.(issue.id), [onCancel, issue.id])

  return (
    <Card className="border-border/60 hover:border-border transition-colors">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-1 min-w-0">
            <h3 className="font-semibold text-sm leading-tight">{issue.title}</h3>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" className={`text-[10px] px-1.5 py-0 ${statusCfg.color}`}>
                <StatusIcon className="h-3 w-3 mr-1" />
                {statusCfg.label}
              </Badge>
              <Badge variant="outline" className="text-[10px] px-1.5 py-0">
                {TYPE_LABELS[issue.type] ?? issue.type}
              </Badge>
              {issue.priority && (
                <Badge variant="outline" className={`text-[10px] px-1.5 py-0 ${PRIORITY_COLORS[issue.priority] ?? ""}`}>
                  {issue.priority}
                </Badge>
              )}
            </div>
          </div>
          <div className="flex items-center gap-1 text-[10px] text-muted-foreground shrink-0">
            <Clock className="h-3 w-3" />
            {timeAgo}
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-2">
        <p className="text-xs text-muted-foreground line-clamp-3">{issue.description}</p>
        {issue.reporterName && (
          <p className="text-[10px] text-muted-foreground">
            Reported by <span className="font-medium text-foreground">{issue.reporterName}</span>
          </p>
        )}
        {issue.resolution && (
          <div className="rounded-md bg-muted/50 p-2 text-xs">
            <span className="font-medium">Resolution:</span> {issue.resolution}
          </div>
        )}
        {canCancel && issue.status === "open" && (
          <div className="flex justify-end pt-1">
            <Button variant="ghost" size="sm" className="h-7 text-xs text-destructive hover:text-destructive" onClick={handleCancel}>
              <XCircle className="h-3 w-3 mr-1" />
              Cancel Issue
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

// ── Main component ───────────────────────────────────────────────────────────

function CreateIssueDialog({ open, onOpenChange, issueType, onTypeChange, title, onTitleChange, description, onDescriptionChange, onSubmit, isPending }: {
  open: boolean; onOpenChange: (open: boolean) => void
  issueType: IssueType; onTypeChange: (v: string) => void
  title: string; onTitleChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  description: string; onDescriptionChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onSubmit: () => void; isPending: boolean
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Report an Issue</DialogTitle>
          <DialogDescription>Describe the problem you&apos;ve encountered in this project. The project team and admins will be notified.</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <label className="text-sm font-medium">Type</label>
            <Select value={issueType} onValueChange={onTypeChange}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ISSUE_TYPES.map((t) => (<SelectItem key={t} value={t}>{TYPE_LABELS[t] ?? t}</SelectItem>))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Title</label>
            <Input placeholder="Brief summary of the issue..." value={title} onChange={onTitleChange} maxLength={200} />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Description</label>
            <Textarea placeholder="Provide details about what happened, steps to reproduce, etc..." value={description} onChange={onDescriptionChange} rows={5} maxLength={4000} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={onSubmit} disabled={isPending || !title.trim() || !description.trim()}>
            {isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
            Submit Issue
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface ProjectIssuesTabProps {
  readonly projectId: string
  readonly currentUserId?: string
}

export function ProjectIssuesTab({ projectId, currentUserId }: ProjectIssuesTabProps) {
  const { toast } = useToast()
  const { data: issues, isLoading } = useProjectIssues(projectId)
  const createIssue = useCreateProjectIssue(projectId)
  const cancelIssue = useCancelProjectIssue(projectId)

  const [createDialogOpen, setCreateDialogOpen] = useState(false)
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [issueType, setIssueType] = useState<IssueType>("technical")
  const [statusFilter, setStatusFilter] = useState<string>("all")

  const handleOpenCreate = useCallback(() => {
    setTitle("")
    setDescription("")
    setIssueType("technical")
    setCreateDialogOpen(true)
  }, [])

  const handleCloseCreate = useCallback((open: boolean) => {
    if (!open) setCreateDialogOpen(false)
  }, [])

  const handleTitleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setTitle(e.target.value), [])
  const handleDescriptionChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setDescription(e.target.value), [])
  const handleTypeChange = useCallback((v: string) => setIssueType(v as IssueType), [])
  const handleStatusFilterChange = useCallback((v: string) => setStatusFilter(v), [])

  const handleSubmit = useCallback(async () => {
    if (!title.trim() || !description.trim()) return
    try {
      await createIssue.mutateAsync({
        type: issueType,
        title: title.trim(),
        description: description.trim(),
      })
      toast({ title: "Issue created successfully" })
      setCreateDialogOpen(false)
    } catch {
      toast({ title: "Failed to create issue", variant: "destructive" })
    }
  }, [title, description, issueType, createIssue, toast])

  const handleCancel = useCallback(async (issueId: string) => {
    try {
      await cancelIssue.mutateAsync({ issueId })
      toast({ title: "Issue cancelled" })
    } catch {
      toast({ title: "Failed to cancel issue", variant: "destructive" })
    }
  }, [cancelIssue, toast])

  const filteredIssues = useMemo(() => {
    if (!issues) return []
    if (statusFilter === "all") return issues
    return issues.filter((i) => i.status === statusFilter)
  }, [issues, statusFilter])

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Project Issues</h2>
          <p className="text-muted-foreground text-sm">
            Report and track problems within this project
          </p>
        </div>
        <Button onClick={handleOpenCreate} className="gap-2">
          <Plus className="h-4 w-4" />
          Report Issue
        </Button>
      </div>

      {/* Filter */}
      <div className="flex items-center gap-3">
        <Select value={statusFilter} onValueChange={handleStatusFilterChange}>
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="Filter by status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {Object.entries(STATUS_CONFIG).map(([key, cfg]) => (
              <SelectItem key={key} value={key}>{cfg.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {issues && (
          <Badge variant="secondary" className="text-xs">
            {filteredIssues.length} issue{filteredIssues.length !== 1 ? "s" : ""}
          </Badge>
        )}
      </div>

      {/* Issues list */}
      {filteredIssues.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 p-12 text-center border rounded-lg border-dashed border-border">
          <ShieldAlert className="h-10 w-10 text-muted-foreground/40" />
          <p className="text-muted-foreground font-medium">
            {statusFilter === "all" ? "No issues reported yet" : `No ${STATUS_CONFIG[statusFilter]?.label?.toLowerCase() ?? statusFilter} issues`}
          </p>
          <p className="text-xs text-muted-foreground max-w-sm">
            Use &quot;Report Issue&quot; to flag technical problems, conflicts, or policy violations within this project.
          </p>
        </div>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {filteredIssues.map((issue) => (
            <IssueCard
              key={issue.id}
              issue={issue}
              onCancel={handleCancel}
              canCancel={issue.reporterId === currentUserId}
            />
          ))}
        </div>
      )}

      <CreateIssueDialog
        open={createDialogOpen}
        onOpenChange={handleCloseCreate}
        issueType={issueType}
        onTypeChange={handleTypeChange}
        title={title}
        onTitleChange={handleTitleChange}
        description={description}
        onDescriptionChange={handleDescriptionChange}
        onSubmit={handleSubmit}
        isPending={createIssue.isPending}
      />
    </div>
  )
}
