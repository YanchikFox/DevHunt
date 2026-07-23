"use client"

import { useState, useMemo, useEffect, useCallback } from "react"
import { isAdminOrCurator, isSuperAdmin as checkSuperAdmin } from "@/lib/constants/roles"
import { UserManagement } from "@/components/admin/UserManagement"
import { BadgeManagement } from "@/components/admin/BadgeManagement"
import { ProjectManagement } from "@/components/admin/ProjectManagement"
import { SupportTicketManagement } from "@/components/admin/SupportTicketManagement"
import { ModerationQueue } from "@/components/admin/ModerationQueue"
import { ProjectIssuesManagement } from "@/components/admin/ProjectIssuesManagement"
import { AdminStats } from "@/components/admin/AdminStats"
import { SuperAdminPanel } from "@/components/admin/SuperAdminPanel"
import { ChatManagement } from "@/components/admin/ChatManagement"
import { ContentManagement } from "@/components/admin/ContentManagement"
import { useCurrentUser } from "@/hooks/use-current-user"
import { useRouter } from "next/navigation"
import { Loader2, Users, FolderKanban, Ticket, Shield, Award, ShieldAlert, BarChart3, ChevronRight, MessageSquare, FileText } from "lucide-react"
import { useTranslations } from "next-intl"
import { cn } from "@/lib/utils"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"

type AdminSection = "overview" | "users" | "projects" | "tickets" | "moderation" | "badges" | "chats" | "content" | "superadmin"

interface NavItem {
  id: AdminSection
  labelKey: string
  icon: React.ElementType
  superadminOnly?: boolean
}

const NAV_ITEMS: NavItem[] = [
  { id: "overview", labelKey: "tabOverview", icon: BarChart3 },
  { id: "users", labelKey: "tabUsers", icon: Users },
  { id: "projects", labelKey: "tabProjects", icon: FolderKanban },
  { id: "tickets", labelKey: "tabTickets", icon: Ticket },
  { id: "moderation", labelKey: "tabModeration", icon: Shield },
  { id: "badges", labelKey: "tabBadges", icon: Award },
  { id: "chats", labelKey: "tabChats", icon: MessageSquare },
  { id: "content", labelKey: "tabContent", icon: FileText },
  { id: "superadmin", labelKey: "tabSuperAdmin", icon: ShieldAlert, superadminOnly: true },
]

function AdminNavButton({ item, isActive, onNavClick }: { item: NavItem; isActive: boolean; onNavClick: (section: AdminSection) => void }) {
  const t = useTranslations("admin")
  const Icon = item.icon
  const handleClick = useCallback(() => onNavClick(item.id), [onNavClick, item.id])
  return (
    <button
      onClick={handleClick}
      className={cn(
        "flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200 relative overflow-hidden",
        isActive
          ? "bg-gradient-to-r from-blue-600 to-violet-600 text-white shadow-md shadow-primary/20"
          : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
        item.superadminOnly && !isActive && "text-orange-500/80 hover:text-orange-600 hover:bg-orange-500/5"
      )}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span className="truncate">{t(item.labelKey)}</span>
      {isActive && <ChevronRight className="ml-auto h-4 w-4 opacity-70" />}
    </button>
  )
}

export default function AdminPage() {
  const { user, isLoading } = useCurrentUser()
  const router = useRouter()
  const t = useTranslations("admin")

  const [activeSection, setActiveSection] = useState<AdminSection>("overview")

  const isPrivileged = useMemo(
    () => isAdminOrCurator(user?.role),
    [user?.role]
  )
  const isSuperAdmin = checkSuperAdmin(user?.role)

  useEffect(() => {
    if (!isLoading && user && !isPrivileged) {
      router.push("/")
    }
  }, [user, isLoading, router, isPrivileged])

  const handleNavClick = useCallback((section: AdminSection) => {
    setActiveSection(section)
  }, [])

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  if (!user || !isPrivileged) {
    return null
  }

  const visibleNavItems = NAV_ITEMS.filter(
    (item) => !item.superadminOnly || isSuperAdmin
  )

  return (
    <div className="flex h-[calc(100vh-4rem)]">
      {/* Sidebar */}
      <aside className="w-60 shrink-0 border-r border-border/50 bg-card/30 overflow-y-auto">
        <div className="p-4 border-b border-border/50">
          <h2 className="text-lg font-bold tracking-tight">{t("dashboardTitle")}</h2>
          <p className="text-xs text-muted-foreground mt-0.5">{t("dashboardDescription")}</p>
        </div>
        <nav className="p-2 space-y-0.5">
          {visibleNavItems.map((item) => (
            <AdminNavButton
              key={item.id}
              item={item}
              isActive={activeSection === item.id}
              onNavClick={handleNavClick}
            />
          ))}
        </nav>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-y-auto bg-background">
        <div className="p-6 lg:p-8 max-w-6xl">
          {activeSection === "overview" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabOverview")} description={t("overviewDescription")} />
              <AdminStats />
            </div>
          )}

          {activeSection === "users" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabUsers")} description={t("usersDescription")} />
              <UserManagement />
            </div>
          )}

          {activeSection === "projects" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabProjects")} description={t("projectsDescription")} />
              <ProjectManagement />
            </div>
          )}

          {activeSection === "tickets" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabTickets")} description={t("ticketsDescription")} />
              <SupportTicketManagement />
            </div>
          )}

          {activeSection === "moderation" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabModeration")} description={t("moderationDescription")} />
              <Tabs defaultValue="reports" className="space-y-4">
                <TabsList>
                  <TabsTrigger value="reports">{t("moderationReports")}</TabsTrigger>
                  <TabsTrigger value="issues">{t("projectIssues")}</TabsTrigger>
                </TabsList>
                <TabsContent value="reports">
                  <ModerationQueue />
                </TabsContent>
                <TabsContent value="issues">
                  <ProjectIssuesManagement />
                </TabsContent>
              </Tabs>
            </div>
          )}

          {activeSection === "badges" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabBadges")} description={t("badgesDescription")} />
              <BadgeManagement />
            </div>
          )}

          {activeSection === "chats" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabChats")} description={t("chatsDescription")} />
              <ChatManagement />
            </div>
          )}

          {activeSection === "content" && (
            <div className="space-y-6">
              <SectionHeader title={t("tabContent")} description={t("contentDescription")} />
              <ContentManagement />
            </div>
          )}

          {activeSection === "superadmin" && isSuperAdmin && (
            <div className="space-y-6">
              <SectionHeader title={t("tabSuperAdmin")} description={t("superAdminDescription")} />
              <SuperAdminPanel />
            </div>
          )}
        </div>
      </main>
    </div>
  )
}

function SectionHeader({ title, description }: { title: string; description: string }) {
  return (
    <div className="pb-4 border-b border-border/50">
      <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
      <p className="text-sm text-muted-foreground mt-1">{description}</p>
    </div>
  )
}
