"use client"

import { useTranslations } from "next-intl"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Link } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Star } from "lucide-react"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"
import { SuggestedUsersPanel } from "@/components/profile/SuggestedUsersPanel"
import { GlobalFeed } from "@/components/profile/GlobalFeed"

const mockDevelopers = [
  {
    id: "1",
    name: "Alex Johnson",
    role: "Full Stack Developer",
    technologies: ["React", "Node.js", "TypeScript"],
    projects: 12,
    reputation: 245,
    avatar: "AJ",
  },
  {
    id: "2",
    name: "Sarah Chen",
    role: "Backend Engineer",
    technologies: [".NET", "PostgreSQL", "Docker"],
    projects: 8,
    reputation: 189,
    avatar: "SC",
  },
  {
    id: "3",
    name: "Mike Wilson",
    role: "DevOps Engineer",
    technologies: ["Kubernetes", "AWS", "Terraform"],
    projects: 15,
    reputation: 312,
    avatar: "MW",
  },
  {
    id: "4",
    name: "Emma Davis",
    role: "ML Engineer",
    technologies: ["Python", "TensorFlow", "PyTorch"],
    projects: 9,
    reputation: 201,
    avatar: "ED",
  },
]

export default function CommunityPage() {
  const t = useTranslations()

  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1 container mx-auto px-4 py-8 space-y-8">
        {/* Hero Section */}
        <div className="text-center space-y-4">
          <h1 className="text-5xl font-bold tracking-tight">
            {t("community.heroTitle")}{" "}
            <span className="bg-gradient-to-r from-primary to-primary/60 bg-clip-text text-transparent">
              {t("community.heroHighlight")}
            </span>
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto">
            {t("community.heroDescription")}
          </p>
        </div>

        {/* Stats */}
        <div className="grid gap-8 sm:grid-cols-3">
          <Card className="text-center">
            <CardContent className="pt-6">
              <div className="text-4xl font-bold text-primary mb-2">1,200+</div>
              <div className="text-sm text-muted-foreground">{t("community.activeDevelopers")}</div>
            </CardContent>
          </Card>
          <Card className="text-center">
            <CardContent className="pt-6">
              <div className="text-4xl font-bold text-primary mb-2">500+</div>
              <div className="text-sm text-muted-foreground">{t("community.activeProjects")}</div>
            </CardContent>
          </Card>
          <Card className="text-center">
            <CardContent className="pt-6">
              <div className="text-4xl font-bold text-primary mb-2">50+</div>
              <div className="text-sm text-muted-foreground">{t("community.partners")}</div>
            </CardContent>
          </Card>
        </div>

        {/* Top Developers */}
        <div className="space-y-4">
          <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
            <GlobalFeed />
            <SuggestedUsersPanel />
          </div>

          {/* Top Developers */}
          <div className="flex items-center justify-between">
            <h2 className="text-2xl font-bold">{t("community.topDevelopers")}</h2>
            <Link href="/login">
              <Button variant="outline">{t("community.viewAll")}</Button>
            </Link>
          </div>
          <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
            {mockDevelopers.map((dev) => (
              <Card
                key={dev.id}
                className="group overflow-hidden transition-all hover:shadow-xl hover:scale-[1.02]"
              >
                <div className="h-48 bg-gradient-to-br from-blue-500/10 to-purple-500/10 relative overflow-hidden">
                  <div className="absolute inset-0 bg-grid-pattern opacity-5" />
                  <div className="absolute top-4 left-4">
                    <div className="h-16 w-16 rounded-full bg-gradient-to-br from-blue-600 to-purple-600 text-white flex items-center justify-center text-2xl font-bold">
                      {dev.avatar}
                    </div>
                  </div>
                </div>
                <CardHeader>
                  <CardTitle className="text-lg group-hover:text-primary transition-colors">
                    {dev.name}
                  </CardTitle>
                  <CardDescription>{dev.role}</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="flex flex-wrap gap-2">
                    {dev.technologies.map((tech) => (
                      <Badge key={tech} variant="outline" className="text-xs">
                        {tech}
                      </Badge>
                    ))}
                  </div>

                  <div className="space-y-2 text-sm pt-2 border-t">
                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">{t("community.projects")}</span>
                      <span className="font-semibold">{dev.projects}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground flex items-center gap-1">
                        <Star className="h-3 w-3" />
                        {t("community.reputation")}
                      </span>
                      <span className="font-semibold">{dev.reputation}</span>
                    </div>
                  </div>

                  <Link href={`/users/${dev.id}`}>
                    <Button variant="outline" className="w-full">
                      {t("community.viewProfile")}
                    </Button>
                  </Link>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </main>
      <Footer />
    </div>
  )
}
