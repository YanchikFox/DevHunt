"use client"

import Image from "next/image"
import { useState, useCallback } from "react"
import { ExternalLink, GitBranch, Heart, Eye, Calendar, Users, Bell, BellOff } from "lucide-react"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"
import { format } from "date-fns"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { useLikeShowcase, useUnlikeShowcase, ShowcaseProject } from "@/lib/api/queries/showcase"
import { useProjectSubscription } from "@/lib/api/queries/project-extras"
import { ShowcaseComments } from "@/components/showcase/ShowcaseComments"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"

interface ShowcaseContentProps {
  showcase: ShowcaseProject
}

function ShowcaseVideoDemo({ demoVideoUrl, t }: { demoVideoUrl: string; t: (key: string) => string }) {
  return (
    <Card>
      <CardHeader><CardTitle>{t("showcase.videoDemo")}</CardTitle></CardHeader>
      <CardContent>
        <div className="aspect-video rounded-lg overflow-hidden bg-black">
          {demoVideoUrl.includes("youtube.com") || demoVideoUrl.includes("youtu.be") ? (
            <iframe src={demoVideoUrl.replace("watch?v=", "embed/")} className="w-full h-full" allowFullScreen title={t("common.projectDemo")} />
          ) : (
            <div className="flex items-center justify-center h-full">
              <a href={demoVideoUrl} target="_blank" rel="noopener noreferrer" className="text-white hover:underline">{t("showcase.watchVideo")}</a>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function ShowcaseSidebar({ showcase, t }: { showcase: ShowcaseProject; t: (key: string) => string }) {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            {t("projects.team")}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {showcase.team.map((member) => (
              <div key={member.fullName} className="flex items-center gap-3">
                <Avatar>
                  <AvatarImage src={member.avatarUrl || undefined} />
                  <AvatarFallback>{member.fullName.substring(0, 2).toUpperCase()}</AvatarFallback>
                </Avatar>
                <div>
                  <p className="font-medium text-sm">{member.fullName}</p>
                  <p className="text-xs text-muted-foreground">{member.role}</p>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {showcase.metrics && Object.keys(showcase.metrics).length > 0 && (
        <Card>
          <CardHeader><CardTitle>{t("showcase.metrics")}</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-2">
              {Object.entries(showcase.metrics).map(([key, value]) => (
                <div key={key} className="flex justify-between text-sm">
                  <span className="text-muted-foreground capitalize">{key.replaceAll("_", " ")}</span>
                  <span className="font-medium">{String(value)}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

export function ShowcaseContent({ showcase: initialShowcase }: Readonly<ShowcaseContentProps>) {
  const t = useTranslations()
  const [showcase, setShowcase] = useState(initialShowcase)
  const likeShowcase = useLikeShowcase()
  const unlikeShowcase = useUnlikeShowcase()
  const [isLiked, setIsLiked] = useState(false)
  const { isSubscribed, subscribe, unsubscribe, isToggling } = useProjectSubscription(showcase.project.projectId)

  const handleSubscribe = useCallback(() => {
    if (isSubscribed) {
      unsubscribe()
    } else {
      subscribe()
    }
  }, [isSubscribed, subscribe, unsubscribe])

  const handleLike = useCallback(async () => {
    try {
      if (isLiked) {
        const result = await unlikeShowcase.mutateAsync(showcase.project.projectId)
        setShowcase(prev => ({ ...prev, likesCount: result.likesCount }))
        setIsLiked(false)
      } else {
        const result = await likeShowcase.mutateAsync(showcase.project.projectId)
        setShowcase(prev => ({ ...prev, likesCount: result.likesCount }))
        setIsLiked(true)
      }
    } catch (error) {
      console.error("Failed to toggle like", error)
    }
  }, [isLiked, likeShowcase, unlikeShowcase, showcase.project.projectId])

  return (
    <div className="space-y-8">
      {/* Header Section */}
      <div className="space-y-4">
        <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <h1 className="text-4xl font-bold tracking-tight">{showcase.project.title}</h1>
              {showcase.featured && (
                <Badge className="bg-yellow-500/90 text-white border-0">Featured</Badge>
              )}
            </div>
            <p className="text-xl text-muted-foreground max-w-2xl">
              {showcase.project.description}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {showcase.demoUrl && (
              <Button asChild>
                <a href={showcase.demoUrl} target="_blank" rel="noopener noreferrer">
                  <ExternalLink className="mr-2 h-4 w-4" />
                  {t("showcase.liveDemo")}
                </a>
              </Button>
            )}
            {showcase.repositoryUrl && (
              <Button variant="outline" asChild>
                <a href={showcase.repositoryUrl} target="_blank" rel="noopener noreferrer">
                  <GitBranch className="mr-2 h-4 w-4" />
                  {t("showcase.repository")}
                </a>
              </Button>
            )}
            <Button
              variant="outline"
              size="icon"
              onClick={handleSubscribe}
              disabled={isToggling}
              title={isSubscribed ? t("showcase.unsubscribe") : t("showcase.subscribeToUpdates")}
            >
              {isSubscribed ? (
                <BellOff className="h-4 w-4" />
              ) : (
                <Bell className="h-4 w-4" />
              )}
            </Button>

            <ReportDialog targetType={REPORT_TARGET_TYPES.PROJECT} targetId={showcase.project.projectId} />
          </div>
        </div>

        <div className="flex items-center gap-4 text-sm text-muted-foreground">
          <div className="flex items-center gap-1">
            <Eye className="h-4 w-4" />
            <span>{t("showcase.viewsCount", { count: showcase.viewsCount })}</span>
          </div>
          <div className="flex items-center gap-1">
            <Calendar className="h-4 w-4" />
            <span>{t("showcase.publishedAt", { date: format(new Date(showcase.publishedAt), "MMM d, yyyy") })}</span>
          </div>
          <Button
            variant={isLiked ? "secondary" : "ghost"}
            size="sm"
            className={cn("gap-1", isLiked && "text-red-500")}
            onClick={handleLike}
            disabled={likeShowcase.isPending || unlikeShowcase.isPending}
          >
            <Heart className={cn("h-4 w-4", isLiked && "fill-current")} />
            <span>{showcase.likesCount}</span>
          </Button>
        </div>
      </div>

      {/* Screenshots Gallery */}
      {showcase.screenshots && showcase.screenshots.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {showcase.screenshots.map((url) => (
            <div key={url} className="relative aspect-video rounded-lg overflow-hidden border bg-muted">
              <Image
                fill
                src={url}
                alt={t("showcase.screenshotAlt", { number: showcase.screenshots.indexOf(url) + 1 })}
                className="object-cover hover:scale-105 transition-transform duration-300"
              />
            </div>
          ))}
        </div>
      )}

      {showcase.demoVideoUrl && <ShowcaseVideoDemo demoVideoUrl={showcase.demoVideoUrl} t={t} />}

      <div className="grid md:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="md:col-span-2 space-y-8">
          <Card>
            <CardHeader>
              <CardTitle>{t("showcase.aboutProject")}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="prose dark:prose-invert max-w-none whitespace-pre-wrap">
                {showcase.summary || t("showcase.noSummary")}
              </div>
            </CardContent>
          </Card>
        </div>

        <ShowcaseSidebar showcase={showcase} t={t} />
      </div>

      {/* Comments Section */}
      <ShowcaseComments projectId={showcase.project.projectId} />
    </div>
  )
}
