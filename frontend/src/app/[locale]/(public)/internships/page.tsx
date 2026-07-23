"use client"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Link } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Briefcase, MapPin, Clock, ArrowRight } from "lucide-react"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"

const mockInternships = [
  {
    id: "1",
    title: "Frontend Developer Intern",
    company: "TechCorp",
    location: "Remote",
    duration: "3 months",
    description: "Build modern web applications using React and TypeScript",
    technologies: ["React", "TypeScript", "Next.js"],
    paid: true,
    applicants: 45,
  },
  {
    id: "2",
    title: "Backend Developer Intern",
    company: "DevHunt",
    location: "Moscow, Russia",
    duration: "6 months",
    description: "Develop scalable APIs and microservices with .NET",
    technologies: [".NET", "PostgreSQL", "Docker"],
    paid: true,
    applicants: 32,
  },
  {
    id: "3",
    title: "ML Engineer Intern",
    company: "AI Labs",
    location: "Remote",
    duration: "4 months",
    description: "Work on machine learning models and data pipelines",
    technologies: ["Python", "TensorFlow", "PyTorch"],
    paid: false,
    applicants: 67,
  },
]

export default function InternshipsPage() {
  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1 container mx-auto px-4 py-8 space-y-8">
        {/* Hero Section */}
        <div className="text-center space-y-4">
          <h1 className="text-5xl font-bold tracking-tight">
            <span className="bg-gradient-to-r from-primary to-primary/60 bg-clip-text text-transparent">
              Internships
            </span>
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto">
            Find the perfect internship to kickstart your tech career
          </p>
        </div>

        {/* Internships Grid */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {mockInternships.map((internship) => (
            <Card
              key={internship.id}
              className="group overflow-hidden transition-all hover:shadow-xl hover:scale-[1.02]"
            >
              <div className="h-48 bg-gradient-to-br from-purple-500/10 to-pink-500/10 relative overflow-hidden">
                <div className="absolute inset-0 bg-grid-pattern opacity-5" />
                <div className="absolute top-4 right-4">
                  {internship.paid ? (
                    <Badge className="bg-green-500">Paid</Badge>
                  ) : (
                    <Badge variant="outline">Unpaid</Badge>
                  )}
                </div>
              </div>
              <CardHeader>
                <CardTitle className="group-hover:text-primary transition-colors">
                  {internship.title}
                </CardTitle>
                <CardDescription className="font-semibold text-foreground">
                  {internship.company}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <p className="text-sm text-muted-foreground line-clamp-2">
                  {internship.description}
                </p>

                <div className="flex flex-wrap gap-2">
                  {internship.technologies.map((tech) => (
                    <Badge key={tech} variant="outline" className="text-xs">
                      {tech}
                    </Badge>
                  ))}
                </div>

                <div className="space-y-2 text-sm text-muted-foreground pt-2 border-t">
                  <div className="flex items-center gap-2">
                    <MapPin className="h-4 w-4" />
                    <span>{internship.location}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Clock className="h-4 w-4" />
                    <span>{internship.duration}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Briefcase className="h-4 w-4" />
                    <span>{internship.applicants} applicants</span>
                  </div>
                </div>

                <div className="pt-2">
                  <Link href="/login">
                    <Button className="w-full group">
                      Apply Now
                      <ArrowRight className="ml-2 h-4 w-4 transition-transform group-hover:translate-x-1" />
                    </Button>
                  </Link>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </main>
      <Footer />
    </div>
  )
}
