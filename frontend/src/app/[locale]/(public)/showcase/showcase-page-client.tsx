"use client"

import { useState, useCallback } from "react"
import { useTranslations } from "next-intl"
import { useShowcaseList, type ShowcaseListItem } from "@/lib/api/queries/showcase"
import { ShowcaseCard } from "@/components/showcase/ShowcaseCard"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Search, Filter, Code2, Star } from "lucide-react"
import { Skeleton } from "@/components/ui/skeleton"

export default function ShowcasePageClient() {
  const t = useTranslations()

  const [searchQuery, setSearchQuery] = useState("")
  const [filterMode, setFilterMode] = useState<string>("all")

  const { data: showcases, isLoading } = useShowcaseList({
    featuredOnly: filterMode === "featured",
    take: 50,
  })

  const handleSearchQueryChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
  }, [])

  // Client-side search filtering (API does not support text search on showcase list)
  const filteredShowcases = showcases?.filter((s) => {
    if (!searchQuery) return true
    const query = searchQuery.toLowerCase()
    return (
      s.projectTitle.toLowerCase().includes(query) ||
      (s.summary?.toLowerCase().includes(query) ?? false)
    )
  })

  return (
    <>
      {/* Search and Filters */}
      <div className="flex flex-col gap-4 sm:flex-row">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder={t("showcase.searchPlaceholder")}
            value={searchQuery}
            onChange={handleSearchQueryChange}
            className="pl-10"
          />
        </div>
        <Select value={filterMode} onValueChange={setFilterMode}>
          <SelectTrigger className="w-full sm:w-[200px]">
            <Filter className="mr-2 h-4 w-4" />
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("showcase.allProjects")}</SelectItem>
            <SelectItem value="featured">
              <span className="flex items-center gap-1">
                <Star className="h-3.5 w-3.5" />
                {t("showcase.featuredOnly")}
              </span>
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Projects Grid */}
      <ShowcaseGrid
        isLoading={isLoading}
        showcases={filteredShowcases}
        searchQuery={searchQuery}
      />
    </>
  )
}

interface ShowcaseGridProps {
  isLoading: boolean
  showcases: ShowcaseListItem[] | undefined
  searchQuery: string
}

function ShowcaseGrid({ isLoading, showcases, searchQuery }: Readonly<ShowcaseGridProps>) {
  const t = useTranslations()

  if (isLoading) {
    return (
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <Card key={i} className="overflow-hidden">
            <Skeleton className="h-48 w-full" />
            <div className="p-4 space-y-3">
              <Skeleton className="h-6 w-3/4" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          </Card>
        ))}
      </div>
    )
  }

  if (showcases && showcases.length > 0) {
    return (
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        {showcases.map((showcase) => (
          <ShowcaseCard key={showcase.id} showcase={showcase} />
        ))}
      </div>
    )
  }

  return (
    <Card>
      <CardContent className="flex flex-col items-center justify-center py-12">
        <Code2 className="h-12 w-12 text-muted-foreground mb-4" />
        <h3 className="text-lg font-semibold mb-2">{t("showcase.noProjectsFound")}</h3>
        <p className="text-muted-foreground text-center">
          {searchQuery
            ? t("showcase.tryAdjustingSearch")
            : t("showcase.noProjectsYet")}
        </p>
      </CardContent>
    </Card>
  )
}
