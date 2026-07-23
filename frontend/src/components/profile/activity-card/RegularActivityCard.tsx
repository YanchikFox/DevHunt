import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import type { EventVisual } from "./types"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

/**
 * Props for the RegularActivityCard component
 */
export interface RegularActivityCardProps {
    activity: UserActivityFeedItem
    actorName: string
    avatarInitial: string
    summary: string
    timeAgo: string
    targetLine: string
    eventGroup: string
    visual: EventVisual
}

/**
 * Renders a standard activity card
 */
export function RegularActivityCard({
    activity,
    actorName,
    avatarInitial,
    summary,
    timeAgo,
    targetLine,
    eventGroup,
    visual,
}: Readonly<RegularActivityCardProps>) {
    const Icon = visual.icon

    return (
        <div className="rounded-xl border border-border bg-card p-4 transition-all hover:border-border/80 hover:bg-accent">
            <div className="flex gap-3">
                <Avatar className="h-9 w-9 ring-2 ring-border">
                    {activity.actorAvatarUrl ? (
                        <AvatarImage src={activity.actorAvatarUrl} alt={actorName} />
                    ) : (
                        <AvatarFallback className="text-sm font-semibold bg-primary/10 text-primary">{avatarInitial}</AvatarFallback>
                    )}
                </Avatar>
                <div className="flex-1 space-y-3">
                    <div className="flex items-start justify-between">
                        <div className="flex items-center gap-2">
                            <div className={`inline-flex h-7 w-7 items-center justify-center rounded-lg ${visual.backgroundClass}`}>
                                <Icon className={`h-4 w-4 ${visual.accentClass}`} />
                            </div>
                            <div>
                                <p className="text-sm font-semibold text-foreground leading-tight">{actorName}</p>
                                <p className="text-xs uppercase tracking-wide text-muted-foreground">{visual.label}</p>
                            </div>
                        </div>
                    <div className="flex items-center gap-2">
                        <span className="text-xs text-muted-foreground">{timeAgo}</span>
                        <ReportDialog targetType={REPORT_TARGET_TYPES.COMMUNITY_POST} targetId={activity.id} />
                    </div>
                    </div>
                    <p className="text-sm font-medium text-muted-foreground leading-relaxed">{summary}</p>
                    {targetLine && <p className="text-xs text-muted-foreground">{targetLine}</p>}
                    <div className="flex flex-wrap gap-2">
                        <Badge variant="secondary" className="bg-primary/10 border border-primary/20 text-primary text-xs">
                            {eventGroup}
                        </Badge>
                    </div>
                </div>
            </div>
        </div>
    )
}
