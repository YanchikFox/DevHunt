"use client"

import { useEffect } from "react"
import { useTranslations } from "next-intl"
import { Link, usePathname } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { ChevronLeft, ChevronRight, LogOut, Plus } from "lucide-react"
import { cn } from "@/lib/utils"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { useProjectsList } from "@/lib/api/queries/projects"
import { type NavItem, isLinkActive } from "./dashboardNav"

interface DashboardSidebarProps {
  navItems: NavItem[]
  collapsed: boolean
  onToggle: () => void
  onLogout: () => void
  mobileOpen?: boolean
  onMobileClose?: () => void
}

export function DashboardSidebar({ navItems, collapsed, onToggle, onLogout, mobileOpen, onMobileClose }: DashboardSidebarProps) {
  const pathname = usePathname()
  const t = useTranslations()

  return (
    <>
      {mobileOpen && onMobileClose && (
        <MobileDrawer
          navItems={navItems}
          onClose={onMobileClose}
          onLogout={onLogout}
        />
      )}

    <TooltipProvider delayDuration={0}>
      <aside
        className={cn(
          "hidden lg:flex fixed left-0 top-0 h-screen flex-col z-30 transition-[width] duration-300 ease-in-out",
          "border-r border-border bg-bg-subtle",
          collapsed ? "w-[60px]" : "w-60"
        )}
      >
        <SidebarLogo collapsed={collapsed} />

        <nav className={cn(
          "flex flex-1 flex-col gap-0.5 overflow-y-auto no-scrollbar",
          collapsed ? "px-2" : "px-3"
        )}>
          {navItems.map((item) => (
            <NavLinkItem
              key={item.href}
              item={item}
              isActive={isLinkActive(pathname, item.href)}
              collapsed={collapsed}
              label={t(item.labelKey)}
            />
          ))}
        </nav>

        {/* My projects section */}
        {!collapsed && <SidebarProjectsList />}

        <SidebarFooter collapsed={collapsed} onToggle={onToggle} />
      </aside>
    </TooltipProvider>
    </>
  )
}

/* ────────────────── Logo ────────────────── */

function SidebarLogo({ collapsed }: { collapsed: boolean }) {
  return (
    <div className={cn(
      "shrink-0 flex items-center",
      collapsed ? "justify-center px-1 pt-4 pb-4" : "justify-start px-5 pt-[22px] pb-4"
    )}>
      <Link href="/dashboard" className="flex items-center gap-2 group">
        <div className={cn(
          "flex items-center justify-center rounded-md bg-primary text-primary-foreground shrink-0",
          collapsed ? "h-5 w-5" : "h-6 w-6"
        )}>
          <span className={cn("font-mono font-bold", collapsed ? "text-[10px]" : "text-[11px]")}>◢</span>
        </div>
        {!collapsed && (
          <span className="text-[15px] font-semibold text-foreground tracking-[-0.3px]">
            DevHunt
          </span>
        )}
      </Link>
    </div>
  )
}

/* ────────────────── Nav Item ────────────────── */

interface NavLinkItemProps {
  item: NavItem
  isActive: boolean
  collapsed: boolean
  label: string
  onClick?: () => void
}

function NavLinkItem({ item, isActive, collapsed, label, onClick }: NavLinkItemProps) {
  const linkContent = (
    <Link
      href={item.href}
      onClick={onClick}
      aria-current={isActive ? "page" : undefined}
      className={cn(
        "flex items-center gap-2.5 rounded-[10px] text-[13px] transition-all duration-150",
        collapsed ? "justify-center p-2.5" : "px-2.5 py-2",
        isActive
          ? "bg-bg-elevated text-foreground shadow-sm font-medium"
          : "text-muted-foreground hover:text-foreground hover:bg-bg-hover"
      )}
    >
      <item.icon className="h-4 w-4 shrink-0" />
      {!collapsed && <span className="flex-1 truncate">{label}</span>}
      {!collapsed &&
        (item.labelKey === "navigation.chats" || item.labelKey === "navigation.aiPlan") && (
          <span className="rounded bg-primary/10 px-1.5 py-0.5 font-mono text-[9px] text-primary">
            AI
          </span>
        )}
    </Link>
  )

  if (collapsed) {
    return (
      <Tooltip>
        <TooltipTrigger asChild>{linkContent}</TooltipTrigger>
        <TooltipContent side="right" className="text-xs font-medium">
          {label}
        </TooltipContent>
      </Tooltip>
    )
  }

  return <div>{linkContent}</div>
}

