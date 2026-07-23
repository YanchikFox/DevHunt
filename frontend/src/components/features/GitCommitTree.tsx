"use client"

import { useState, useMemo, useCallback } from "react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { GitBranch, GitMerge, GitCommit, User, Calendar } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Represents a commit in the tree.
 */
export interface Commit {
  id: string
  hash: string
  message: string
  author: string
  email?: string
  date: string
  branch: string
  isMerge?: boolean
  parentHash?: string
  mergeParentHash?: string // Second parent for merge commits
}

/**
 * Props for the GitCommitTree component.
 */
export interface CommitTreeProps {
  /** List of commits to build the tree from */
  commits: Commit[]
}

// Colors for different branches
const branchColors = [
  "#60a5fa", // light blue
  "#34d399", // green
  "#a78bfa", // purple
  "#fb923c", // orange
  "#f472b6", // pink
]

interface CommitNode {
  commit: Commit
  x: number
  y: number
  branchIndex: number
  color: string
  parents: CommitNode[]
  children: CommitNode[]
}

function buildCommitNodes(sortedCommits: Commit[]): CommitNode[] {
  const nodes = new Map<string, CommitNode>()
  const branchColorMap = new Map<string, string>()
  const mainBranch =
    sortedCommits.find((c) => c.branch === "main" || c.branch === "master")?.branch ||
    sortedCommits[0].branch

  sortedCommits.forEach((commit, idx) => {
    if (!branchColorMap.has(commit.branch)) {
      const colorIdx = commit.branch === mainBranch ? 0 : branchColorMap.size
      branchColorMap.set(commit.branch, branchColors[colorIdx % branchColors.length])
    }
    nodes.set(commit.hash, {
      commit, x: 0, y: idx * 60 + 30, branchIndex: 0,
      color: branchColorMap.get(commit.branch) ?? branchColors[0],
      parents: [], children: [],
    })
  })

  nodes.forEach((node) => {
    if (node.commit.parentHash) {
      const parent = nodes.get(node.commit.parentHash)
      if (parent) { node.parents.push(parent); parent.children.push(node) }
    }
    if (node.commit.isMerge && node.commit.mergeParentHash) {
      const mergeParent = nodes.get(node.commit.mergeParentHash)
      if (mergeParent) { node.parents.push(mergeParent); mergeParent.children.push(node) }
    }
    if (node.parents.length === 0) {
      const commitIdx = sortedCommits.indexOf(node.commit)
      const prevInBranch = sortedCommits.slice(0, commitIdx).reverse().find((c) => c.branch === node.commit.branch)
      if (prevInBranch) {
        const parent = nodes.get(prevInBranch.hash)
        if (parent) { node.parents.push(parent); parent.children.push(node) }
      }
    }
  })

  let nextXIndex = 1
  const branchXMap = new Map<string, number>()
  branchXMap.set(mainBranch, 0)
  branchColorMap.forEach((_, branchName) => {
    if (branchName !== mainBranch && !branchXMap.has(branchName)) {
      branchXMap.set(branchName, nextXIndex++)
    }
  })
  sortedCommits.forEach((commit) => {
    const node = nodes.get(commit.hash)
    if (!node) return
    node.branchIndex = branchXMap.get(commit.branch) || 0
    node.x = 24 + node.branchIndex * 32
  })

  return Array.from(nodes.values())
}

