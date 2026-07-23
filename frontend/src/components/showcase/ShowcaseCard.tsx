"use client"

import Image from "next/image"
import { useCallback } from "react"
import { useTranslations } from "next-intl"
import { format } from "date-fns"
import { Eye, Heart, Calendar, ExternalLink, GitBranch, Star } from "lucide-react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"
import { type ShowcaseListItem } from "@/lib/api/queries/showcase"

interface ShowcaseCardProps {
  showcase: ShowcaseListItem
}

function parseScreenshots(json: string | null): string[] {
  if (!json) return []
  try {
    return JSON.parse(json) as string[]
  } catch {
    return []
  }
}

export function ShowcaseCard({ showcase }: Readonly<ShowcaseCardProps>) {
  const t = useTranslations()
  const screenshots = parseScreenshots(showcase.screenshotsJson)
  const firstScreenshot = screenshots[0]

  const handleDemoClick = useCallback((e: React.MouseEvent) => {
    e.stopPropagation()
  }, [])

  const handleRepoClick = useCallback((e: React.MouseEvent) => {
    e.stopPropagation()
  }, [])

  return (
    <Link href={`/showcase/${showcase.projectId}`} className="block">
      <Card className="group overflow-hidden transition-all hover:shadow-xl hover:scale-[1.02] h-full">
        {/* Thumbnail / Gradient */}
        <div className="h-48 relative overflow-hidden bg-gradient-to-br from-primary/10 to-primary/5">
          {firstScreenshot ? (
            <Image
              fill
              src={firstScreenshot}
              alt={showcase.projectTitle}
              className="object-cover group-hover:scale-105 transition-transform duration-300"
            />
          ) : (
            <div className="absolute inset-0 bg-grid-pattern opacity-5" />
          )}
          {showcase.featured && (
            <div className="absolute top-3 left-3">
              <Badge className="bg-yellow-500/90 text-white border-0 gap-1">
                <Star className="h-3 w-3 fill-current" />
                Featured
              </Badge>
            </div>
          )}
        </div>

        <CardHeader className="pb-2">
          <CardTitle className="group-hover:text-primary transition-colors line-clamp-1 text-lg">
            {showcase.projectTitle}
          </CardTitle>
          {showcase.summary && (
            <p className="text-sm text-muted-foreground line-clamp-2">{showcase.summary}</p>
          )}
        </CardHeader>

        <CardContent className="space-y-3">
          {/* Stats Row */}
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <div className="flex items-center gap-1">
              <Eye className="h-3.5 w-3.5" />
              <span>{showcase.viewsCount}</span>
            </div>
            <div className="flex items-center gap-1">
              <Heart className="h-3.5 w-3.5" />
              <span>{showcase.likesCount}</span>
            </div>
            <div className="flex items-center gap-1 ml-auto">
              <Calendar className="h-3.5 w-3.5" />
              <span>{format(new Date(showcase.publishedAt), "MMM d, yyyy")}</span>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex gap-2 pt-1">
            <Button variant="outline" size="sm" className="flex-1">
              <Eye className="mr-2 h-4 w-4" />
              {t("showcase.viewDetails")}
            </Button>
            {showcase.repositoryUrl && (
              <Button variant="ghost" size="sm" className="px-3" asChild>
                <a href={showcase.repositoryUrl} target="_blank" rel="noopener noreferrer" onClick={handleRepoClick}>
                  <GitBranch className="h-4 w-4" />
                </a>
              </Button>
            )}
            {showcase.demoUrl && (
              <Button variant="ghost" size="sm" className="px-3" asChild>
                <a href={showcase.demoUrl} target="_blank" rel="noopener noreferrer" onClick={handleDemoClick}>
                  <ExternalLink className="h-4 w-4" />
                </a>
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
