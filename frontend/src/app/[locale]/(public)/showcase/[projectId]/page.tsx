import { Metadata } from "next"
import { notFound } from "next/navigation"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"
import { ShowcaseContent } from "@/components/showcase/ShowcaseContent"
import { ShowcaseProject } from "@/lib/api/queries/showcase"

async function getShowcase(projectId: string): Promise<ShowcaseProject | null> {
  try {
    const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://core-api:8080"
    const response = await fetch(`${API_URL}/api/projects/${projectId}/showcase`, {
      next: { revalidate: 60 }, // Cache for 1 minute
    })

    if (!response.ok) {
      return null
    }

    return response.json()
  } catch (error) {
    console.error("Failed to fetch showcase:", error)
    return null
  }
}

export async function generateMetadata({
  params,
}: {
  params: { locale: string; projectId: string }
}): Promise<Metadata> {
  const showcase = await getShowcase(params.projectId)

  const canonical = `/${params.locale}/showcase/${params.projectId}`

  if (!showcase) {
    return {
      title: "Project Not Found - DevHunt",
      alternates: {
        canonical,
      },
    }
  }

  const ogImages = showcase.screenshots?.[0]
    ? [showcase.screenshots[0]]
    : ["/opengraph-image"]

  return {
    title: `${showcase.project.title} - DevHunt Showcase`,
    description: showcase.summary || showcase.project.description,
    alternates: {
      canonical,
    },
    openGraph: {
      title: showcase.project.title,
      description: showcase.summary || showcase.project.description,
      type: "website",
      url: canonical,
      images: ogImages,
    },
    twitter: {
      card: "summary_large_image",
      title: showcase.project.title,
      description: showcase.summary || showcase.project.description,
      images: ogImages,
    },
  }
}

export default async function ProjectShowcasePage({ params }: { params: { projectId: string } }) {
  const showcase = await getShowcase(params.projectId)

  if (!showcase) {
    notFound()
  }

  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1 container mx-auto px-4 py-8">
        <ShowcaseContent showcase={showcase} />
      </main>
      <Footer />
    </div>
  )
}
