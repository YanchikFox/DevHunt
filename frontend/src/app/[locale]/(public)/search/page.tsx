"use client"

import { useSearchParams } from "next/navigation"
import { useTranslations } from "next-intl"
import { useQuery } from "@tanstack/react-query"
import { apiClient } from "@/lib/api/client"
import { useState, useCallback } from "react"
import { useRouter, Link } from "@/i18n/routing";
import { Search, FolderGit2, User, Loader2, SearchX } from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

interface SearchProject {
  id: string
  title: string
  description: string | null
  status: string
  ownerName?: string
  techStack?: string[]
  teamSize?: number
  rating?: number
}

interface SearchUser {
  id: string
  fullName: string | null
  email?: string
  avatarUrl: string | null
  role: string | null
  bio: string | null
  skills?: string[]
  rating?: number
}

function useSearchResults(query: string, tab: string) {
  return useQuery({
    queryKey: ["search-page", query, tab],
    queryFn: async () => {
      if (!query) return { projects: [], users: [], projectsTotal: 0, usersTotal: 0 }

      const [projectsRes, usersRes] = await Promise.all([
        tab !== "users"
          ? apiClient.get("/projects", {
              params: { query, pageSize: 20, excludeDrafts: true },
            }).catch(() => ({ data: { data: [], pagination: { total: 0 } } }))
          : Promise.resolve({ data: { data: [], pagination: { total: 0 } } }),
        tab !== "projects"
          ? apiClient.get("/users/search", {
              params: { query, pageSize: 20 },
            }).catch(() => ({ data: { data: [], pagination: { total: 0 } } }))
          : Promise.resolve({ data: { data: [], pagination: { total: 0 } } }),
      ])

      return {
        projects: (projectsRes.data?.data || []) as SearchProject[],
        users: (usersRes.data?.data || []) as SearchUser[],
        projectsTotal: projectsRes.data?.pagination?.total || 0,
        usersTotal: usersRes.data?.pagination?.total || 0,
      }
    },
    enabled: !!query,
    staleTime: 30_000,
  })
}

export default function SearchPage() {
  const t = useTranslations()
  const searchParams = useSearchParams()
  const router = useRouter()
  const initialQuery = searchParams.get("q") || ""
  const [inputValue, setInputValue] = useState(initialQuery)
  const [tab, setTab] = useState("all")

  const { data, isLoading } = useSearchResults(initialQuery, tab)

  const handleSearch = useCallback(() => {
    if (inputValue.trim()) {
      router.push(`/search?q=${encodeURIComponent(inputValue.trim())}`)
    }
  }, [inputValue, router])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter") handleSearch()
  }, [handleSearch])

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setInputValue(e.target.value)
  }, [])

  const handleTabChange = useCallback((value: string) => {
    setTab(value)
  }, [])

  const totalResults = (data?.projectsTotal || 0) + (data?.usersTotal || 0)
  const showProjects = tab === "all" || tab === "projects"
  const showUsers = tab === "all" || tab === "users"

  return (
    <div className="container mx-auto max-w-4xl px-4 py-8 sm:py-12">
      {/* Search input */}
      <div className="relative mb-8 group">
        <Search
          className="absolute left-4 top-1/2 -translate-y-1/2 text-muted-foreground group-focus-within:text-primary transition-colors"
          size={20}
        />
        {isLoading && (
          <Loader2 className="absolute right-28 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground animate-spin" />
        )}
        <input
          type="text"
          value={inputValue}
          onChange={handleInputChange}
          onKeyDown={handleKeyDown}
          placeholder={t("common.search")}
          autoFocus
          className="w-full pl-12 pr-28 py-3.5 bg-muted/40 border border-border/50 rounded-xl text-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary/50 focus:bg-background transition-all duration-200"
        />
        <Button
          onClick={handleSearch}
          size="sm"
          className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-lg"
        >
          {t("common.search")}
        </Button>
      </div>

      {initialQuery && (
        <div className="space-y-6">
          {/* Tab filter + result count */}
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-1 p-1 bg-muted/50 rounded-xl w-fit">
              <FilterTab
                label={t("common.all") || "All"}
                count={totalResults}
                isActive={tab === "all"}
                onClick={() => handleTabChange("all")}
              />
              <FilterTab
                label={t("navigation.openProjects") || "Projects"}
                count={data?.projectsTotal || 0}
                isActive={tab === "projects"}
                icon={<FolderGit2 className="h-3.5 w-3.5" />}
                onClick={() => handleTabChange("projects")}
              />
              <FilterTab
                label={t("common.users") || "Users"}
                count={data?.usersTotal || 0}
                isActive={tab === "users"}
                icon={<User className="h-3.5 w-3.5" />}
                onClick={() => handleTabChange("users")}
              />
            </div>

            {!isLoading && data && (
              <p className="text-sm text-muted-foreground">
                {totalResults} {t("common.results") || "results"} for &quot;<span className="font-medium text-foreground">{initialQuery}</span>&quot;
              </p>
            )}
          </div>

          {/* Results */}
          <div className="space-y-3">
            {showProjects && data?.projects && data.projects.length > 0 && (
              <section>
                {tab === "all" && data.users && data.users.length > 0 && (
                  <div className="flex items-center gap-2 mb-3">
                    <FolderGit2 className="h-4 w-4 text-muted-foreground" />
                    <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wider">
                      {t("navigation.openProjects") || "Projects"}
                    </h2>
                  </div>
                )}
                <div className="space-y-2">
                  {data.projects.map((project) => (
                    <ProjectCard key={project.id} project={project} />
                  ))}
                </div>
              </section>
            )}

            {showUsers && data?.users && data.users.length > 0 && (
              <section className={showProjects && data?.projects?.length ? "mt-6" : ""}>
                {tab === "all" && data.projects && data.projects.length > 0 && (
                  <div className="flex items-center gap-2 mb-3">
                    <User className="h-4 w-4 text-muted-foreground" />
                    <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wider">
                      {t("common.users") || "Users"}
                    </h2>
                  </div>
                )}
                <div className="space-y-2">
                  {data.users.map((user) => (
                    <UserCard key={user.id} user={user} />
                  ))}
                </div>
              </section>
            )}

            {!isLoading && totalResults === 0 && <EmptyState query={initialQuery} />}
          </div>
        </div>
      )}

      {/* Initial state — no query */}
      {!initialQuery && (
        <div className="text-center py-16">
          <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-muted/50 mb-4">
            <Search className="h-8 w-8 text-muted-foreground/50" />
          </div>
          <p className="text-lg font-medium text-muted-foreground">
            {t("common.search")}
          </p>
          <p className="text-sm text-muted-foreground/70 mt-1">
            Find projects, people, and community resources
          </p>
        </div>
      )}
    </div>
  )
}

