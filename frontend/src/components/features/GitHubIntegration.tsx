"use client"

import { useState, useCallback } from "react"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Badge } from "@/components/ui/badge"
import { Github, Link2, CheckCircle2, ExternalLink } from "lucide-react"

interface RepoConnection {
  id: string
  name: string
  owner: string
  url: string
  isConnected: boolean
  lastSync?: string
}

export function GitHubIntegration() {
  const [repoUrl, setRepoUrl] = useState("")
  const [connectedRepos, setConnectedRepos] = useState<RepoConnection[]>([
    {
      id: "1",
      name: "devhunt-platform",
      owner: "devhunt",
      url: "https://github.com/devhunt/devhunt-platform",
      isConnected: true,
      lastSync: new Date().toISOString(),
    },
  ])

  const handleConnect = useCallback(() => {
    const match = repoUrl.match(/github\.com\/([^/]+)\/([^/]+)/)
    if (match) {
      const [, owner, name] = match
      const newRepo: RepoConnection = {
        id: Date.now().toString(),
        name: name.replace(/\.git$/, ""),
        owner,
        url: repoUrl,
        isConnected: true,
        lastSync: new Date().toISOString(),
      }
      setConnectedRepos((prev) => [...prev, newRepo])
      setRepoUrl("")
    }
  }, [repoUrl])

  const handleRepoUrlChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setRepoUrl(e.target.value)
  }, [])

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Github className="h-5 w-5 text-primary" />
          GitHub Integration
        </CardTitle>
        <CardDescription>Connect your repositories for live sync</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex gap-2">
          <div className="flex-1">
            <Label htmlFor="repo-url" className="sr-only">
              Repository URL
            </Label>
            <Input
              id="repo-url"
              placeholder="https://github.com/owner/repo"
              value={repoUrl}
              onChange={handleRepoUrlChange}
              className="font-mono text-sm"
            />
          </div>
          <Button onClick={handleConnect} disabled={!repoUrl}>
            <Link2 className="h-4 w-4 mr-2" />
            Connect
          </Button>
        </div>

        {connectedRepos.length > 0 && (
          <div className="space-y-2">
            <Label className="text-sm font-medium">Connected Repositories</Label>
            {connectedRepos.map((repo) => (
              <div
                key={repo.id}
                className="flex items-center justify-between p-3 rounded-lg border-2 hover:border-primary/50 transition-colors bg-muted/30"
              >
                <div className="flex items-center gap-3 flex-1 min-w-0">
                  <Github className="h-5 w-5 text-muted-foreground flex-shrink-0" />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="font-semibold text-sm truncate">
                        {repo.owner}/{repo.name}
                      </p>
                      {repo.isConnected && (
                        <Badge
                          variant="outline"
                          className="bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400 border-green-300 dark:border-green-700"
                        >
                          <CheckCircle2 className="h-3 w-3 mr-1" />
                          Connected
                        </Badge>
                      )}
                    </div>
                    {repo.lastSync && (
                      <p className="text-xs text-muted-foreground">
                        Last synced: {new Date(repo.lastSync).toLocaleString()}
                      </p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Button variant="ghost" size="sm" asChild>
                    <a href={repo.url} target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="h-4 w-4" />
                    </a>
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
