"use client"

import { useTranslations } from "next-intl"
import { useState, useMemo, useCallback } from "react"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { ActivityCard } from "@/components/profile/ActivityCard"
import { ActivityItemCompact } from "@/components/profile/ActivityItemCompact"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import { Search, LayoutList, LayoutGrid, X } from "lucide-react"
import { cn } from "@/lib/utils"

interface ActivityGroup {
  key: string
  label: string
  items: UserActivityFeedItem[]
}

type ActivityFilter = "all" | "tasks" | "project" | "columns" | "team"

interface ActivityTabContentProps {
  readonly activities: UserActivityFeedItem[]
  readonly groupedActivities: ActivityGroup[]
  readonly isFetching: boolean
  readonly hasNextPage: boolean
  readonly isFetchingNextPage: boolean
  readonly onLoadMore: () => void
}

const filterConfig: Record<ActivityFilter, { label: string; eventPrefixes: string[] }> = {
  all: { label: "activity.filterAll", eventPrefixes: [] },
  tasks: { label: "activity.filterTasks", eventPrefixes: ["task."] },
  project: { label: "activity.filterProject", eventPrefixes: ["project."] },
  columns: { label: "activity.filterColumns", eventPrefixes: ["column."] },
  team: { label: "activity.filterTeam", eventPrefixes: ["user."] },
}

interface FilterButtonProps {
  filterKey: ActivityFilter
  isActive: boolean
  count: number
  label: string
  onSetFilter: (key: ActivityFilter) => void
}

function FilterButton({ filterKey, isActive, count, label, onSetFilter }: FilterButtonProps) {
  const handleClick = useCallback(() => onSetFilter(filterKey), [onSetFilter, filterKey])
  return (
    <Button
      variant={isActive ? "secondary" : "ghost"}
      size="sm"
      className={cn("h-7 px-2.5 text-xs gap-1.5", !isActive && "text-muted-foreground")}
      onClick={handleClick}
    >
      {label}
      <Badge
        variant={isActive ? "default" : "secondary"}
        className={cn("h-4 px-1 text-[10px] min-w-[18px] justify-center", isActive ? "bg-primary/20" : "bg-muted")}
      >
        {count}
      </Badge>
    </Button>
  )
}

function getEventGroup(eventType: string): ActivityFilter {
  if (eventType.startsWith("task.")) return "tasks"
  if (eventType.startsWith("column.")) return "columns"
  if (eventType.startsWith("project.")) return "project"
  if (eventType.startsWith("user.")) return "team"
  return "all"
}

