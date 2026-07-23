"use client"

import { useState, useMemo, useCallback } from "react"
import { useTranslations } from "next-intl"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Badge } from "@/components/ui/badge"
import {
  GitBranch,
  GitCommit,
  Code2,
  Activity,
  Users,
  LayoutDashboard,
} from "lucide-react";
// Lazy load heavy components for better bundle optimization
import dynamic from "next/dynamic"
import { GitCommitTree } from "@/components/features/GitCommitTree"
import { GitStats } from "@/components/features/GitStats"
import type { CommitDiff } from "@/components/features/CommitDiffViewer"
import { cn } from "@/lib/utils"

// Lazy load heavy components - load only when needed
const CommitDiffViewer = dynamic(
  () =>
    import("@/components/features/CommitDiffViewer").then((mod) => ({
      default: mod.CommitDiffViewer,
    })),
  {
    loading: () => <div className="p-8 text-center text-muted-foreground animate-pulse">Loading diff viewer...</div>,
    ssr: false,
  }
)

const GitHubIntegration = dynamic(
  () =>
    import("@/components/features/GitHubIntegration").then((mod) => ({
      default: mod.GitHubIntegration,
    })),
  {
    loading: () => <div className="p-8 text-center text-muted-foreground animate-pulse">Loading GitHub integration...</div>,
    ssr: false,
  }
)

const KanbanBoard = dynamic(
  () => import("@/components/features/KanbanBoard").then((mod) => ({ default: mod.KanbanBoard })),
  {
    loading: () => <div className="p-8 text-center text-muted-foreground animate-pulse">Loading Kanban board...</div>,
    ssr: false,
  }
)

const ActivityHeatmap = dynamic(
  () =>
    import("@/components/features/ActivityHeatmap").then((mod) => ({
      default: mod.ActivityHeatmap,
    })),
  {
    loading: () => <div className="p-8 text-center text-muted-foreground animate-pulse">Loading activity heatmap...</div>,
    ssr: false,
  }
)

const CodeQualityInsights = dynamic(
  () =>
    import("@/components/features/CodeQualityInsights").then((mod) => ({
      default: mod.CodeQualityInsights,
    })),
  {
    loading: () => <div className="p-8 text-center text-muted-foreground animate-pulse">Loading code quality insights...</div>,
    ssr: false,
  }
)

