"use client"

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { UserStatsDto } from "@/lib/api/queries/profile"
import { useTranslations } from "next-intl"
import { Activity, BarChart, Zap } from "lucide-react"

export type UserStatsPanelProps = {
  stats?: UserStatsDto
  isVisible: boolean
}

export function UserStatsPanel({ stats, isVisible }: UserStatsPanelProps) {
  const t = useTranslations("stats")

  if (!isVisible || !stats) {
    return null
  }

  const advancedMetrics = [
    { 
      label: t("contribution"), 
      value: stats.contributionScore, 
      icon: <Zap className="h-4 w-4 text-yellow-500" />,
      color: "bg-yellow-500"
    },
    { 
      label: t("community"), 
      value: stats.communityScore,
      icon: <Activity className="h-4 w-4 text-blue-500" />,
      color: "bg-blue-500"
    },
    {
      label: t("consistency14d"),
      value: Math.min(100, (stats.consistencyDaysActiveLast14 / 14) * 100),
      meta: `${stats.consistencyDaysActiveLast14} / 14 ${t("days")}`,
      icon: <BarChart className="h-4 w-4 text-green-500" />,
      color: "bg-green-500"
    },
  ]

  return (
    <Card className="border-border/50 bg-card/40 backdrop-blur-sm shadow-sm h-full">
      <CardHeader className="pb-3 border-b border-border/40">
        <CardTitle className="text-lg font-semibold flex items-center gap-2">
          <Activity className="h-5 w-5 text-primary" />
          {t("userStats")}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-6 space-y-6">
        {advancedMetrics.map((item) => (
          <div key={item.label} className="space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="flex items-center gap-2 text-muted-foreground font-medium">
                {item.icon}
                {item.label}
              </span>
              <span className="font-bold text-foreground">
                {item.meta ?? `${Math.round(item.value ?? 0)}%`}
              </span>
            </div>
            <div className="h-2.5 rounded-full bg-muted/50 overflow-hidden">
              <div
                className={`h-full rounded-full ${item.color} shadow-sm transition-all duration-1000 ease-out`}
                style={{ width: `${Math.min(100, Math.max(0, item.value ?? 0))}%` }}
              />
            </div>
          </div>
        ))}

        <div className="pt-4 border-t border-border/40">
           <div className="flex items-center justify-between text-xs text-muted-foreground">
             <span>{t("activity30d")}</span>
             <span className="font-medium text-foreground">{stats.recentActivityCount} events</span>
           </div>
        </div>
      </CardContent>
    </Card>
  )
}
