"use client"

import { useCallback, useMemo } from "react"
import { useInfiniteQuery } from "@tanstack/react-query"
import { apiClient } from "@/lib/api/client"
import type { ProjectActivityPage, TranslateFn } from "./types"

const filterActivityItems = (items: ProjectActivityPage["data"] | undefined) =>
  (items ?? []).filter((activity) => activity.eventType !== "project.media_uploaded")

const groupActivitiesByDate = (
  activities: ProjectActivityPage["data"] | undefined,
  t: TranslateFn
) => {
  const items = Array.isArray(activities) ? activities : []
  const groups = new Map<string, typeof items>()

  const pad2 = (value: number) => String(value).padStart(2, "0")
  const toLocalDateKey = (date: Date) =>
    `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`

  const now = new Date()
  const todayKey = toLocalDateKey(now)
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  const yesterdayKey = toLocalDateKey(yesterday)

  for (const activity of items) {
    const createdAt = activity?.createdAt
    const date = typeof createdAt === "string" ? new Date(createdAt) : null
    const key = date && !Number.isNaN(date.getTime())
      ? toLocalDateKey(date)
      : "unknown"

    const bucket = groups.get(key)
    if (bucket) bucket.push(activity)
    else groups.set(key, [activity])
  }

  const sortedKeys = Array.from(groups.keys()).sort((a, b) => {
    if (a === "unknown") return 1
    if (b === "unknown") return -1
    return b.localeCompare(a)
  })

  return sortedKeys.map((key) => {
    const label = key === "unknown"
      ? t("projects.noDate")
      : key === todayKey
        ? t("projects.today")
        : key === yesterdayKey
          ? t("projects.yesterday")
          : (() => {
            const [y, m, d] = key.split("-").map((part) => Number(part))
            const localMidnight = new Date(y, (m ?? 1) - 1, d ?? 1)
            return localMidnight.toLocaleDateString(
              // Use the locale from i18n (e.g. "pl" → "pl-PL")
              `${t("common.locale")}`,
              { day: "2-digit", month: "long", year: "numeric" }
            )
          })()

    return { key, label, items: groups.get(key) ?? [] }
  })
}

export function useProjectDetailActivity(
  projectId: string,
  hasProjectId: boolean,
  t: TranslateFn
) {
  const projectActivity = useInfiniteQuery<ProjectActivityPage>({
    queryKey: ["projectActivity", projectId],
    queryFn: async ({ pageParam = 1 }) => {
      const response = await apiClient.get<ProjectActivityPage>("/activities", {
        params: { projectId, page: pageParam, pageSize: 8 },
      })
      return response.data
    },
    enabled: hasProjectId,
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage?.pagination?.hasNext ? (lastPage.pagination.page ?? 1) + 1 : undefined,
  })

  const projectActivities = useMemo(
    () => filterActivityItems(projectActivity.data?.pages?.flatMap((page) => page?.data ?? [])),
    [projectActivity.data]
  )

  const groupedProjectActivities = useMemo(
    () => groupActivitiesByDate(projectActivities, t),
    [projectActivities, t]
  )

  const handleLoadMoreActivity = useCallback(() => {
    projectActivity.fetchNextPage()
  }, [projectActivity])

  return {
    projectActivity,
    projectActivities,
    groupedProjectActivities,
    handleLoadMoreActivity,
  }
}
