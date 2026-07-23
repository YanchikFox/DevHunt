import { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"
import { LandingHero } from "@/components/landing/LandingHero"
import { LandingAISection } from "@/components/landing/LandingAISection"
import { LandingFeatures } from "@/components/landing/LandingFeatures"
import { LandingStories } from "@/components/landing/LandingStories"
import { LandingStats } from "@/components/landing/LandingStats"
import { LandingCTA } from "@/components/landing/LandingCTA"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations()
  return {
    title: "DevHunt - Developer Collaboration Platform",
    description: t("homepage.heroDescription"),
    openGraph: {
      title: "DevHunt - Developer Collaboration Platform",
      description: t("homepage.heroDescription"),
      type: "website",
    },
  }
}

export default function HomePage() {
  return (
    <div className="flex min-h-screen flex-col bg-background selection:bg-primary/10 selection:text-primary">
      <Header />
      <main className="flex-1">
        <LandingHero />
        <LandingAISection />
        <LandingFeatures />
        <LandingStories />
        <LandingStats />
        <LandingCTA />
      </main>
      <Footer />
    </div>
  )
}
