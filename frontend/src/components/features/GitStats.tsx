"use client"

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { TrendingUp, Users, GitCommit, Code2 } from "lucide-react"

/**
 * Represents a contributor's statistics.
 */
export interface Contributor {
  name: string
  commits: number
  additions: number
  deletions: number
}

/**
 * Props for the GitStats component.
 */
export interface GitStatsProps {
  /** Total number of commits in the repository */
  totalCommits: number
  /** Number of active branches */
  activeBranches: number
  /** List of top contributors */
  contributors: Contributor[]
  /** Number of commits in the last week */
  commitsLastWeek: number
  /** Code changes statistics */
  codeChanges: {
    additions: number
    deletions: number
  }
}

/**
 * Displays a summary of Git repository statistics.
 * Shows total commits, contributors, code changes, and activity.
 *
 * @example
 * ```tsx
 * <GitStats
 *   totalCommits={100}
 *   activeBranches={5}
 *   contributors={[{ name: "John", commits: 50, additions: 100, deletions: 20 }]}
 *   commitsLastWeek={10}
 *   codeChanges={{ additions: 500, deletions: 200 }}
 * />
 * ```
 */
export function GitStats({
  totalCommits,
  activeBranches,
  contributors,
  commitsLastWeek,
  codeChanges,
}: GitStatsProps) {
  const topContributor = contributors[0]

  return (
    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      <Card className="border-2 hover:border-primary/50 transition-colors">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
            <GitCommit className="h-4 w-4" />
            Total Commits
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-3xl font-bold">{totalCommits.toLocaleString()}</div>
          <p className="text-xs text-muted-foreground mt-1">{commitsLastWeek} this week</p>
        </CardContent>
      </Card>

      <Card className="border-2 hover:border-primary/50 transition-colors">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
            <Users className="h-4 w-4" />
            Contributors
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-3xl font-bold">{contributors.length}</div>
          <p className="text-xs text-muted-foreground mt-1">{topContributor?.name} most active</p>
        </CardContent>
      </Card>

      <Card className="border-2 hover:border-primary/50 transition-colors">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
            <Code2 className="h-4 w-4" />
            Code Changes
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-2">
            <span className="text-lg font-bold text-green-600 dark:text-green-400">
              +{codeChanges.additions.toLocaleString()}
            </span>
            <span className="text-lg font-bold text-red-600 dark:text-red-400">
              -{codeChanges.deletions.toLocaleString()}
            </span>
          </div>
          <p className="text-xs text-muted-foreground mt-1">
            Net: +{(codeChanges.additions - codeChanges.deletions).toLocaleString()}
          </p>
        </CardContent>
      </Card>

      <Card className="border-2 hover:border-primary/50 transition-colors">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
            <TrendingUp className="h-4 w-4" />
            Activity
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-3xl font-bold">{activeBranches}</div>
          <p className="text-xs text-muted-foreground mt-1">Active branches</p>
        </CardContent>
      </Card>
    </div>
  )
}