// Realistic commit tree with proper Git history
const mockCommits = [
  // First commit - initial point
  {
    id: "1",
    message: "Initial commit",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 120 * 60 * 60 * 1000).toISOString(),
    hash: "a1b2c3d4e5f6",
    branch: "main",
    isMerge: false,
  },
  // ... (rest of mock commits same as before, preserving data for demo)
  {
    id: "2",
    message: "feat: add project structure and setup",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 108 * 60 * 60 * 1000).toISOString(),
    hash: "b2c3d4e5f6a1",
    branch: "main",
    isMerge: false,
    parentHash: "a1b2c3d4e5f6",
  },
  {
    id: "3",
    message: "feat: implement user authentication",
    author: "Sarah Chen",
    email: "sarah.chen@devhunt.com",
    date: new Date(Date.now() - 96 * 60 * 60 * 1000).toISOString(),
    hash: "c3d4e5f6a1b2",
    branch: "main",
    isMerge: false,
    parentHash: "b2c3d4e5f6a1",
  },
  {
    id: "4",
    message: "refactor: improve API architecture",
    author: "Mike Wilson",
    email: "mike.wilson@devhunt.com",
    date: new Date(Date.now() - 84 * 60 * 60 * 1000).toISOString(),
    hash: "d4e5f6a1b2c3",
    branch: "develop",
    isMerge: false,
    parentHash: "c3d4e5f6a1b2",
  },
  {
    id: "5",
    message: "feat: add dark mode toggle",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 72 * 60 * 60 * 1000).toISOString(),
    hash: "e5f6a1b2c3d4",
    branch: "feature/dark-mode",
    isMerge: false,
    parentHash: "d4e5f6a1b2c3",
  },
  {
    id: "6",
    message: "feat: add push notifications",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 68 * 60 * 60 * 1000).toISOString(),
    hash: "f6a1b2c3d4e5",
    branch: "feature/notifications",
    isMerge: false,
    parentHash: "d4e5f6a1b2c3",
  },
  {
    id: "7",
    message: "fix: dark mode persistence issue",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 64 * 60 * 60 * 1000).toISOString(),
    hash: "g7h8i9j0k1l2",
    branch: "feature/dark-mode",
    isMerge: false,
    parentHash: "e5f6a1b2c3d4",
  },
  {
    id: "8",
    message: "test: add dark mode unit tests",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 60 * 60 * 60 * 1000).toISOString(),
    hash: "h8i9j0k1l2m3",
    branch: "feature/dark-mode",
    isMerge: false,
    parentHash: "g7h8i9j0k1l2",
  },
  {
    id: "9",
    message: "docs: update contributing guidelines",
    author: "Mike Wilson",
    email: "mike.wilson@devhunt.com",
    date: new Date(Date.now() - 56 * 60 * 60 * 1000).toISOString(),
    hash: "i9j0k1l2m3n4",
    branch: "develop",
    isMerge: false,
    parentHash: "d4e5f6a1b2c3",
  },
  {
    id: "10",
    message: "feat: implement notification preferences",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 52 * 60 * 60 * 1000).toISOString(),
    hash: "j0k1l2m3n4o5",
    branch: "feature/notifications",
    isMerge: false,
    parentHash: "f6a1b2c3d4e5",
  },
  {
    id: "11",
    message: "Merge branch 'feature/dark-mode' into develop",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 48 * 60 * 60 * 1000).toISOString(),
    hash: "k1l2m3n4o5p6",
    branch: "develop",
    isMerge: true,
    parentHash: "i9j0k1l2m3n4",
    mergeParentHash: "h8i9j0k1l2m3",
  },
  {
    id: "12",
    message: "fix: resolve session timeout bug",
    author: "Sarah Chen",
    email: "sarah.chen@devhunt.com",
    date: new Date(Date.now() - 44 * 60 * 60 * 1000).toISOString(),
    hash: "l2m3n4o5p6q7",
    branch: "main",
    isMerge: false,
    parentHash: "c3d4e5f6a1b2",
  },
  {
    id: "13",
    message: "fix: critical security vulnerability",
    author: "Sarah Chen",
    email: "sarah.chen@devhunt.com",
    date: new Date(Date.now() - 40 * 60 * 60 * 1000).toISOString(),
    hash: "m3n4o5p6q7r8",
    branch: "hotfix/security",
    isMerge: false,
    parentHash: "l2m3n4o5p6q7",
  },
  {
    id: "14",
    message: "Merge branch 'hotfix/security' into main",
    author: "Sarah Chen",
    email: "sarah.chen@devhunt.com",
    date: new Date(Date.now() - 36 * 60 * 60 * 1000).toISOString(),
    hash: "n4o5p6q7r8s9",
    branch: "main",
    isMerge: true,
    parentHash: "l2m3n4o5p6q7",
    mergeParentHash: "m3n4o5p6q7r8",
  },
  {
    id: "15",
    message: "refactor: optimize notification delivery",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 32 * 60 * 60 * 1000).toISOString(),
    hash: "o5p6q7r8s9t0",
    branch: "feature/notifications",
    isMerge: false,
    parentHash: "j0k1l2m3n4o5",
  },
  {
    id: "16",
    message: "Merge branch 'feature/notifications' into develop",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 28 * 60 * 60 * 1000).toISOString(),
    hash: "p6q7r8s9t0u1",
    branch: "develop",
    isMerge: true,
    parentHash: "k1l2m3n4o5p6",
    mergeParentHash: "o5p6q7r8s9t0",
  },
  {
    id: "17",
    message: "feat: add real-time collaboration",
    author: "Mike Wilson",
    email: "mike.wilson@devhunt.com",
    date: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    hash: "q7r8s9t0u1v2",
    branch: "develop",
    isMerge: false,
    parentHash: "p6q7r8s9t0u1",
  },
  {
    id: "18",
    message: "feat: add project analytics dashboard",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 20 * 60 * 60 * 1000).toISOString(),
    hash: "r8s9t0u1v2w3",
    branch: "feature/analytics",
    isMerge: false,
    parentHash: "q7r8s9t0u1v2",
  },
  {
    id: "19",
    message: "chore: update dependencies",
    author: "Sarah Chen",
    email: "sarah.chen@devhunt.com",
    date: new Date(Date.now() - 16 * 60 * 60 * 1000).toISOString(),
    hash: "s9t0u1v2w3x4",
    branch: "main",
    isMerge: false,
    parentHash: "n4o5p6q7r8s9",
  },
  {
    id: "20",
    message: "fix: correct chart rendering issues",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 12 * 60 * 60 * 1000).toISOString(),
    hash: "t0u1v2w3x4y5",
    branch: "feature/analytics",
    isMerge: false,
    parentHash: "r8s9t0u1v2w3",
  },
  {
    id: "21",
    message: "test: add e2e tests for collaboration",
    author: "Mike Wilson",
    email: "mike.wilson@devhunt.com",
    date: new Date(Date.now() - 8 * 60 * 60 * 1000).toISOString(),
    hash: "u1v2w3x4y5z6",
    branch: "develop",
    isMerge: false,
    parentHash: "q7r8s9t0u1v2",
  },
  {
    id: "22",
    message: "Merge branch 'feature/analytics' into develop",
    author: "Emma Davis",
    email: "emma.davis@devhunt.com",
    date: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(),
    hash: "v2w3x4y5z6a1",
    branch: "develop",
    isMerge: true,
    parentHash: "u1v2w3x4y5z6",
    mergeParentHash: "t0u1v2w3x4y5",
  },
  {
    id: "23",
    message: "feat: add API rate limiting",
    author: "Alex Johnson",
    email: "alex.johnson@devhunt.com",
    date: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    hash: "w3x4y5z6a1b2",
    branch: "main",
    isMerge: false,
    parentHash: "s9t0u1v2w3x4",
  },
]

