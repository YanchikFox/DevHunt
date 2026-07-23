"use client"

import { useState, useCallback, useMemo } from "react"
import { useModerationQueue, useModerationDecision, type ModerationReport } from "@/lib/api/queries/moderation"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import {
  Loader2,
  CheckCircle,
  XCircle,
  Ban,
  ExternalLink,
  User,
  FolderKanban,
  MessageSquare,
  Newspaper,
  MessageCircle,
  Image as ImageIcon,
  ListTodo,
  Activity,
  Clock,
  AlertTriangle,
  Shield,
} from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import Link from "next/link"
import { formatDistanceToNow } from "date-fns"

// ── Target type configuration ────────────────────────────────────────────────

const TARGET_TYPE_CONFIG: Record<string, {
  label: string
  icon: React.ComponentType<{ className?: string }>
  color: string
  buildUrl: (id: string) => string | null
}> = {
  user: {
    label: "User",
    icon: User,
    color: "bg-blue-500/10 text-blue-600 border-blue-200 dark:text-blue-400 dark:border-blue-800",
    buildUrl: (id) => `/dashboard/profile/${id}`,
  },
  project: {
    label: "Project",
    icon: FolderKanban,
    color: "bg-emerald-500/10 text-emerald-600 border-emerald-200 dark:text-emerald-400 dark:border-emerald-800",
    buildUrl: (id) => `/dashboard/projects/${id}`,
  },
  message: {
    label: "Chat Message",
    icon: MessageSquare,
    color: "bg-purple-500/10 text-purple-600 border-purple-200 dark:text-purple-400 dark:border-purple-800",
    buildUrl: () => null,
  },
  news_post: {
    label: "News Post",
    icon: Newspaper,
    color: "bg-amber-500/10 text-amber-600 border-amber-200 dark:text-amber-400 dark:border-amber-800",
    buildUrl: () => null,
  },
  news_comment: {
    label: "News Comment",
    icon: MessageCircle,
    color: "bg-orange-500/10 text-orange-600 border-orange-200 dark:text-orange-400 dark:border-orange-800",
    buildUrl: () => null,
  },
  showcase_comment: {
    label: "Showcase Comment",
    icon: MessageCircle,
    color: "bg-pink-500/10 text-pink-600 border-pink-200 dark:text-pink-400 dark:border-pink-800",
    buildUrl: () => null,
  },
  community_post: {
    label: "Community Post",
    icon: Activity,
    color: "bg-indigo-500/10 text-indigo-600 border-indigo-200 dark:text-indigo-400 dark:border-indigo-800",
    buildUrl: () => null,
  },
  task: {
    label: "Task",
    icon: ListTodo,
    color: "bg-cyan-500/10 text-cyan-600 border-cyan-200 dark:text-cyan-400 dark:border-cyan-800",
    buildUrl: () => null,
  },
  image: {
    label: "Image",
    icon: ImageIcon,
    color: "bg-rose-500/10 text-rose-600 border-rose-200 dark:text-rose-400 dark:border-rose-800",
    buildUrl: () => null,
  },
}

const REASON_LABELS: Record<string, string> = {
  spam: "Spam or advertising",
  harassment: "Harassment or bullying",
  inappropriate_content: "Inappropriate or offensive content",
  misinformation: "Misinformation or false claims",
  other: "Other",
}

function getReasonLabel(reason: string): string {
  return REASON_LABELS[reason] ?? reason
}

// ── ReportCard sub-component ─────────────────────────────────────────────────

interface ReportCardProps {
  readonly report: ModerationReport
  readonly onAction: (reportId: string, type: "approve" | "decline" | "ban") => void
  readonly t: (key: string) => string
}

