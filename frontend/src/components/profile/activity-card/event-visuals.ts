import {
    Activity,
    FileText,
    Camera,
    RefreshCw,
    Sparkles,
    UserCheck,
    UserMinus,
    UserPlus,
    UserX,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"
import type { EventVisual } from "./types"

/** Static visual configuration without translations */
type EventVisualConfig = {
    icon: LucideIcon
    labelKey: string
    accentClass: string
    backgroundClass: string
}

/** Static event visual configurations (icon, classes, translation key) */
const EVENT_VISUAL_CONFIG: Record<string, EventVisualConfig> = {
    "user.follow": {
        icon: UserPlus,
        labelKey: "activity.userFollow",
        accentClass: "text-blue-600",
        backgroundClass: "border-blue-200 bg-blue-50",
    },
    "user.unfollow": {
        icon: UserMinus,
        labelKey: "activity.userUnfollow",
        accentClass: "text-muted-foreground",
        backgroundClass: "border-border bg-muted",
    },
    "user.created_project": {
        icon: Sparkles,
        labelKey: "activity.userCreatedProject",
        accentClass: "text-emerald-600",
        backgroundClass: "border-emerald-200 bg-emerald-50",
    },
    "user.joined_project": {
        icon: UserPlus,
        labelKey: "activity.userJoinedProject",
        accentClass: "text-cyan-600",
        backgroundClass: "border-cyan-200 bg-cyan-50",
    },
    "user.left_project": {
        icon: UserX,
        labelKey: "activity.userLeftProject",
        accentClass: "text-orange-600",
        backgroundClass: "border-orange-200 bg-orange-50",
    },
    "user.updated_profile": {
        icon: UserCheck,
        labelKey: "activity.userUpdatedProfile",
        accentClass: "text-primary",
        backgroundClass: "border-primary/20 bg-primary/10",
    },
    "project.news_published": {
        icon: FileText,
        labelKey: "activity.projectNewsPublished",
        accentClass: "text-primary",
        backgroundClass: "border-primary/20 bg-primary/10",
    },
    "project.media_uploaded": {
        icon: Camera,
        labelKey: "activity.projectMediaUploaded",
        accentClass: "text-primary",
        backgroundClass: "border-primary/20 bg-primary/10",
    },
    "project.state_changed": {
        icon: RefreshCw,
        labelKey: "activity.projectStateChanged",
        accentClass: "text-emerald-600",
        backgroundClass: "border-emerald-200 bg-emerald-50",
    },
    "task.created": {
        icon: Sparkles,
        labelKey: "activity.tasks",
        accentClass: "text-emerald-600",
        backgroundClass: "border-emerald-200 bg-emerald-50",
    },
    "task.updated": {
        icon: RefreshCw,
        labelKey: "activity.tasks",
        accentClass: "text-emerald-600",
        backgroundClass: "border-emerald-200 bg-emerald-50",
    },
    "task.status_changed": {
        icon: RefreshCw,
        labelKey: "activity.tasks",
        accentClass: "text-emerald-600",
        backgroundClass: "border-emerald-200 bg-emerald-50",
    },
    "task.deleted": {
        icon: UserMinus,
        labelKey: "activity.tasks",
        accentClass: "text-orange-600",
        backgroundClass: "border-orange-200 bg-orange-50",
    },
    "task.restored": {
        icon: UserPlus,
        labelKey: "activity.tasks",
        accentClass: "text-cyan-600",
        backgroundClass: "border-cyan-200 bg-cyan-50",
    },
}

/**
 * Returns visual configuration for each event type with translated labels
 */
export function getEventVisuals(t: (key: string) => string): Record<string, EventVisual> {
    const result: Record<string, EventVisual> = {}
    for (const [eventType, config] of Object.entries(EVENT_VISUAL_CONFIG)) {
        result[eventType] = {
            icon: config.icon,
            label: t(config.labelKey),
            accentClass: config.accentClass,
            backgroundClass: config.backgroundClass,
        }
    }
    return result
}

/**
 * Returns the default visual for unknown event types
 */
export function getDefaultVisual(t: (key: string) => string): EventVisual {
    return {
        icon: Activity,
        label: t("activity.activity"),
        accentClass: "text-muted-foreground",
        backgroundClass: "border-border bg-muted",
    }
}

/**
 * Derives the event group from the event type
 */
export function deriveEventGroup(eventType: string): string {
    const prefix = (eventType || "").split(".")[0]
    switch (prefix) {
        case "task":
            return "tasks"
        case "project":
            return "project"
        case "user":
            return "user"
        default:
            return "activity"
    }
}