const mockDiff: CommitDiff = {
  commitId: "1",
  hash: "a1b2c3d",
  message: "feat: add user authentication system",
  author: "Alex Johnson",
  date: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  files: [
    {
      path: "src/auth/login.tsx",
      additions: 45,
      deletions: 12,
      lines: [
        {
          type: "context",
          content: "import { useState } from 'react'",
          newLineNumber: 1,
          oldLineNumber: 1,
        },
        { type: "added", content: "import { useAuth } from '@/hooks/use-auth'", newLineNumber: 2 },
        { type: "added", content: "", newLineNumber: 3 },
        {
          type: "context",
          content: "export function LoginForm() {",
          newLineNumber: 4,
          oldLineNumber: 2,
        },
        { type: "added", content: "  const { login, isLoading } = useAuth()", newLineNumber: 5 },
        { type: "removed", content: "  const [email, setEmail] = useState('')", oldLineNumber: 3 },
        {
          type: "removed",
          content: "  const [password, setPassword] = useState('')",
          oldLineNumber: 4,
        },
      ],
    },
    {
      path: "src/components/ui/auth-button.tsx",
      additions: 32,
      deletions: 0,
      lines: [
        { type: "added", content: "export function AuthButton() {", newLineNumber: 1 },
        { type: "added", content: "  return <Button>Login</Button>", newLineNumber: 2 },
        { type: "added", content: "}", newLineNumber: 3 },
      ],
    },
  ],
}

