import { Metadata } from "next"
import ShowcasePageClient from "./showcase-page-client"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"

// Generate metadata for SEO (FRONTEND_UX_IMPROVEMENT_PLAN 2.1)
export async function generateMetadata({
  params,
}: {
  params: { locale: string }
}): Promise<Metadata> {
  const canonical = `/${params.locale}/showcase`

  return {
    title: "Project Showcase - DevHunt",
    description:
      "Discover amazing projects built by talented developers. Get inspired and showcase your own work.",
    alternates: {
      canonical,
    },
    openGraph: {
      title: "Project Showcase - DevHunt",
      description: "Discover amazing projects built by talented developers",
      type: "website",
      url: canonical,
      images: [{ url: "/opengraph-image" }],
    },
    twitter: {
      card: "summary_large_image",
      title: "Project Showcase - DevHunt",
      description: "Discover amazing projects built by talented developers",
      images: ["/opengraph-image"],
    },
  }
}

export default async function ShowcasePage() {
  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1 container mx-auto px-4 py-8 space-y-8">
        {/* Hero Section */}
        <div className="text-center space-y-4">
          <h1 className="text-5xl font-bold tracking-tight">
            Project{" "}
            <span className="bg-gradient-to-r from-primary to-primary/60 bg-clip-text text-transparent">
              Showcase
            </span>
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto">
            Discover amazing projects built by talented developers. Get inspired and showcase your
            own work.
          </p>
        </div>
        <ShowcasePageClient />
      </main>
      <Footer />
    </div>
  )
}
