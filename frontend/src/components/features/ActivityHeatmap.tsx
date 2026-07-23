"use client"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Calendar } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Props for the ActivityHeatmap component.
 */
export interface ActivityHeatmapProps {
  /** Array of daily activity data points */
  data?: Array<{ date: string; count: number }>
}

// Generate mock data for the last 52 weeks
const generateMockData = () => {
  const data = []
  const today = new Date()

  for (let i = 0; i < 365; i++) {
    const date = new Date(today)
    date.setDate(date.getDate() - i)

    // Generate random activity with higher probability for recent days
    const daysAgo = i
    const baseCount = daysAgo < 30 ? 10 : daysAgo < 90 ? 5 : 2
    const count = Math.floor(Math.random() * baseCount)

    data.push({
      date: date.toISOString().split("T")[0],
      count,
    })
  }

  return data.reverse()
}

/**
 * Visualizes user activity over the last year as a heatmap (similar to GitHub's contribution graph).
 *
 * @example
 * ```tsx
 * <ActivityHeatmap
 *   data={[
 *     { date: "2023-01-01", count: 5 },
 *     { date: "2023-01-02", count: 12 }
 *   ]}
 * />
 * ```
 */
export function ActivityHeatmap({ data = generateMockData() }: ActivityHeatmapProps) {
  // Group data by weeks
  const weeks: Array<Array<{ date: string; count: number }>> = []
  for (let i = 0; i < data.length; i += 7) {
    weeks.push(data.slice(i, i + 7))
  }

  const getIntensityColor = (count: number) => {
    if (count === 0) return "bg-muted"
    if (count < 2) return "bg-green-100 dark:bg-green-900/20"
    if (count < 5) return "bg-green-300 dark:bg-green-700/40"
    if (count < 10) return "bg-green-500 dark:bg-green-600"
    return "bg-green-700 dark:bg-green-500"
  }

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Calendar className="h-5 w-5 text-primary" />
          Development Activity
        </CardTitle>
        <CardDescription>One year of coding activity visualized</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {/* Heatmap */}
          <div className="flex gap-1 overflow-x-auto pb-4">
            {weeks.map((week) => (
              <div key={week[0]?.date ?? week[week.length - 1]?.date} className="flex flex-col gap-1">
                {week.map((day) => (
                  <div
                    key={day.date}
                    className={cn(
                      "w-3 h-3 rounded-sm transition-all hover:scale-125 cursor-pointer hover:ring-2 hover:ring-primary/50",
                      getIntensityColor(day.count)
                    )}
                    title={`${day.date}: ${day.count} contributions`}
                  />
                ))}
              </div>
            ))}
          </div>

          {/* Legend and Stats */}
          <div className="flex items-center justify-between pt-4 border-t">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span>Less</span>
              <div className="flex gap-0.5">
                <div className="w-3 h-3 rounded-sm bg-muted" />
                <div className="w-3 h-3 rounded-sm bg-green-100 dark:bg-green-900/20" />
                <div className="w-3 h-3 rounded-sm bg-green-300 dark:bg-green-700/40" />
                <div className="w-3 h-3 rounded-sm bg-green-500 dark:bg-green-600" />
                <div className="w-3 h-3 rounded-sm bg-green-700 dark:bg-green-500" />
              </div>
              <span>More</span>
            </div>
            <div className="flex items-center gap-4 text-sm">
              <div>
                <span className="text-muted-foreground">Total contributions: </span>
                <span className="font-semibold">{data.reduce((sum, d) => sum + d.count, 0)}</span>
              </div>
              <div>
                <span className="text-muted-foreground">Streak: </span>
                <span className="font-semibold text-green-600 dark:text-green-400">12 days 🔥</span>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
