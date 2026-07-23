"use client"

import { useState, useCallback } from "react"
import { useMyTickets } from "@/lib/api/queries/support"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Badge } from "@/components/ui/badge"
import { Loader2, Plus, MessageSquare, HelpCircle, Bug, Lightbulb, CreditCard, Box, LifeBuoy } from "lucide-react";
import { useTranslations } from "next-intl"
import { Link } from "@/i18n/routing"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils"

const statusColors: Record<string, string> = {
  open: "bg-blue-100 text-blue-700 border-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:border-blue-800",
  in_progress: "bg-yellow-100 text-yellow-700 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:border-yellow-800",
  waiting_user: "bg-purple-100 text-purple-700 border-purple-200 dark:bg-purple-900/30 dark:text-purple-400 dark:border-purple-800",
  resolved: "bg-green-100 text-green-700 border-green-200 dark:bg-green-900/30 dark:text-green-400 dark:border-green-800",
  closed: "bg-gray-100 text-gray-700 border-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700",
}

const priorityColors: Record<string, string> = {
  low: "bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-400 dark:border-slate-700",
  medium: "bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-900/20 dark:text-blue-400 dark:border-blue-900",
  high: "bg-orange-50 text-orange-700 border-orange-200 dark:bg-orange-900/20 dark:text-orange-400 dark:border-orange-900",
  urgent: "bg-red-50 text-red-700 border-red-200 dark:bg-red-900/20 dark:text-red-400 dark:border-red-900",
}

const categoryIcons: Record<string, React.ReactNode> = {
  question: <HelpCircle className="h-3.5 w-3.5" />,
  bug: <Bug className="h-3.5 w-3.5" />,
  feature: <Lightbulb className="h-3.5 w-3.5" />,
  billing: <CreditCard className="h-3.5 w-3.5" />,
  other: <Box className="h-3.5 w-3.5" />,
}

export default function SupportPage() {
  const t = useTranslations("support")
  const tCommon = useTranslations("common")
  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined)
  const [categoryFilter, setCategoryFilter] = useState<string | undefined>(undefined)

  const { data, isLoading } = useMyTickets(statusFilter, categoryFilter, page)

  const handleStatusFilterChange = useCallback((v: string) => { setStatusFilter(v === "all" ? undefined : v); setPage(1) }, [])
  const handleCategoryFilterChange = useCallback((v: string) => { setCategoryFilter(v === "all" ? undefined : v); setPage(1) }, [])
  const handlePrevPage = useCallback(() => setPage((p) => p - 1), [])
  const handleNextPage = useCallback(() => setPage((p) => p + 1), [])

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-20">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-1">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-pink-500 to-rose-600 shadow-lg shadow-pink-500/20">
              <LifeBuoy className="h-5 w-5 text-white" />
            </div>
            <h1 className="text-3xl font-bold tracking-tight text-foreground">
              {t("title")}
            </h1>
          </div>
          <p className="text-muted-foreground pl-14 max-w-2xl">{t("description")}</p>
        </div>
        <Link href="/dashboard/support/new">
          <Button className="gap-2 bg-primary text-primary-foreground hover:bg-primary/90 shadow-md border-0 transition hover:shadow-lg">
            <Plus className="h-4 w-4" /> {t("createTicket")}
          </Button>
        </Link>
      </div>

      <Card className="border border-border/50 bg-card shadow-sm">
        <CardHeader className="pb-4 border-b border-border/40 bg-muted/20">
          <div className="flex flex-col sm:flex-row gap-4 justify-between items-start sm:items-center">
            <CardTitle className="text-lg font-medium">{t("yourTickets")}</CardTitle>
            <div className="flex gap-3 w-full sm:w-auto">
              <Select
                value={statusFilter ?? "all"}
                onValueChange={handleStatusFilterChange}
              >
                <SelectTrigger className="w-[160px] bg-background/50 border-border/60">
                  <SelectValue placeholder={t("filterByStatus")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("allStatuses")}</SelectItem>
                  <SelectItem value="open">{t("statusOpen")}</SelectItem>
                  <SelectItem value="in_progress">{t("statusInProgress")}</SelectItem>
                  <SelectItem value="waiting_user">{t("statusWaitingUser")}</SelectItem>
                  <SelectItem value="resolved">{t("statusResolved")}</SelectItem>
                  <SelectItem value="closed">{t("statusClosed")}</SelectItem>
                </SelectContent>
              </Select>

              <Select
                value={categoryFilter ?? "all"}
                onValueChange={handleCategoryFilterChange}
              >
                <SelectTrigger className="w-[160px] bg-background/50 border-border/60">
                  <SelectValue placeholder={t("filterByCategory")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("allCategories")}</SelectItem>
                  <SelectItem value="question">{t("categoryQuestion")}</SelectItem>
                  <SelectItem value="bug">{t("categoryBug")}</SelectItem>
                  <SelectItem value="feature">{t("categoryFeature")}</SelectItem>
                  <SelectItem value="billing">{t("categoryBilling")}</SelectItem>
                  <SelectItem value="other">{t("categoryOther")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {(!data?.data || data.data.length === 0) ? (
            <div className="flex flex-col items-center justify-center py-16 text-center text-muted-foreground">
              <div className="h-16 w-16 rounded-full bg-muted/40 flex items-center justify-center mb-4">
                <MessageSquare className="h-8 w-8 opacity-50" />
              </div>
              <p className="font-medium text-lg">{t("noTickets")}</p>
              <p className="text-sm mt-1 max-w-sm mx-auto">{t("noTicketsDescription")}</p>
            </div>
          ) : (
            <div className="divide-y divide-border/40">
              {data.data.map((ticket) => (
                <Link
                  key={ticket.id}
                  href={`/dashboard/support/${ticket.id}`}
                  className="block p-4 hover:bg-accent/30 transition group"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1.5 flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <h3 className="font-semibold text-foreground group-hover:text-primary transition-colors truncate">
                          {ticket.subject}
                        </h3>
                        <Badge variant="outline" className={cn("text-[10px] px-1.5 py-0 h-5 font-normal", statusColors[ticket.status])}>
                          {ticket.status}
                        </Badge>
                      </div>
                      <div className="flex flex-wrap gap-2 text-xs text-muted-foreground items-center">
                        <Badge variant="secondary" className="gap-1 font-normal bg-muted/50">
                          {categoryIcons[ticket.category]}
                          <span className="capitalize">{ticket.category}</span>
                        </Badge>
                        <span>•</span>
                        <span>ID: #{ticket.id.slice(0, 8)}</span>
                        <span>•</span>
                        <span className={priorityColors[ticket.priority] ? "font-medium " + priorityColors[ticket.priority].split(" ")[1] : ""}>
                          {ticket.priority.toUpperCase()} Priority
                        </span>
                      </div>
                    </div>
                    <div className="text-right text-xs text-muted-foreground shrink-0">
                      <div>{new Date(ticket.createdAt).toLocaleDateString()}</div>
                      <div className="flex items-center gap-1 justify-end mt-1.5">
                        <MessageSquare className="h-3 w-3" />
                        <span>{ticket.messageCount}</span>
                      </div>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Pagination */}
      {data && data.data && data.data.length > 0 && (
        <div className="flex justify-center gap-2">
          <Button variant="outline" size="sm" disabled={page === 1} onClick={handlePrevPage}>
            {tCommon("previous")}
          </Button>
          <Button variant="outline" size="sm" disabled={data.data.length < 20} onClick={handleNextPage}>
            {tCommon("next")}
          </Button>
        </div>
      )}
    </div>
  )
}
