import {
  LayoutDashboard,
  FolderKanban,
  Users,
  MessageSquare,
  Mail,
  HelpCircle,
  ShieldCheck,
  Sparkles,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"
import { isAdminOrCurator } from "@/lib/constants/roles"

export interface NavItem {
  href: string
  labelKey: string
  icon: LucideIcon
  highlightColor: string
}

const BASE_NAV_ITEMS: NavItem[] = [
  { href: "/dashboard", labelKey: "navigation.dashboard", icon: LayoutDashboard, highlightColor: "text-amber-600" },
  { href: "/dashboard/ai", labelKey: "navigation.aiPlan", icon: Sparkles, highlightColor: "text-violet-500" },
  { href: "/dashboard/projects", labelKey: "navigation.projects", icon: FolderKanban, highlightColor: "text-amber-500" },
  { href: "/dashboard/teams", labelKey: "navigation.teams", icon: Users, highlightColor: "text-orange-500" },
  { href: "/dashboard/chats", labelKey: "navigation.chats", icon: MessageSquare, highlightColor: "text-emerald-500" },
  { href: "/dashboard/invitations", labelKey: "navigation.invitations", icon: Mail, highlightColor: "text-amber-400" },
  { href: "/dashboard/support", labelKey: "navigation.support", icon: HelpCircle, highlightColor: "text-stone-400" },
]

const ADMIN_NAV_ITEM: NavItem = {
  href: "/admin",
  labelKey: "navigation.admin",
  icon: ShieldCheck,
  highlightColor: "text-red-500",
}

export function getNavItems(role?: string): NavItem[] {
  if (isAdminOrCurator(role)) {
    return [...BASE_NAV_ITEMS, ADMIN_NAV_ITEM]
  }
  return BASE_NAV_ITEMS
}

export function isLinkActive(pathname: string, href: string): boolean {
  if (href === "/dashboard") return pathname === "/dashboard"
  return pathname.startsWith(href)
}