function CommitDetailsPanel({ commit, commitNodes }: { commit: Commit; commitNodes: CommitNode[] }) {
  const nodeColor = commitNodes.find((n) => n.commit.id === commit.id)?.color || branchColors[0]
  return (
    <div className="border-t bg-muted/30 p-6">
      <div className="space-y-4">
        <div>
          <h3 className="text-lg font-semibold mb-3">{commit.message}</h3>
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <p className="text-muted-foreground mb-1">Author</p>
              <div className="flex items-center gap-2">
                <div className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-semibold" style={{ backgroundColor: nodeColor }}>
                  <User className="h-4 w-4" />
                </div>
                <div>
                  <p className="font-medium">{commit.author}</p>
                  {commit.email && <p className="text-xs text-muted-foreground">{commit.email}</p>}
                </div>
              </div>
            </div>
            <div>
              <p className="text-muted-foreground mb-1">Commit Hash</p>
              <p className="font-mono text-blue-600 dark:text-blue-400">{commit.hash}</p>
            </div>
            <div>
              <p className="text-muted-foreground mb-1">Date</p>
              <p>{new Date(commit.date).toLocaleString()}</p>
            </div>
            <div>
              <p className="text-muted-foreground mb-1">Branch</p>
              <Badge variant="outline" style={{ borderColor: nodeColor }}>{commit.branch}</Badge>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export function GitCommitTree({ commits }: CommitTreeProps) {
  const [selectedCommit, setSelectedCommit] = useState<string | null>(commits[0]?.id || null)

  const sortedCommits = useMemo(() => {
    return [...commits].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
  }, [commits])

  const commitNodes = useMemo(() => buildCommitNodes(sortedCommits), [sortedCommits])

  const selectedCommitData = sortedCommits.find((c) => c.id === selectedCommit)

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <GitBranch className="h-5 w-5 text-primary" />
          Commit Tree & History
        </CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        <div className="flex h-[700px]">
          {/* Left: Commit Graph (Date Order) */}
          <div className="w-96 border-r bg-gradient-to-b from-muted/20 to-muted/10 overflow-y-auto">
            <div className="sticky top-0 bg-muted/50 backdrop-blur-sm px-4 py-3 border-b z-10">
              <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Date Order
              </div>
            </div>
            <div className="p-4 relative" style={{ minHeight: `${sortedCommits.length * 60}px` }}>
              <svg
                className="absolute inset-0 pointer-events-none"
                style={{ width: "100%", height: `${sortedCommits.length * 60}px` }}
              >
                {/* Draw angular lines from parent to children (90 degrees) */}
                {commitNodes.map((node) => {
                  // Draw lines from all parents
                  return node.parents.map((parent) => {
                    // If commits are in the same branch (same X), draw vertical line
                    if (parent.x === node.x) {
                      return (
                        <line
                          key={`line-${parent.commit.hash}-${node.commit.hash}`}
                          x1={parent.x}
                          y1={parent.y + 8}
                          x2={node.x}
                          y2={node.y + 8}
                          stroke={node.color}
                          strokeWidth="3"
                          opacity="0.7"
                        />
                      )
                    }

                    // If branches are different, draw angular line:
                    // 1. Vertically down from parent
                    // 2. Horizontally to child's X position
                    // 3. Vertically down to child
                    const midY = node.y + 8

                    return (
                      <g key={`line-${parent.commit.hash}-${node.commit.hash}`}>
                        <line
                          x1={parent.x}
                          y1={parent.y + 8}
                          x2={parent.x}
                          y2={midY}
                          stroke={node.color}
                          strokeWidth="3"
                          opacity="0.7"
                        />
                        <line
                          x1={parent.x}
                          y1={midY}
                          x2={node.x}
                          y2={midY}
                          stroke={node.color}
                          strokeWidth="3"
                          opacity="0.7"
                        />
                        <line
                          x1={node.x}
                          y1={midY}
                          x2={node.x}
                          y2={node.y + 8}
                          stroke={node.color}
                          strokeWidth="3"
                          opacity="0.7"
                        />
                      </g>
                    )
                  })
                })}
              </svg>

              {/* Commit nodes */}
              {commitNodes.map((node) => (
                <CommitNodeItem
                  key={node.commit.id}
                  node={node}
                  isSelected={selectedCommit === node.commit.id}
                  onSelect={setSelectedCommit}
                />
              ))}
            </div>
          </div>

          {/* Right: Commit Details (Comment) */}
          <div className="flex-1 flex flex-col">
            <div className="sticky top-0 bg-muted/50 backdrop-blur-sm px-4 py-3 border-b z-10">
              <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Comment
              </div>
            </div>

            <div className="flex-1 overflow-y-auto">
              <div className="divide-y">
                {sortedCommits.map((commit) => {
                  const isSelected = selectedCommit === commit.id
                  const node = commitNodes.find((n) => n.commit.id === commit.id)
                  const branchColor = node?.color || branchColors[0]

                  return (
                    <CommitListItem
                      key={commit.id}
                      commit={commit}
                      isSelected={isSelected}
                      branchColor={branchColor}
                      onSelect={setSelectedCommit}
                    />
                  )
                })}
              </div>
            </div>
          </div>
        </div>

        {selectedCommitData && (
          <CommitDetailsPanel commit={selectedCommitData} commitNodes={commitNodes} />
        )}
      </CardContent>
    </Card>
  )
}

interface CommitNodeItemProps {
  node: CommitNode
  isSelected: boolean
  onSelect: (id: string) => void
}

function CommitNodeItem({ node, isSelected, onSelect }: CommitNodeItemProps) {
  const handleClick = useCallback(() => {
    onSelect(node.commit.id)
  }, [onSelect, node.commit.id])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      onSelect(node.commit.id)
    }
  }, [onSelect, node.commit.id])

  return (
    <div
      className="absolute cursor-pointer group z-10"
      style={{ left: `${node.x - 8}px`, top: `${node.y - 8}px` }}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
    >
      <div
        className={cn(
          "w-4 h-4 rounded-full border-2 border-background transition-all relative shadow-md",
          isSelected && "ring-2 ring-primary ring-offset-1 scale-150",
          "group-hover:scale-125 group-hover:shadow-lg"
        )}
        style={{ backgroundColor: node.color }}
      >
        {node.commit.isMerge && (
          <div className="absolute inset-0 flex items-center justify-center">
            <GitMerge className="h-2.5 w-2.5 text-white" />
          </div>
        )}
      </div>
    </div>
  )
}

