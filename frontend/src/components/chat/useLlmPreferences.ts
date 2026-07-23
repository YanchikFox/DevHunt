"use client"

import { useCallback, useEffect, useState } from "react"

const STORAGE_KEY = "devhunt.ai.llm.preferences"

export interface LlmPreferences {
  readonly provider?: string | null
  readonly modelId?: string | null
  readonly enableTools: boolean
}

const DEFAULT_PREFERENCES: LlmPreferences = {
  provider: null,
  modelId: null,
  enableTools: true,
}

export function useLlmPreferences() {
  const [preferences, setPreferences] = useState<LlmPreferences>(DEFAULT_PREFERENCES)

  useEffect(() => {
    const stored = readStoredPreferences()
    if (stored) setPreferences(stored)
  }, [])

  const updatePreferences = useCallback((patch: Partial<LlmPreferences>) => {
    setPreferences((current) => {
      const next = { ...current, ...patch }
      writeStoredPreferences(next)
      return next
    })
  }, [])

  return { preferences, updatePreferences }
}

function readStoredPreferences(): LlmPreferences | null {
  if (typeof window === "undefined") return null

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isStoredPreferences(parsed)) return null
    return parsed
  } catch {
    return null
  }
}

function writeStoredPreferences(preferences: LlmPreferences): void {
  if (typeof window === "undefined") return
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences))
}

function isStoredPreferences(value: unknown): value is LlmPreferences {
  if (!isObjectRecord(value)) return false
  const providerValid =
    value.provider === undefined ||
    value.provider === null ||
    typeof value.provider === "string"
  const modelValid =
    value.modelId === undefined ||
    value.modelId === null ||
    typeof value.modelId === "string"
  const toolsValid = value.enableTools === undefined || typeof value.enableTools === "boolean"
  return providerValid && modelValid && toolsValid
}

function isObjectRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null
}
