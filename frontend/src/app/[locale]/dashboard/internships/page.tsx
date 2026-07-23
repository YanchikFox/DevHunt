"use client"

import { useState, useCallback } from "react"
import { useTranslations } from "next-intl"
import { useInternshipsList } from "@/lib/api/queries/internships"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Filter, Briefcase, MapPin } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton"

import { InternshipCard } from "./_components/InternshipCard"

const createApplicationSchema = (t: (key: string) => string) => z.object({
  message: z.string().min(20, t("internships.messageMinLength")),
})

type ApplicationForm = z.infer<ReturnType<typeof createApplicationSchema>>

const ALL_TYPES_OPTION = "all"
const ALL_LOCATIONS_OPTION = "all"

export default function InternshipsPage() {
  const t = useTranslations()
  const [type, setType] = useState<string>("")
  const [isRemote, setIsRemote] = useState<boolean | undefined>(undefined)
  const { data: internships, isLoading } = useInternshipsList({
    type: type || undefined,
    isRemote,
  })
  const { toast } = useToast()

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
  } = useForm<ApplicationForm>({
    resolver: zodResolver(createApplicationSchema(t)),
  })

  const onSubmit = useCallback(async () => {
    try {
      toast({
        title: t("common.success"),
        description: t("internships.applicationSubmitted"),
      })
      reset()
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("internships.applicationSubmitFailed"),
        variant: "destructive",
      })
    }
  }, [toast, t, reset])

  const getTypeLabel = useCallback((type: string) => {
    const labels: Record<string, string> = {
      challenge: t("internships.challenge"),
      hackathon: t("internships.hackathon"),
      internship: t("internships.internship"),
      course: t("internships.course"),
    }
    return labels[type] || type
  }, [t])

  const getTypeColor = useCallback((type: string) => {
    const colors: Record<string, string> = {
      challenge: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400 border-blue-200 dark:border-blue-800",
      hackathon: "bg-purple-100 text-purple-700 dark:bg-purple-900/20 dark:text-purple-400 border-purple-200 dark:border-purple-800",
      internship: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400 border-green-200 dark:border-green-800",
      course: "bg-orange-100 text-orange-700 dark:bg-orange-900/20 dark:text-orange-400 border-orange-200 dark:border-orange-800",
    }
    return colors[type] || "bg-muted text-muted-foreground border-border"
  }, [])

  const typeSelectValue = type || ALL_TYPES_OPTION
  let locationSelectValue = ALL_LOCATIONS_OPTION
  if (isRemote !== undefined) {
    locationSelectValue = isRemote ? "remote" : "onSite"
  }

  const handleTypeChange = useCallback((value: string) => {
    setType(value === ALL_TYPES_OPTION ? "" : value)
  }, [])

  const handleLocationChange = useCallback((value: string) => {
    if (value === ALL_LOCATIONS_OPTION) {
      setIsRemote(undefined)
      return
    }
    setIsRemote(value === "remote")
  }, [])

  const handleClearFilters = useCallback(() => { setType(""); setIsRemote(undefined) }, [])

  return (
    <div className="space-y-6 fade-in">
      <div>
        <span className="caption mb-1.5 block">[Opportunities]</span>
        <h1 className="font-serif text-[32px] font-normal tracking-[-0.5px] leading-none text-foreground">
          {t("internships.title")}
        </h1>
        <p className="text-[14px] text-muted-foreground mt-2">
          {t("internships.discoverOpportunities")}
        </p>
      </div>

      {/* FILTERS */}
      <div className="rounded-[14px] border border-border bg-card p-4">
        <div className="flex flex-col md:flex-row gap-3 items-center">
          <div className="w-full md:w-auto flex-1 md:flex-none md:min-w-[220px]">
            <span className="caption mb-1 block">{t("teams.programType")}</span>
            <Select value={typeSelectValue} onValueChange={handleTypeChange}>
              <SelectTrigger className="w-full h-9 text-[12px] rounded-[10px]">
                <div className="flex items-center gap-2">
                  <Filter className="h-4 w-4 text-muted-foreground" />
                <SelectValue placeholder={t("teams.programType")} />
                </div>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_TYPES_OPTION}>{t("teams.allTypes")}</SelectItem>
                <SelectItem value="challenge">{t("internships.challenge")}</SelectItem>
                <SelectItem value="hackathon">{t("internships.hackathon")}</SelectItem>
                <SelectItem value="internship">{t("internships.internship")}</SelectItem>
                <SelectItem value="course">{t("internships.course")}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="w-full md:w-auto flex-1 md:flex-none md:min-w-[220px]">
            <span className="caption mb-1 block">{t("teams.location")}</span>
            <Select value={locationSelectValue} onValueChange={handleLocationChange}>
              <SelectTrigger className="w-full h-9 text-[12px] rounded-[10px]">
                <div className="flex items-center gap-2">
                  <MapPin className="h-4 w-4 text-muted-foreground" />
                  <SelectValue placeholder={t("teams.location")} />
                </div>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_LOCATIONS_OPTION}>{t("teams.allLocations")}</SelectItem>
                <SelectItem value="remote">{t("internships.remote")}</SelectItem>
                <SelectItem value="onSite">{t("internships.onSite")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* GRID */}
      {isLoading ? (
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
            <Card key={i} className="border border-border/50 bg-card/30">
              <CardHeader className="p-6">
                <Skeleton className="h-6 w-3/4 mb-2" />
                <Skeleton className="h-4 w-1/2" />
              </CardHeader>
              <CardContent className="px-6 pb-6">
                <div className="flex gap-2 mb-4">
                  <Skeleton className="h-5 w-16" />
                  <Skeleton className="h-5 w-16" />
                </div>
                <Skeleton className="h-24 w-full rounded-md" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : internships && internships.length > 0 ? (
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {internships.map((internship) => (
            <InternshipCard
              key={internship.id}
              internship={internship}
              getTypeColor={getTypeColor}
              getTypeLabel={getTypeLabel}
              onSubmit={handleSubmit(onSubmit)}
              register={register}
              errors={errors}
            />
          ))}
        </div>
      ) : (
        <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
          <Briefcase className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
          <h3 className="text-[15px] font-semibold text-foreground mb-1">
            {t("internships.noOpportunitiesFound")}
          </h3>
          <p className="text-[13px] text-muted-foreground max-w-md mx-auto mb-4">
            {type || isRemote !== undefined
              ? t("internships.tryAdjustingFilters")
              : t("internships.noInternshipsAvailable")}
          </p>
          <button
            onClick={handleClearFilters}
            className="text-[12px] text-primary hover:underline font-medium"
          >
            Clear filters
          </button>
        </div>
      )}
    </div>
  )
}
