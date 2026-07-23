"use client"

import { ArrowLeft } from "lucide-react"
import { Link } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { ProjectDetailContent } from "./_components/ProjectDetailContent"
import { useProjectDetail, type ProjectDetailState } from "./_hooks/useProjectDetail"

interface ProjectDetailPageProps {
  params: Promise<{ id: string }> | { id: string }
}

function ProjectDetailLoading() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-10 w-32" />
      <Skeleton className="h-12 w-full" />
      <Skeleton className="h-96 w-full" />
    </div>
  )
}

function ProjectDetailError({ detail }: { readonly detail: ProjectDetailState }) {
  const { mounted, projectError, tasksError, teamError, t } = detail

  return (
    <div className="space-y-6">
      {mounted ? (
        <Link href="/dashboard/projects">
          <Button variant="ghost" className="gap-2">
            <ArrowLeft className="h-4 w-4" />
            {t("projects.backToProjects")}
          </Button>
        </Link>
      ) : (
        <Button variant="ghost" className="gap-2" disabled>
          <ArrowLeft className="h-4 w-4" />
          {t("projects.backToProjects")}
        </Button>
      )}
      <Card>
        <CardContent className="p-8 text-center">
          <p className="text-muted-foreground">
            {projectError instanceof Error ? projectError.message : t("projects.projectNotFound")}
          </p>
          {(tasksError || teamError) && (
            <p className="text-sm text-muted-foreground mt-2">
              {t("projects.partialLoadError")}
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

export default function ProjectDetailPage({ params }: Readonly<ProjectDetailPageProps>) {
  const detail = useProjectDetail(params)

  if (!detail.hasProjectId || detail.projectLoading) {
    return <ProjectDetailLoading />
  }

  if (detail.projectError || !detail.project) {
    return <ProjectDetailError detail={detail} />
  }

  return <ProjectDetailContent detail={detail} />
}