export default function WorkspacePage() {
  const t = useTranslations()
  const [activeTab, setActiveTab] = useState("overview")
  const [selectedCommit, setSelectedCommit] = useState<string | null>(mockCommits[0].id)

  const handleRecentActivitySelect = useCallback((id: string) => {
    setActiveTab("commits")
    setSelectedCommit(id)
  }, [])

  const selectedCommitDiff = useMemo(() => {
    if (!selectedCommit) return null
    return mockDiff
  }, [selectedCommit])

  return (
    <div className="space-y-6 fade-in">
      {/* Header */}
      <div>
        <span className="caption mb-1.5 block">[Dev]</span>
        <h1 className="font-serif text-[32px] font-normal tracking-[-0.5px] leading-none text-foreground">
          {t("navigation.workspace")}
        </h1>
        <p className="text-[14px] text-muted-foreground mt-2">
          Code collaboration, Git visualization, and project insights
        </p>
      </div>

      {/* GitHub Integration Card */}
      <GitHubIntegration />

      {/* Stats */}
      <GitStats
        totalCommits={127}
        activeBranches={4}
        contributors={[
          { name: "Alex Johnson", commits: 45, additions: 2340, deletions: 890 },
          { name: "Sarah Chen", commits: 32, additions: 1560, deletions: 420 },
          { name: "Mike Wilson", commits: 28, additions: 1890, deletions: 650 },
          { name: "Emma Davis", commits: 22, additions: 1120, deletions: 380 },
        ]}
        commitsLastWeek={18}
        codeChanges={{ additions: 6910, deletions: 2340 }}
      />

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <div className="overflow-x-auto pb-2 -mx-4 px-4 sm:mx-0 sm:px-0 sm:pb-0">
          <div className="flex gap-0.5 bg-secondary p-[3px] rounded-[10px] w-fit">
            <TabsList className="bg-transparent border-0 p-0 h-auto gap-0.5">
              {[
                { id: "overview", icon: Activity, label: "Overview" },
                { id: "tasks", icon: LayoutDashboard, label: "Tasks" },
                { id: "commits", icon: GitCommit, label: "Commits" },
                { id: "branches", icon: GitBranch, label: "Branches" },
                { id: "code-review", icon: Code2, label: "Review" },
              ].map((tab) => (
                <TabsTrigger
                  key={tab.id}
                  value={tab.id}
                  className="gap-1.5 px-3 py-[5px] rounded-[6px] text-[12px] font-medium data-[state=active]:bg-card data-[state=active]:text-foreground data-[state=active]:shadow-sm transition-all duration-150"
                >
                  <tab.icon className="h-3 w-3" />
                  {tab.label}
                </TabsTrigger>
              ))}
            </TabsList>
          </div>
        </div>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-8 animate-in fade-in slide-in-from-left-2 duration-300">
          {/* Activity Heatmap */}
          <ActivityHeatmap />

          {/* Grid Layout */}
          <div className="grid gap-8 lg:grid-cols-2">
            {/* Recent Activity */}
            <Card className="border border-border/50 bg-card shadow-sm overflow-hidden">
              <CardHeader className="bg-muted/20 pb-4 border-b border-border/40">
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Activity className="h-5 w-5 text-primary" />
                  Recent Activity
                </CardTitle>
                <CardDescription>Latest commits and changes</CardDescription>
              </CardHeader>
              <CardContent className="space-y-1 p-0">
                {mockCommits.slice(0, 5).map((commit, i) => (
                  <div key={commit.id} className={cn("px-6 py-2", i !== 4 && "border-b border-border/30")}>
                    <RecentActivityItem
                      commit={commit}
                      onSelect={handleRecentActivitySelect}
                    />
                  </div>
                ))}
              </CardContent>
            </Card>

            {/* Contributors Activity */}
            <Card className="border border-border/50 bg-card shadow-sm overflow-hidden">
              <CardHeader className="bg-muted/20 pb-4 border-b border-border/40">
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Users className="h-5 w-5 text-primary" />
                  Top Contributors
                </CardTitle>
                <CardDescription>Most active developers this week</CardDescription>
              </CardHeader>
              <CardContent className="space-y-6 pt-6">
                {[
                  { name: "Alex Johnson", commits: 12, color: "bg-blue-500 shadow-blue-500/50" },
                  { name: "Sarah Chen", commits: 8, color: "bg-green-500 shadow-green-500/50" },
                  { name: "Mike Wilson", commits: 6, color: "bg-purple-500 shadow-purple-500/50" },
                  { name: "Emma Davis", commits: 4, color: "bg-orange-500 shadow-orange-500/50" },
                ].map((contributor) => (
                  <div key={contributor.name} className="space-y-2 group">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <div className={`w-3 h-3 rounded-full shadow-sm ${contributor.color}`} />
                        <span className="text-sm font-medium group-hover:text-primary transition-colors">{contributor.name}</span>
                      </div>
                      <span className="text-sm text-muted-foreground bg-muted px-2 py-0.5 rounded-full text-xs">
                        {contributor.commits} commits
                      </span>
                    </div>
                    <div className="h-2 bg-muted/60 rounded-full overflow-hidden">
                      <div
                        className={`h-full ${contributor.color.split(' ')[0]} transition duration-1000 ease-out`}
                        style={{ width: `${(contributor.commits / 12) * 100}%` }}
                      />
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          {/* Code Quality Insights */}
          <CodeQualityInsights />
        </TabsContent>

        {/* Tasks Tab */}
        <TabsContent value="tasks" className="space-y-4 animate-in fade-in slide-in-from-right-2 duration-300">
          <KanbanBoard />
        </TabsContent>

        {/* Commits Tab */}
        <TabsContent value="commits" className="space-y-4 animate-in fade-in slide-in-from-right-2 duration-300">
          <div className="grid gap-8 lg:grid-cols-3">
            <div className="lg:col-span-2 space-y-4">
              {selectedCommitDiff ? (
                <CommitDiffViewer diff={selectedCommitDiff} />
              ) : (
                <Card className="border border-border/50 bg-card border-dashed">
                  <CardContent className="p-12 text-center">
                    <div className="h-16 w-16 rounded-full bg-muted/50 flex items-center justify-center mx-auto mb-4">
                      <GitCommit className="h-8 w-8 text-muted-foreground opacity-50" />
                    </div>
                    <p className="text-muted-foreground">Select a commit to view changes</p>
                  </CardContent>
                </Card>
              )}
            </div>

            {/* Commit List */}
            <Card className="border border-border/50 bg-card shadow-sm h-fit">
              <CardHeader className="bg-muted/20 pb-4 border-b border-border/40">
                <CardTitle className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">Commit History</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 max-h-[600px] overflow-y-auto pt-4 pr-2 custom-scrollbar">
                {mockCommits.map((commit) => (
                  <CommitHistoryItem
                    key={commit.id}
                    commit={commit}
                    isSelected={selectedCommit === commit.id}
                    onSelect={setSelectedCommit}
                  />
                ))}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* Branches Tab */}
        <TabsContent value="branches" className="space-y-4 animate-in fade-in slide-in-from-right-2 duration-300">
          <GitCommitTree commits={mockCommits} />
        </TabsContent>

        {/* Code Review Tab */}
        <TabsContent value="code-review" className="space-y-6 animate-in fade-in slide-in-from-right-2 duration-300">
          <Card className="border border-border/50 bg-card shadow-sm">
            <CardHeader className="bg-muted/20 pb-4 border-b border-border/40">
              <CardTitle className="flex items-center gap-2 text-lg">
                <Code2 className="h-5 w-5 text-primary" />
                Pull Requests & Code Review
              </CardTitle>
              <CardDescription>Review code changes and provide feedback</CardDescription>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="grid gap-4 md:grid-cols-2">
                {[
                  {
                    id: "1",
                    title: "feat: add dark mode support",
                    author: "Emma Davis",
                    branch: "feature/dark-mode",
                    status: "open",
                    commits: 5,
                    additions: 320,
                    deletions: 89,
                  },
                  {
                    id: "2",
                    title: "fix: resolve authentication bug",
                    author: "Sarah Chen",
                    branch: "hotfix/auth-bug",
                    status: "merged",
                    commits: 2,
                    additions: 45,
                    deletions: 23,
                  },
                ].map((pr) => (
                  <div
                    key={pr.id}
                    className="p-5 rounded-xl border border-border/60 bg-card hover:border-primary/40 hover:shadow-lg transition cursor-pointer group"
                  >
                    <div className="flex items-start justify-between mb-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-2">
                          <h4 className="font-semibold text-base group-hover:text-primary transition-colors">
                            {pr.title}
                          </h4>
                          <Badge
                            variant={pr.status === "merged" ? "default" : "secondary"}
                            className={
                              pr.status === "merged"
                                ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 border-green-200 dark:border-green-800"
                                : "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 border-blue-200 dark:border-blue-800"
                            }
                          >
                            {pr.status}
                          </Badge>
                        </div>
                        <div className="flex items-center gap-4 text-sm text-muted-foreground mt-3">
                          <span className="flex items-center gap-1.5 bg-muted/50 px-2 py-1 rounded-md text-xs font-mono">
                            <GitBranch className="h-3 w-3" />
                            {pr.branch}
                          </span>
                        </div>
                      </div>
                      <Button variant="outline" size="sm" className="opacity-0 group-hover:opacity-100 transition-opacity">
                        Review
                      </Button>
                    </div>

                    <div className="flex items-center justify-between text-xs text-muted-foreground pt-3 border-t border-border/40">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-foreground">{pr.author}</span>
                        <span>•</span>
                        <span>{pr.commits} commits</span>
                      </div>
                      <div className="flex gap-2 font-mono">
                        <span className="text-green-600 dark:text-green-400">+{pr.additions}</span>
                        <span className="text-red-600 dark:text-red-400">-{pr.deletions}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}

interface RecentActivityItemProps {
  commit: (typeof mockCommits)[0]
  onSelect: (id: string) => void
}

function RecentActivityItem({ commit, onSelect }: RecentActivityItemProps) {
  const handleClick = useCallback(() => {
    onSelect(commit.id)
  }, [onSelect, commit.id])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      onSelect(commit.id)
    }
  }, [onSelect, commit.id])

  return (
    <div
      className="flex items-start gap-3 p-2 rounded-lg hover:bg-accent/40 transition-colors cursor-pointer group"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
    >
      <div
        className={`w-2.5 h-2.5 rounded-full mt-2 flex-shrink-0 shadow-sm transition-transform group-hover:scale-110 ${commit.isMerge ? "bg-purple-500 shadow-purple-500/30" : "bg-blue-500 shadow-blue-500/30"
          }`}
      />
      <div className="flex-1 min-w-0">
        <p className="font-medium text-sm text-foreground/90 group-hover:text-primary transition-colors line-clamp-1">
          {commit.message}
        </p>
        <div className="flex items-center gap-3 mt-1.5 text-xs text-muted-foreground">
          <span className="font-mono bg-muted/50 px-1.5 py-0.5 rounded text-[10px]">{commit.hash.substring(0, 7)}</span>
          <span>{commit.author}</span>
          <span>{new Date(commit.date).toLocaleDateString()}</span>
          <Badge variant="outline" className="text-[10px] h-4 px-1 border-border/50 bg-background/50">
            {commit.branch}
          </Badge>
        </div>
      </div>
    </div>
  )
}

interface CommitHistoryItemProps {
  commit: (typeof mockCommits)[0]
  isSelected: boolean
  onSelect: (id: string) => void
}

function CommitHistoryItem({ commit, isSelected, onSelect }: CommitHistoryItemProps) {
  const handleClick = useCallback(() => {
    onSelect(commit.id)
  }, [onSelect, commit.id])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      onSelect(commit.id)
    }
  }, [onSelect, commit.id])

  return (
    <div
      className={cn(
        "p-3 rounded-lg border cursor-pointer transition",
        isSelected
          ? "border-primary/50 bg-primary/5 shadow-inner"
          : "border-transparent hover:bg-accent/50 hover:border-border/60"
      )}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
    >
      <div className="flex items-start gap-3">
        <div
          className={`w-2 h-2 rounded-full mt-1.5 flex-shrink-0 ${commit.isMerge ? "bg-purple-500" : "bg-blue-500"
            }`}
        />
        <div className="flex-1 min-w-0">
          <p className={cn("font-medium text-xs line-clamp-2", isSelected ? "text-primary" : "text-foreground")}>{commit.message}</p>
          <div className="flex items-center gap-2 mt-1.5 text-[10px] text-muted-foreground">
            <span className="font-mono">{commit.hash.substring(0, 7)}</span>
            <span>{commit.author}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
