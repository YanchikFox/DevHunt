"use client"

import { useState, useRef, useCallback, useEffect, useMemo } from "react"
import { useTranslations } from "next-intl"
import { useRouter, Link } from "@/i18n/routing";
import { useQuery } from "@tanstack/react-query"
import { apiClient } from "@/lib/api/client"
import { Search, FolderGit2, User, Loader2, ArrowRight } from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { cn } from "@/lib/utils"

interface SearchProject {
  id: string
  title: string
  status: string
  ownerName?: string
  avatarUrl?: string
  techStack?: string[]
}

interface SearchUser {
  id: string
  fullName: string | null
  email?: string
  avatarUrl: string | null
  role: string | null
  bio: string | null
}

interface SearchResults {
  projects: SearchProject[]
  users: SearchUser[]
}

function useGlobalSearch(query: string) {
  return useQuery({
    queryKey: ["global-search", query],
    queryFn: async (): Promise<SearchResults> => {
      if (!query || query.length < 2) return { projects: [], users: [] }

      const [projectsRes, usersRes] = await Promise.all([
        apiClient.get("/projects", {
          params: { query, pageSize: 5, excludeDrafts: true },
        }).catch(() => ({ data: { data: [] } })),
        apiClient.get("/users/search", {
          params: { query, pageSize: 5 },
        }).catch(() => ({ data: { data: [] } })),
      ])

      return {
        projects: (projectsRes.data?.data || []).map((p: Record<string, unknown>) => ({
          id: p.id as string,
          title: p.title as string,
          status: p.status as string,
          ownerName: p.ownerName as string | undefined,
          avatarUrl: p.avatarUrl as string | undefined,
          techStack: p.techStack as string[] | undefined,
        })),
        users: (usersRes.data?.data || []).map((u: Record<string, unknown>) => ({
          id: u.id as string,
          fullName: u.fullName as string | null,
          email: u.email as string | undefined,
          avatarUrl: u.avatarUrl as string | null,
          role: u.role as string | null,
          bio: u.bio as string | null,
        })),
      }
    },
    enabled: query.length >= 2,
    staleTime: 30_000,
    placeholderData: (prev) => prev,
  })
}

function SearchDropdown({ data, hasResults, isFetching, selectedIndex, onClose, onSubmit, t }: {
  data: SearchResults | undefined
  hasResults: boolean; isFetching: boolean; selectedIndex: number
  onClose: () => void; onSubmit: () => void; t: (key: string) => string
}) {
  return (
    <div id="search-listbox" role="listbox" aria-label={t("common.search")} className="absolute top-full left-0 right-0 mt-2 bg-popover border border-border rounded-xl shadow-lg overflow-hidden z-50 max-h-[min(400px,50vh)] overflow-y-auto">
      {!hasResults && !isFetching && (
        <div className="px-4 py-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</div>
      )}
      {data?.projects && data.projects.length > 0 && (
        <div>
          <div className="px-3 py-2 text-xs font-medium text-muted-foreground uppercase tracking-wider bg-muted/30">
            <FolderGit2 className="inline h-3 w-3 mr-1.5" />{t("navigation.openProjects")}
          </div>
          {data.projects.map((project, i) => (
            <Link key={project.id} id={`search-option-${i}`} role="option" aria-selected={selectedIndex === i}
              href={`/projects/${project.id}`} onClick={onClose}
              className={cn("flex items-center gap-3 px-3 py-2.5 hover:bg-muted/50 transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/50", selectedIndex === i && "bg-muted/50")}>
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><FolderGit2 className="h-4 w-4" /></div>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium truncate">{project.title}</p>
                <p className="text-xs text-muted-foreground truncate">{project.techStack?.slice(0, 3).join(", ")}</p>
              </div>
            </Link>
          ))}
        </div>
      )}
      {data?.users && data.users.length > 0 && (
        <div>
          <div className="px-3 py-2 text-xs font-medium text-muted-foreground uppercase tracking-wider bg-muted/30">
            <User className="inline h-3 w-3 mr-1.5" />{t("common.users")}
          </div>
          {data.users.map((user, i) => {
            const idx = (data?.projects?.length || 0) + i
            const displayName = user.fullName || user.email || "User"
            return (
              <Link key={user.id} id={`search-option-${idx}`} role="option" aria-selected={selectedIndex === idx}
                href={`/community/users/${user.id}`} onClick={onClose}
                className={cn("flex items-center gap-3 px-3 py-2.5 hover:bg-muted/50 transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/50", selectedIndex === idx && "bg-muted/50")}>
                <Avatar className="h-8 w-8 shrink-0">
                  <AvatarImage src={user.avatarUrl || undefined} alt={displayName} />
                  <AvatarFallback className="text-xs bg-primary/10 text-primary">{displayName.charAt(0).toUpperCase()}</AvatarFallback>
                </Avatar>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium truncate">{displayName}</p>
                  {user.bio && <p className="text-xs text-muted-foreground truncate">{user.bio}</p>}
                </div>
              </Link>
            )
          })}
        </div>
      )}
      {hasResults && (
        <button type="button" onClick={onSubmit}
          className="flex items-center justify-center gap-2 w-full px-3 py-2.5 text-sm text-primary hover:bg-muted/50 transition-colors border-t border-border/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/50">
          {t("common.viewAll")}<ArrowRight className="h-3.5 w-3.5" />
        </button>
      )}
    </div>
  )
}