interface CommitListItemProps {
  commit: Commit
  isSelected: boolean
  branchColor: string
  onSelect: (id: string) => void
}

function CommitListItem({ commit, isSelected, branchColor, onSelect }: CommitListItemProps) {
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
        "p-4 cursor-pointer transition-all group relative",
        isSelected && "bg-blue-500/10 dark:bg-blue-500/20"
      )}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
    >
      {/* Blue selection bar */}
      {isSelected && (
        <div className="absolute left-0 top-0 bottom-0 w-1 bg-blue-500 dark:bg-blue-400" />
      )}

      <div className="flex items-start gap-4">
        {/* Commit indicator */}
        <div className="flex-shrink-0 mt-1">
          <div
            className={cn(
              "w-5 h-5 rounded-full border-2 border-background flex items-center justify-center shadow-sm",
              isSelected && "ring-2 ring-primary ring-offset-1 scale-110"
            )}
            style={{ backgroundColor: branchColor }}
          >
            {commit.isMerge ? (
              <GitMerge className="h-3 w-3 text-white" />
            ) : (
              <GitCommit className="h-3 w-3 text-white" />
            )}
          </div>
        </div>

        {/* Commit information */}
        <div className="flex-1 min-w-0">
          <p
            className={cn(
              "font-medium text-sm leading-tight break-words mb-2",
              isSelected
                ? "text-primary font-semibold"
                : "group-hover:text-primary transition-colors"
            )}
          >
            {commit.message}
          </p>

          <div className="flex items-center gap-4 text-xs text-muted-foreground flex-wrap">
            <span className="font-mono text-blue-600 dark:text-blue-400">
              {commit.hash.slice(0, 7)}
            </span>

            <div className="flex items-center gap-1.5">
              <User className="h-3 w-3" />
              <span className="font-medium">{commit.author}</span>
              {commit.email && <span className="text-muted-foreground/70">({commit.email})</span>}
            </div>

            <div className="flex items-center gap-1.5">
              <Calendar className="h-3 w-3" />
              <span>{new Date(commit.date).toLocaleString()}</span>
            </div>

            <Badge variant="outline" className="text-xs" style={{ borderColor: branchColor }}>
              {commit.branch}
            </Badge>

            {commit.isMerge && (
              <Badge
                variant="secondary"
                className="bg-purple-100 text-purple-700 dark:bg-purple-900/20 dark:text-purple-400 text-xs"
              >
                <GitMerge className="h-3 w-3 mr-1" />
                Merge
              </Badge>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