function ReportCard({ report, onAction, t }: ReportCardProps) {
  const config = TARGET_TYPE_CONFIG[report.targetType] ?? {
    label: report.targetType,
    icon: AlertTriangle,
    color: "bg-gray-500/10 text-gray-600 border-gray-200",
    buildUrl: () => null,
  }
  const IconComponent = config.icon
  const contentUrl = config.buildUrl(report.targetId)

  const handleApprove = useCallback(() => onAction(report.id, "approve"), [onAction, report.id])
  const handleDecline = useCallback(() => onAction(report.id, "decline"), [onAction, report.id])
  const handleBan = useCallback(() => onAction(report.id, "ban"), [onAction, report.id])

  const timeAgo = useMemo(() => {
    try {
      return formatDistanceToNow(new Date(report.createdAt), { addSuffix: true })
    } catch {
      return report.createdAt
    }
  }, [report.createdAt])

  return (
    <Card className="border-border/60 hover:border-border transition-colors">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <Badge variant="outline" className={`gap-1.5 px-2.5 py-1 text-xs font-medium ${config.color}`}>
              <IconComponent className="h-3.5 w-3.5" />
              {config.label}
            </Badge>
            <div className="flex items-center gap-1 text-xs text-muted-foreground">
              <Clock className="h-3 w-3" />
              <span>{timeAgo}</span>
            </div>
          </div>
          {contentUrl ? (
            <Link
              href={contentUrl}
              className="inline-flex items-center gap-1 text-xs text-primary hover:underline"
            >
              View content
              <ExternalLink className="h-3 w-3" />
            </Link>
          ) : (
            <span className="font-mono text-[10px] text-muted-foreground bg-muted px-2 py-1 rounded">
              ID: {report.targetId.slice(0, 12)}...
            </span>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
            {t("reason")}
          </p>
          <p className="text-sm">{getReasonLabel(report.reason)}</p>
        </div>
        <div className="flex items-center gap-2">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
            {t("reporter")}:
          </p>
          <Link
            href={`/dashboard/profile/${report.reporterId}`}
            className="text-sm text-primary hover:underline inline-flex items-center gap-1"
          >
            {report.reporterName ?? `User ${report.reporterId.slice(0, 8)}`}
            <ExternalLink className="h-3 w-3" />
          </Link>
        </div>
        <div className="flex items-center gap-2 pt-2 border-t border-border/40">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5 text-green-600 hover:text-green-700 hover:bg-green-50 dark:hover:bg-green-950/30"
            onClick={handleApprove}
          >
            <CheckCircle className="h-3.5 w-3.5" />
            {t("approve")}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            onClick={handleDecline}
          >
            <XCircle className="h-3.5 w-3.5" />
            {t("decline")}
          </Button>
          <Button
            size="sm"
            variant="destructive"
            className="gap-1.5 ml-auto"
            onClick={handleBan}
          >
            <Ban className="h-3.5 w-3.5" />
            {t("ban")}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

// ── Action descriptions ──────────────────────────────────────────────────────

const DECISION_CONFIG: Record<string, {
  title: string
  description: string
  variant: "default" | "destructive"
}> = {
  approve: {
    title: "Approve Report",
    description: "Confirm that the reported content violates platform rules. The content author may receive a warning.",
    variant: "default",
  },
  decline: {
    title: "Decline Report",
    description: "Dismiss this report. The reported content will remain visible and no action will be taken.",
    variant: "default",
  },
  ban: {
    title: "Ban User",
    description: "Ban the content author from the platform. This is a severe action — use only for repeated or serious violations.",
    variant: "destructive",
  },
}

// ── Main component ───────────────────────────────────────────────────────────

export function ModerationQueue() {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const { data: reports, isLoading } = useModerationQueue()
  const decision = useModerationDecision()

  const [selectedReportId, setSelectedReportId] = useState<string | null>(null)
  const [decisionType, setDecisionType] = useState<"approve" | "decline" | "ban" | null>(null)
  const [actionNote, setActionNote] = useState("")

  const selectedReport = useMemo(() => {
    if (!selectedReportId || !reports) return null
    return reports.find((r) => r.id === selectedReportId) ?? null
  }, [selectedReportId, reports])

  const decisionCfg = decisionType ? DECISION_CONFIG[decisionType] : null

  const handleDecision = useCallback(async () => {
    if (!selectedReportId || !decisionType) return
    try {
      await decision.mutateAsync({
        reportId: selectedReportId,
        decision: decisionType,
        actionTaken: actionNote || undefined,
      })
      toast({ title: t("moderationDecisionSuccess") })
      setSelectedReportId(null)
      setDecisionType(null)
      setActionNote("")
    } catch {
      toast({ title: t("moderationDecisionFailed"), variant: "destructive" })
    }
  }, [selectedReportId, decisionType, actionNote, decision, toast, t])

  const openDecisionDialog = useCallback((reportId: string, type: "approve" | "decline" | "ban") => {
    setSelectedReportId(reportId)
    setDecisionType(type)
    setActionNote("")
  }, [])

  const handleCloseDecisionDialog = useCallback((open: boolean) => {
    if (!open) {
      setSelectedReportId(null)
      setDecisionType(null)
    }
  }, [])

  const handleActionNoteChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setActionNote(e.target.value)
  }, [])

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    )
  }

  if (!reports || reports.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-12 text-center border rounded-lg border-dashed border-border">
        <Shield className="h-10 w-10 text-muted-foreground/40" />
        <p className="text-muted-foreground font-medium">{t("noReportsPending")}</p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Badge variant="secondary" className="gap-1">
          <AlertTriangle className="h-3 w-3" />
          {reports.length}
        </Badge>
        <span>pending report{reports.length !== 1 ? "s" : ""} to review</span>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {reports.map((report) => (
          <ReportCard
            key={report.id}
            report={report}
            onAction={openDecisionDialog}
            t={t}
          />
        ))}
      </div>

      {/* Decision Dialog */}
      <Dialog open={!!selectedReportId} onOpenChange={handleCloseDecisionDialog}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              {decisionType === "ban" && <Ban className="h-5 w-5 text-destructive" />}
              {decisionType === "approve" && <CheckCircle className="h-5 w-5 text-green-600" />}
              {decisionType === "decline" && <XCircle className="h-5 w-5 text-muted-foreground" />}
              {decisionCfg?.title}
            </DialogTitle>
            <DialogDescription>
              {decisionCfg?.description}
            </DialogDescription>
          </DialogHeader>

          {selectedReport && (
            <div className="rounded-md bg-muted/50 p-3 text-sm space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Target:</span>
                <Badge variant="outline" className="text-xs">
                  {TARGET_TYPE_CONFIG[selectedReport.targetType]?.label ?? selectedReport.targetType}
                </Badge>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Reason:</span>
                <span>{getReasonLabel(selectedReport.reason)}</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Reporter:</span>
                <span>{selectedReport.reporterName ?? `User ${selectedReport.reporterId.slice(0, 8)}`}</span>
              </div>
            </div>
          )}

          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">
              {t("actionNotePlaceholder")}
            </label>
            <Textarea
              placeholder="Describe what action was taken and why..."
              value={actionNote}
              onChange={handleActionNoteChange}
              rows={3}
              maxLength={500}
            />
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => handleCloseDecisionDialog(false)}
            >
              Cancel
            </Button>
            <Button
              variant={decisionCfg?.variant ?? "default"}
              onClick={handleDecision}
              disabled={decision.isPending}
            >
              {decision.isPending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : null}
              {t("confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