/* ────────────────── Projects List ────────────────── */

function SidebarProjectsList() {
  const { data: projects } = useProjectsList()
  const pathname = usePathname()
  const preview = (projects ?? []).slice(0, 4)

  if (preview.length === 0) return null

  return (
    <div className="px-3 pt-4 pb-3">
      <div className="flex items-center justify-between px-1.5 mb-2">
        <span className="caption">[My projects]</span>
        <Link href="/dashboard/projects" className="text-muted-foreground hover:text-primary transition-colors">
          <Plus className="h-3 w-3" />
        </Link>
      </div>
      <div className="flex flex-col">
        {preview.map((p) => {
          const active = pathname.includes(`/projects/${p.id}`)
          return (
            <Link
              key={p.id}
              href={`/dashboard/projects/${p.id}`}
              className={cn(
                "flex items-center gap-2 px-2.5 py-1.5 rounded-[6px] text-[12px] transition-all duration-150",
                active
                  ? "bg-bg-elevated text-foreground shadow-sm"
                  : "text-muted-foreground hover:text-foreground hover:bg-bg-hover"
              )}
            >
              <div className="flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-[4px] bg-primary/10 font-mono text-[10px] font-semibold text-primary">
                {p.title?.[0] ?? "?"}
              </div>
              <span className="flex-1 truncate">{p.title}</span>
            </Link>
          )
        })}
      </div>
    </div>
  )
}

/* ────────────────── Footer ────────────────── */

function SidebarFooter({ collapsed, onToggle }: { collapsed: boolean; onToggle: () => void }) {
  return (
    <div className="shrink-0 border-t border-border p-3">
      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-full text-muted-foreground hover:text-foreground/80"
        onClick={onToggle}
        aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
      >
        {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
      </Button>
    </div>
  )
}

/* ────────────────── Mobile Drawer ────────────────── */

function MobileDrawer({
  navItems,
  onClose,
  onLogout,
}: {
  navItems: NavItem[]
  onClose: () => void
  onLogout: () => void
}) {
  const pathname = usePathname()
  const t = useTranslations()

  useEffect(() => {
    document.body.style.overflow = "hidden"
    return () => {
      document.body.style.overflow = ""
    }
  }, [])

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/40 backdrop-blur-sm fade-in lg:hidden"
        aria-hidden="true"
        onClick={onClose}
      />

      {/* Drawer panel */}
      <div
        id="mobile-nav-drawer"
        role="dialog"
        aria-modal="true"
        aria-label="Navigation"
        className="fixed left-0 top-0 z-50 flex h-screen w-72 flex-col border-r border-border bg-bg-subtle shadow-2xl animate-in lg:hidden"
      >
        <SidebarLogo collapsed={false} />

        <nav className="flex-1 p-3 space-y-0.5 overflow-y-auto" aria-label="Main navigation">
          {navItems.map((item) => (
            <NavLinkItem
              key={item.href}
              item={item}
              isActive={isLinkActive(pathname, item.href)}
              collapsed={false}
              label={t(item.labelKey)}
              onClick={onClose}
            />
          ))}
        </nav>

        <div className="border-t border-border p-3">
          <Button
            variant="ghost"
            size="sm"
            className="w-full justify-start text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors text-[13px]"
            onClick={onLogout}
          >
            <LogOut className="mr-2 h-4 w-4" aria-hidden="true" />
            {t("auth.logout")}
          </Button>
        </div>
      </div>
    </>
  )
}
