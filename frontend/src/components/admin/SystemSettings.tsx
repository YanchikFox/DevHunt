"use client"

import { useState, useCallback } from "react"
import { Settings, ToggleLeft, AlertTriangle, Shield } from "lucide-react"
import { useTranslations } from "next-intl"
import { MaintenanceToggle } from "./settings/MaintenanceToggle"
import { PlatformSettingsForm } from "./settings/PlatformSettingsForm"
import { FeatureFlagsPanel } from "./settings/FeatureFlagsPanel"
import { IpWhitelistManager } from "./settings/IpWhitelistManager"

type SubTab = "maintenance" | "settings" | "flags" | "whitelist"

// ── Sub-component for tab buttons ───────────────────────────

interface SettingsTabButtonProps {
  tabKey: SubTab
  label: string
  icon: React.ReactNode
  isActive: boolean
  onTabChange: (tab: SubTab) => void
}

function SettingsTabButton({ tabKey, label, icon, isActive, onTabChange }: SettingsTabButtonProps) {
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
    </button>
  )
}

export function SystemSettings() {
  const t = useTranslations("superAdmin")
  const [activeTab, setActiveTab] = useState<SubTab>("maintenance")

  const handleTabChange = useCallback((tab: SubTab) => {
    setActiveTab(tab)
  }, [])

  const tabs: { key: SubTab; label: string; icon: React.ReactNode }[] = [
    { key: "maintenance", label: t("maintenanceMode"), icon: <AlertTriangle className="h-4 w-4" /> },
    { key: "settings", label: t("platformSettings"), icon: <Settings className="h-4 w-4" /> },
    { key: "flags", label: t("featureFlags"), icon: <ToggleLeft className="h-4 w-4" /> },
    { key: "whitelist", label: t("ipWhitelist"), icon: <Shield className="h-4 w-4" /> },
  ]

  return (
    <div className="space-y-6">
      {/* Sub-tabs */}
      <div className="flex gap-1 border-b">
        {tabs.map((tab) => (
          <SettingsTabButton
            key={tab.key}
            tabKey={tab.key}
            label={tab.label}
            icon={tab.icon}
            isActive={activeTab === tab.key}
            onTabChange={handleTabChange}
          />
        ))}
      </div>

      {/* Tab Content */}
      {activeTab === "maintenance" && <MaintenanceToggle />}
      {activeTab === "settings" && <PlatformSettingsForm />}
      {activeTab === "flags" && <FeatureFlagsPanel />}
      {activeTab === "whitelist" && <IpWhitelistManager />}
    </div>
  )
}
