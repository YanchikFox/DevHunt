"use client"

import { useTranslations } from "next-intl"
import { toast } from "@/hooks/use-toast"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Loader2, ShieldBan } from "lucide-react"
import {
  useBannedChannelMembers,
  useUnbanChannelMember,
} from "@/lib/api/queries/channel-members"
import type { ProjectChannel } from "@/lib/api/queries/project-channels"

type Props = {
  readonly projectId: string
  readonly channel: ProjectChannel
}

/** Ban-list tab — shows explicitly banned users with an unban button. */
export function ChannelBansTab({ projectId, channel }: Props) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")

  const { data: banned = [], isLoading } = useBannedChannelMembers(
    projectId,
    channel.id,
  )
  const unbanMutation = useUnbanChannelMember(projectId, channel.id)

  const handleUnban = async (userId: string) => {
    try {
      await unbanMutation.mutateAsync(userId)
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Unban failed",
        variant: "destructive",
      })
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
        <Loader2 className="mr-2 size-4 animate-spin" /> {tCommon("loading")}
      </div>
    )
  }

  if (banned.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-10 text-center text-sm text-muted-foreground">
        <ShieldBan className="size-6 opacity-50" />
        <p>{t("bansEmpty")}</p>
      </div>
    )
  }

  return (
    <div className="max-h-[360px] space-y-2 overflow-y-auto pr-1">
      {banned.map((m) => {
        const initials = m.fullName.slice(0, 1).toUpperCase() || "?"
        const bannedDate = m.bannedAt
          ? new Date(m.bannedAt).toLocaleDateString()
          : "—"
        return (
          <div
            key={m.userId}
            className="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2"
          >
            <Avatar className="h-8 w-8">
              <AvatarImage src={m.avatarUrl ?? undefined} alt={m.fullName} />
              <AvatarFallback className="bg-destructive/10 text-xs font-semibold text-destructive">
                {initials}
              </AvatarFallback>
            </Avatar>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-medium">{m.fullName}</div>
              <div className="text-[11px] text-muted-foreground">
                {t("bannedOn", { date: bannedDate })}
              </div>
              {m.banReason && (
                <div className="mt-1 text-xs italic text-muted-foreground">
                  “{m.banReason}”
                </div>
              )}
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={() => handleUnban(m.userId)}
              disabled={unbanMutation.isPending}
            >
              {t("unbanMember")}
            </Button>
          </div>
        )
      })}
    </div>
  )
}
