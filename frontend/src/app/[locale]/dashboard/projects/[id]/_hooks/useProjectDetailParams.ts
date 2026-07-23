import { useEffect, useMemo, useState } from "react"

interface ProjectParams {
  id: string
}

export function useProjectDetailParams(params: Promise<ProjectParams> | ProjectParams) {
  const projectId = useMemo(() => {
    if ("then" in params && typeof params.then === "function") {
      return null
    }
    return (params as ProjectParams).id
  }, [params])

  const [resolvedId, setResolvedId] = useState<string | null>(projectId?.trim() || null)
  const [mounted, setMounted] = useState(false)

  useEffect(() => {
    setMounted(true)
  }, [])

  useEffect(() => {
    if ("then" in params && typeof params.then === "function") {
      ;(params as Promise<ProjectParams>).then((p) => setResolvedId(p.id?.trim() || null))
    }
  }, [params])

  const finalProjectId = (resolvedId || projectId || "").trim()
  const hasProjectId = finalProjectId.length > 0

  return { finalProjectId, hasProjectId, mounted }
}