export function GlobalSearch({ className }: { className?: string }) {
  const t = useTranslations()
  const router = useRouter()
  const [query, setQuery] = useState("")
  const [debouncedQuery, setDebouncedQuery] = useState("")
  const [isOpen, setIsOpen] = useState(false)
  const [selectedIndex, setSelectedIndex] = useState(-1)
  const containerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const { data, isFetching } = useGlobalSearch(debouncedQuery)
  const hasResults = (data?.projects?.length || 0) + (data?.users?.length || 0) > 0

  // Debounce input
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(query), 300)
    return () => clearTimeout(timer)
  }, [query])

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false)
      }
    }
    document.addEventListener("mousedown", handler)
    return () => document.removeEventListener("mousedown", handler)
  }, [])

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setQuery(e.target.value)
    setIsOpen(true)
    setSelectedIndex(-1)
  }, [])

  const handleSubmit = useCallback(() => {
    if (query.trim()) {
      router.push(`/search?q=${encodeURIComponent(query.trim())}`)
      setIsOpen(false)
      inputRef.current?.blur()
    }
  }, [query, router])

  const allItems = useMemo(() => [
    ...(data?.projects || []).map((p) => ({ type: "project" as const, ...p })),
    ...(data?.users || []).map((u) => ({ type: "user" as const, ...u })),
  ], [data?.projects, data?.users])

  const handleClose = useCallback(() => setIsOpen(false), [])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      setIsOpen(false)
      inputRef.current?.blur()
    } else if (e.key === "Enter") {
      if (selectedIndex >= 0 && selectedIndex < allItems.length) {
        const item = allItems[selectedIndex]
        const href = item.type === "project" ? `/projects/${item.id}` : `/community/users/${item.id}`
        router.push(href)
        setIsOpen(false)
      } else {
        handleSubmit()
      }
    } else if (e.key === "ArrowDown") {
      e.preventDefault()
      setSelectedIndex((prev) => Math.min(prev + 1, allItems.length - 1))
    } else if (e.key === "ArrowUp") {
      e.preventDefault()
      setSelectedIndex((prev) => Math.max(prev - 1, -1))
    }
  }, [allItems, selectedIndex, handleSubmit, router])

  const handleFocus = useCallback(() => {
    if (query.length >= 2) setIsOpen(true)
  }, [query])

  return (
    <div ref={containerRef} className={cn("relative", className)}>
      <div className="relative">
        <Search
          className="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none"
          size={14}
        />
        {isFetching ? (
          <Loader2
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground animate-spin"
            size={14}
          />
        ) : (
          <kbd className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 rounded border border-border bg-bg-subtle px-1.5 py-[1px] font-mono text-[10px] text-muted-foreground">
            ⌘K
          </kbd>
        )}
        <input
          ref={inputRef}
          type="text"
          
          aria-label={t("common.search")}
          aria-expanded={isOpen && query.length >= 2}
          aria-haspopup="listbox"
          aria-autocomplete="list"
          aria-controls="search-listbox"
          aria-activedescendant={selectedIndex >= 0 ? `search-option-${selectedIndex}` : undefined}
          value={query}
          onChange={handleInputChange}
          onKeyDown={handleKeyDown}
          onFocus={handleFocus}
          placeholder={t("common.search")}
          className="h-[34px] w-full rounded-[10px] border border-border bg-bg-elevated pl-8 pr-12 text-[12px] text-foreground placeholder:text-muted-foreground outline-none transition-[border-color,box-shadow] focus:border-primary focus:ring-[3px] focus:ring-primary/20"
        />
      </div>

      {isOpen && query.length >= 2 && (
        <SearchDropdown data={data} hasResults={hasResults} isFetching={isFetching} selectedIndex={selectedIndex} onClose={handleClose} onSubmit={handleSubmit} t={t} />
      )}
    </div>
  )
}
