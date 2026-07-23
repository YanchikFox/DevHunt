"use client"

import { useState, useCallback } from "react"
import { useAdminContentStats } from "@/lib/api/queries/admin-content"
import { FolderGit2, Newspaper, MessageSquare, Eye } from "lucide-react"
import { useTranslations } from "next-intl"
import { ProjectsContent } from "./content/ProjectsContent"
import { NewsContent } from "./content/NewsContent"
import { CommentsContent } from "./content/CommentsContent"
import { ShowcaseContent } from "./content/ShowcaseContent"

type SubTab = "projects" | "news" | "comments" | "showcase"

// ── Sub-component for tab buttons ───────────────────────────

interface ContentTabButtonProps {
  tabKey: SubTab
  label: string
  icon: React.ReactNode
  count?: number
  isActive: boolean
  onTabChange: (tab: SubTab) => void
}

function ContentTabButton({ tabKey, label, icon, count, isActive, onTabChange }: ContentTabButtonProps) {
  const handleClick = useCallback(() => onTabChange(tabKey), [onTabChange, tabKey])

  return (
    <button
      type="button"
      onClick={handleClick}
      className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
        isActive
          ? "border-primary text-primary"
          : "border-transparent text-muted-foreground hover:text-foreground"
      }`}
    >
      {icon}
      {label}
      {count !== undefined && (
        <span className="text-xs bg-muted px-1.5 py-0.5 rounded-full">{count}</span>
      )}
    </button>
  )
}

export function ContentManagement() {
  const t = useTranslations("admin")
  const [activeTab, setActiveTab] = useState<SubTab>("projects")
  const { data: stats } = useAdminContentStats()

  const handleTabChange = useCallback((tab: SubTab) => {
    setActiveTab(tab)
  }, [])

  const tabs: { key: SubTab; label: string; icon: React.ReactNode; count?: number }[] = [
    { key: "projects", label: t("projects"), icon: <FolderGit2 className="h-4 w-4" />, count: stats?.projects },
    { key: "news", label: t("news"), icon: <Newspaper className="h-4 w-4" />, count: stats?.news },
    { key: "comments", label: t("comments"), icon: <MessageSquare className="h-4 w-4" />, count: (stats?.showcaseComments ?? 0) + (stats?.newsComments ?? 0) },
    { key: "showcase", label: t("showcase"), icon: <Eye className="h-4 w-4" />, count: stats?.showcase },
  ]

  return (
    <div className="space-y-6">
      {/* Sub-tabs */}
      <div className="flex gap-1 border-b">
        {tabs.map((tab) => (
          <ContentTabButton
            key={tab.key}
            tabKey={tab.key}
            label={tab.label}
            icon={tab.icon}
            count={tab.count}
            isActive={activeTab === tab.key}
            onTabChange={handleTabChange}
          />
        ))}
      </div>

      {/* Tab Content */}
      {activeTab === "projects" && <ProjectsContent />}
      {activeTab === "news" && <NewsContent />}
      {activeTab === "comments" && <CommentsContent />}
      {activeTab === "showcase" && <ShowcaseContent />}
    </div>
  )
}
