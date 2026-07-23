"use client"

import { useState, useCallback, useMemo } from "react"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Github, Search, Lock, Globe, Loader2, Check } from "lucide-react"
import { cn } from "@/lib/utils"
import { useGitHubRepos, useUpdateIntegration, type GitHubRepo } from "@/lib/api/queries/integrations"
import { useToast } from "@/hooks/use-toast"
import { ScrollArea } from "@/components/ui/scroll-area"

interface GitHubRepoSelectorProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    integrationId: string
    projectId: string
    currentRepo?: string
    onSuccess?: () => void
}

export function GitHubRepoSelector({
    open,
    onOpenChange,
    integrationId,
    projectId,
    currentRepo,
    onSuccess,
}: GitHubRepoSelectorProps) {
    const [search, setSearch] = useState("")
    const { toast } = useToast()
    const { data: repos, isLoading, error } = useGitHubRepos(open ? integrationId : undefined)
    const updateIntegration = useUpdateIntegration(projectId)

    const filteredRepos = useMemo(() => {
        if (!repos) return []
        if (!search) return repos
        const lower = search.toLowerCase()
        return repos.filter(
            (r: GitHubRepo) =>
                r.fullName.toLowerCase().includes(lower) ||
                (r.description?.toLowerCase().includes(lower) ?? false)
        )
    }, [repos, search])

    const handleSelect = useCallback(
        async (repo: GitHubRepo) => {
            try {
                await updateIntegration.mutateAsync({
                    integrationId,
                    config: { repository: repo.fullName },
                })
                toast({
                    title: "Repository connected",
                    description: `${repo.fullName} has been linked to your project.`,
                })
                onOpenChange(false)
                onSuccess?.()
            } catch {
                toast({
                    title: "Error",
                    description: "Failed to connect repository.",
                    variant: "destructive",
                })
            }
        },
        [integrationId, updateIntegration, toast, onOpenChange, onSuccess]
    )

    const handleSearchChange = useCallback(
        (e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value),
        []
    )

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-lg">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <Github className="h-5 w-5" />
                        {currentRepo ? "Change Repository" : "Select Repository"}
                    </DialogTitle>
                    <DialogDescription>
                        {currentRepo
                            ? `Currently connected to ${currentRepo}. Select a different repository.`
                            : "Choose a GitHub repository to connect to this project."}
                    </DialogDescription>
                </DialogHeader>

                <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                        placeholder="Search repositories..."
                        value={search}
                        onChange={handleSearchChange}
                        className="pl-9"
                    />
                </div>

                <ScrollArea className="h-[350px] pr-3">
                    {isLoading && (
                        <div className="flex items-center justify-center py-8">
                            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                        </div>
                    )}

                    {error && (
                        <div className="text-center py-8 text-sm text-destructive">
                            Failed to load repositories. Please try again.
                        </div>
                    )}

                    {!isLoading && !error && filteredRepos.length === 0 && (
                        <div className="text-center py-8 text-sm text-muted-foreground">
                            {search ? "No repositories match your search." : "No repositories found."}
                        </div>
                    )}

                    <div className="space-y-1">
                        {filteredRepos.map((repo: GitHubRepo) => {
                            const isCurrent = repo.fullName === currentRepo
                            return (
                                <button
                                    key={repo.fullName}
                                    type="button"
                                    onClick={() => handleSelect(repo)}
                                    disabled={updateIntegration.isPending || isCurrent}
                                    className={cn(
                                        "w-full text-left p-3 rounded-lg border transition-colors disabled:opacity-50",
                                        isCurrent
                                            ? "border-primary/50 bg-primary/5"
                                            : "hover:bg-accent hover:border-primary/50"
                                    )}
                                >
                                    <div className="flex items-center gap-2">
                                        <span className="font-medium text-sm truncate flex-1">
                                            {repo.fullName}
                                        </span>
                                        {isCurrent && (
                                            <Check className="h-4 w-4 text-primary shrink-0" />
                                        )}
                                        <Badge variant="outline" className="text-[10px] shrink-0">
                                            {repo.isPrivate ? (
                                                <><Lock className="h-3 w-3 mr-1" /> Private</>
                                            ) : (
                                                <><Globe className="h-3 w-3 mr-1" /> Public</>
                                            )}
                                        </Badge>
                                    </div>
                                    {repo.description && (
                                        <p className="text-xs text-muted-foreground mt-1 line-clamp-1">
                                            {repo.description}
                                        </p>
                                    )}
                                </button>
                            )
                        })}
                    </div>
                </ScrollArea>

                <div className="flex justify-end">
                    <Button variant="outline" onClick={() => onOpenChange(false)}>
                        Cancel
                    </Button>
                </div>
            </DialogContent>
        </Dialog>
    )
}