export function ActivityTabContent({
  activities,
  groupedActivities: _groupedActivities,
  isFetching,
  hasNextPage,
  isFetchingNextPage,
  onLoadMore,
}: ActivityTabContentProps) {
  const t = useTranslations()
  const [viewMode, setViewMode] = useState<"compact" | "expanded">("compact")
  const [filter, setFilter] = useState<ActivityFilter>("all")
  const [searchQuery, setSearchQuery] = useState("")

  const handleSetViewModeCompact = useCallback(() => setViewMode("compact"), [])
  const handleSetViewModeExpanded = useCallback(() => setViewMode("expanded"), [])
  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value), [])
  const handleClearSearch = useCallback(() => setSearchQuery(""), [])
  const handleSetFilter = useCallback((key: ActivityFilter) => setFilter(key), [])

  // Count activities by type
  const filterCounts = useMemo(() => {
    const counts: Record<ActivityFilter, number> = {
      all: activities.length,
      tasks: 0,
      project: 0,
      columns: 0,
      team: 0,
    }
    activities.forEach((activity) => {
      const group = getEventGroup(activity.eventType)
      if (group !== "all") counts[group]++
    })
    return counts
  }, [activities])

  // Filter and search activities
  const filteredActivities = useMemo(() => {
    let result = activities

    // Apply type filter
    if (filter !== "all") {
      const prefixes = filterConfig[filter].eventPrefixes
      result = result.filter((activity) =>
        prefixes.some((prefix) => activity.eventType.startsWith(prefix))
      )
    }

    // Apply search
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase()
      result = result.filter((activity) => {
        const summary = activity.summary?.toLowerCase() || ""
        const actorName = activity.actorName?.toLowerCase() || ""
        const projectTitle = activity.projectTitle?.toLowerCase() || ""
        const eventType = activity.eventType.toLowerCase()
        return (
          summary.includes(query) ||
          actorName.includes(query) ||
          projectTitle.includes(query) ||
          eventType.includes(query)
        )
      })
    }

    return result
  }, [activities, filter, searchQuery])

  // Group filtered activities by date
  const filteredGroups = useMemo(() => {
    const groups: ActivityGroup[] = []
    const groupMap = new Map<string, UserActivityFeedItem[]>()

    filteredActivities.forEach((activity) => {
      const raw = activity.createdAt
      if (!raw) return
      const date = new Date(raw)
      const today = new Date()
      const yesterday = new Date(today)
      yesterday.setDate(yesterday.getDate() - 1)

      let key: string

      if (date.toDateString() === today.toDateString()) {
        key = "today"
      } else if (date.toDateString() === yesterday.toDateString()) {
        key = "yesterday"
      } else {
        key = date.toISOString().split("T")[0]
      }

      const existing = groupMap.get(key) || []
      existing.push(activity)
      groupMap.set(key, existing)
    })

    // Convert to array and sort
    const sortedKeys = Array.from(groupMap.keys()).sort((a, b) => {
      if (a === "today") return -1
      if (b === "today") return 1
      if (a === "yesterday") return -1
      if (b === "yesterday") return 1
      return b.localeCompare(a)
    })

    sortedKeys.forEach((key) => {
      const items = groupMap.get(key)
      if (items && items.length > 0) {
        let label = ""
        if (key === "today") {
          label = t("activity.today")
        } else if (key === "yesterday") {
          label = t("activity.yesterday")
        } else {
          label = new Date(key).toLocaleDateString()
        }
        groups.push({ key, label, items })
      }
    })

    return groups
  }, [filteredActivities, t])

  return (
    <Card className="border border-border/70 bg-card/80 shadow-sm">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-sm font-semibold uppercase">{t("projects.projectActivity")}</CardTitle>
            <CardDescription>{t("projects.recentActionsNewsAndUpdates")}</CardDescription>
          </div>
          {/* View toggle */}
          <div className="flex items-center gap-1 bg-muted/50 rounded-lg p-0.5">
            <Button
              variant={viewMode === "compact" ? "secondary" : "ghost"}
              size="sm"
              className="h-7 px-2"
              onClick={handleSetViewModeCompact}
              title={t("activity.compactView")}
            >
              <LayoutList className="h-4 w-4" />
            </Button>
            <Button
              variant={viewMode === "expanded" ? "secondary" : "ghost"}
              size="sm"
              className="h-7 px-2"
              onClick={handleSetViewModeExpanded}
              title={t("activity.expandedView")}
            >
              <LayoutGrid className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* Search and filters */}
        <div className="flex flex-col gap-3 pt-3">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder={t("activity.searchPlaceholder")}
              value={searchQuery}
              onChange={handleSearchChange}
              className="pl-8 h-8 text-sm"
            />
            {searchQuery && (
              <Button
                variant="ghost"
                size="sm"
                className="absolute right-1 top-1/2 -translate-y-1/2 h-6 w-6 p-0"
                onClick={handleClearSearch}
              >
                <X className="h-3 w-3" />
              </Button>
            )}
          </div>

          {/* Filter tabs */}
          <div className="flex flex-wrap gap-1.5">
            {(Object.keys(filterConfig) as ActivityFilter[]).map((filterKey) => (
              <FilterButton
                key={filterKey}
                filterKey={filterKey}
                isActive={filter === filterKey}
                count={filterCounts[filterKey]}
                label={t(filterConfig[filterKey].label)}
                onSetFilter={handleSetFilter}
              />
            ))}
          </div>
        </div>
      </CardHeader>

      <CardContent className="pt-0">
        {/* Results info */}
        {(filter !== "all" || searchQuery) && (
          <div className="text-xs text-muted-foreground mb-3">
            {t("activity.showingResults", { count: filteredActivities.length, total: activities.length })}
          </div>
        )}

        {/* Activity list */}
        {filteredActivities.length === 0 && !isFetching ? (
          <p className="text-sm text-muted-foreground py-8 text-center">
            {searchQuery ? t("activity.noSearchResults") : t("projects.noActivityYet")}
          </p>
        ) : viewMode === "compact" ? (
          /* Compact view */
          <div className="space-y-4">
            {filteredGroups.map((group) => (
              <div key={group.key}>
                <p className="text-xs font-medium text-muted-foreground mb-1 px-2">{group.label}</p>
                <div className="border rounded-lg divide-y divide-border/50">
                  {group.items.map((activity) => (
                    <ActivityItemCompact
                      key={activity.id}
                      activity={activity}
                      showAvatar={true}
                      showProject={false}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
        ) : (
          /* Expanded view */
          <div className="space-y-4">
            {filteredGroups.map((group) => (
              <div key={group.key} className="space-y-3">
                <p className="text-xs font-medium text-muted-foreground">{group.label}</p>
                <div className="space-y-3">
                  {group.items.map((activity) => (
                    <ActivityCard key={activity.id} activity={activity} context="project" />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Load more */}
        {hasNextPage && (
          <div className="flex justify-center pt-4">
            <Button
              variant="outline"
              size="sm"
              onClick={onLoadMore}
              disabled={isFetchingNextPage}
            >
              {isFetchingNextPage ? t("common.loading") : t("common.showMore")}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