function FilterTab({ label, count, isActive, icon, onClick }: Readonly<{
  label: string
  count: number
  isActive: boolean
  icon?: React.ReactNode
  onClick: () => void
}>) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-sm font-medium transition-all duration-200",
        isActive
          ? "bg-background text-foreground shadow-sm"
          : "text-muted-foreground hover:text-foreground hover:bg-background/50"
      )}
    >
      {icon}
      {label}
      <span className={cn(
        "text-xs tabular-nums ml-0.5",
        isActive ? "text-muted-foreground" : "text-muted-foreground/60"
      )}>
        {count}
      </span>
    </button>
  )
}

function ProjectCard({ project }: Readonly<{ project: SearchProject }>) {
  return (
    <Link href={`/projects/${project.id}`}>
      <div className="flex items-start gap-4 p-4 rounded-xl border border-border/50 bg-card hover:bg-accent/50 hover:border-border transition-all duration-200 cursor-pointer group">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary group-hover:bg-primary/15 transition-colors">
          <FolderGit2 className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="font-semibold truncate group-hover:text-primary transition-colors">{project.title}</h3>
            <Badge variant="secondary" className="text-xs shrink-0 py-0">
              {project.status}
            </Badge>
          </div>
          {project.description && (
            <p className="text-sm text-muted-foreground line-clamp-2 mb-2">{project.description}</p>
          )}
          {project.techStack && project.techStack.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {project.techStack.slice(0, 5).map((tech) => (
                <Badge key={tech} variant="outline" className="text-xs bg-muted/50 border-border/60">
                  {tech}
                </Badge>
              ))}
            </div>
          )}
        </div>
      </div>
    </Link>
  )
}

function UserCard({ user }: Readonly<{ user: SearchUser }>) {
  const displayName = user.fullName || user.email || "User"
  return (
    <Link href={`/community/users/${user.id}`}>
      <div className="flex items-center gap-4 p-4 rounded-xl border border-border/50 bg-card hover:bg-accent/50 hover:border-border transition-all duration-200 cursor-pointer group">
        <Avatar className="h-10 w-10 shrink-0 border border-border/50 group-hover:border-primary/30 transition-colors">
          <AvatarImage src={user.avatarUrl || undefined} alt={displayName} />
          <AvatarFallback className="bg-primary/10 text-primary font-medium">
            {displayName.charAt(0).toUpperCase()}
          </AvatarFallback>
        </Avatar>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 mb-0.5">
            <h3 className="font-semibold truncate group-hover:text-primary transition-colors">{displayName}</h3>
            {user.role && (
              <Badge variant="secondary" className="text-xs shrink-0 py-0">
                {user.role}
              </Badge>
            )}
          </div>
          {user.bio && (
            <p className="text-sm text-muted-foreground line-clamp-1 mb-1.5">{user.bio}</p>
          )}
          {user.skills && user.skills.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {user.skills.slice(0, 4).map((skill) => (
                <Badge key={skill} variant="outline" className="text-xs bg-muted/50 border-border/60">
                  {skill}
                </Badge>
              ))}
            </div>
          )}
        </div>
      </div>
    </Link>
  )
}

function EmptyState({ query }: Readonly<{ query: string }>) {
  const t = useTranslations()
  return (
    <div className="text-center py-16">
      <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-muted/50 mb-4">
        <SearchX className="h-8 w-8 text-muted-foreground/40" />
      </div>
      <p className="font-medium text-foreground mb-1">
        {t("common.noResults") || "No results found"}
      </p>
      <p className="text-sm text-muted-foreground">
        No matches for &quot;<span className="font-medium">{query}</span>&quot;. Try different keywords.
      </p>
    </div>
  )
}
