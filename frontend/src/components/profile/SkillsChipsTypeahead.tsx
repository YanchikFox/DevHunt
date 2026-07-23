"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  useResolveSkills,
  useSkillCategories,
  useSkillSuggestions,
  type SkillSuggestItem,
} from "@/lib/api/queries/skills"
import { useDebouncedValue } from "@/hooks/use-debounced-value"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useTranslations } from "next-intl"
import { useToast } from "@/hooks/use-toast"
import { containsProfanity } from "@/lib/profanity"

export interface SkillsChipsTypeaheadProps {
  inputId?: string
  label?: string
  placeholder?: string
  skills: string[]
  setSkills: (skills: string[]) => void
  maxSkills?: number
  showLabel?: boolean
  showHelpText?: boolean
  enableSuggestions?: boolean
}

interface SuggestionItemProps {
  item: SkillSuggestItem
  isActive: boolean
  itemIndex: number
  onActivate: (index: number) => void
  onSelect: (name: string) => void
}

function SuggestionItem({ item, isActive, itemIndex, onActivate, onSelect }: SuggestionItemProps) {
  const handleMouseEnter = useCallback(() => onActivate(itemIndex), [onActivate, itemIndex])
  const handleMouseDown = useCallback((e: { preventDefault(): void }) => e.preventDefault(), [])
  const handleClick = useCallback(() => onSelect(item.name), [onSelect, item.name])

  return (
    <button
      type="button"
      className={
        "flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-muted/50 " +
        (isActive ? "bg-muted/50" : "")
      }
      onMouseEnter={handleMouseEnter}
      onMouseDown={handleMouseDown}
      onClick={handleClick}
    >
      <span>{item.name}</span>
      <span className="text-xs text-muted-foreground">{item.category}</span>
    </button>
  )
}

export function SkillsChipsTypeahead({
  inputId = "skills",
  label = "Skills",
  placeholder = "e.g., React, Python, UI/UX Design",
  skills,
  setSkills,
  maxSkills = 20,
  showLabel = true,
  showHelpText = true,
  enableSuggestions = true,
}: SkillsChipsTypeaheadProps) {
  const t = useTranslations()
  const { toast } = useToast()
  const [skillInput, setSkillInput] = useState("")
  const debouncedQuery = useDebouncedValue(skillInput, 200)
  const [activeIndex, setActiveIndex] = useState<number>(-1)
  const [isFocused, setIsFocused] = useState(false)

  const ALL_CATEGORIES_VALUE = "__all__"
  const [selectedCategory, setSelectedCategory] = useState<string>(ALL_CATEGORIES_VALUE)
  const categoriesQuery = useSkillCategories()

  // Local metadata for rendering/grouping. Keys are normalized (trim+lower) skill names.
  const [skillMetaByKey, setSkillMetaByKey] = useState<Record<string, { name: string; category: string }>>({})

  const suggestQuery = useSkillSuggestions({
    q: enableSuggestions ? debouncedQuery : "",
    limit: 8,
    category: selectedCategory === ALL_CATEGORIES_VALUE ? undefined : selectedCategory,
  })

  const resolveQuery = useResolveSkills({ names: skills })

  const normalizedSkillsSet = useMemo(() => {
    return new Set(skills.map((s) => s.trim().toLowerCase()).filter(Boolean))
  }, [skills])

  const suggestions = useMemo(() => {
    if (!enableSuggestions) return []
    const items = suggestQuery.data?.items || []
    return items.filter((item) => !normalizedSkillsSet.has(item.name.trim().toLowerCase()))
  }, [enableSuggestions, suggestQuery.data?.items, normalizedSkillsSet])

  useEffect(() => {
    const resolved = resolveQuery.data?.items
    if (!resolved || resolved.length === 0) return

    // 1) Merge metadata for category grouping.
    setSkillMetaByKey((prev) => {
      let changed = false
      const next = { ...prev }

      for (const item of resolved) {
        const key = item.name.trim().toLowerCase()
        const existing = next[key]
        if (!existing || existing.category !== item.category || existing.name !== item.name) {
          next[key] = { name: item.name, category: item.category }
          changed = true
        }
      }

      return changed ? next : prev
    })

    // 2) Normalize stored skill strings to canonical names when we can resolve them.
    //    Example: "react" -> "React", "dotnet" -> ".NET".
    const normalized = skills.slice()
    let changedSkills = false
    const existingLower = new Set(normalized.map((s) => s.trim().toLowerCase()).filter(Boolean))

    for (const item of resolved) {
      const rawKey = item.raw.trim().toLowerCase()
      const canonical = item.name.trim()
      const canonicalKey = canonical.toLowerCase()

      const idx = normalized.findIndex((s) => s.trim().toLowerCase() === rawKey)
      if (idx < 0) continue

      if (normalized[idx] !== canonical) {
        // If canonical already exists elsewhere, drop the duplicate raw entry.
        if (existingLower.has(canonicalKey) && rawKey !== canonicalKey) {
          normalized.splice(idx, 1)
        } else {
          normalized[idx] = canonical
          existingLower.add(canonicalKey)
        }
        changedSkills = true
      }
    }

    if (changedSkills) {
      setSkills(normalized)
    }
  }, [resolveQuery.data?.items, setSkills, skills])

  const rememberSkillMeta = useCallback((item: SkillSuggestItem) => {
    const key = item.name.trim().toLowerCase()
    setSkillMetaByKey((prev) => {
      const existing = prev[key]
      if (existing && existing.category === item.category && existing.name === item.name) {
        return prev
      }
      return { ...prev, [key]: { name: item.name, category: item.category } }
    })
  }, [])

  const addSkillValue = useCallback(
    (value: string, meta?: { category?: string }) => {
      const trimmed = value.trim()
      if (!trimmed) return
      if (skills.length >= maxSkills) return

      const key = trimmed.toLowerCase()
      if (normalizedSkillsSet.has(key)) return

      if (containsProfanity(trimmed)) {
        toast({ title: t("profanity.tagRejected"), variant: "destructive" })
        setSkillInput("")
        setActiveIndex(-1)
        return
      }

      setSkills([...skills, trimmed])

      if (meta?.category) {
        const key2 = trimmed.toLowerCase()
        setSkillMetaByKey((prev) => ({ ...prev, [key2]: { name: trimmed, category: meta.category! } }))
      }
      setSkillInput("")
      setActiveIndex(-1)
    },
    [maxSkills, normalizedSkillsSet, setSkills, skills, toast, t]
  )

  const addManySkillValues = useCallback(
    (values: string[]) => {
      if (values.length === 0) return

      const existing = new Set(skills.map((s) => s.trim().toLowerCase()).filter(Boolean))
      const next: string[] = [...skills]
      let profanityFound = false

      for (const raw of values) {
        if (next.length >= maxSkills) break
        const trimmed = raw.trim()
        if (!trimmed) continue
        const key = trimmed.toLowerCase()
        if (existing.has(key)) continue
        if (containsProfanity(trimmed)) {
          profanityFound = true
          continue
        }
        existing.add(key)
        next.push(trimmed)
      }

      if (profanityFound) {
        toast({ title: t("profanity.tagRejected"), variant: "destructive" })
      }

      if (next.length !== skills.length) {
        setSkills(next)
      }
      setSkillInput("")
      setActiveIndex(-1)
    },
    [maxSkills, setSkills, skills, toast, t]
  )

  const parseSkillsFromText = useCallback((text: string) => {
    // Split by common separators: commas, semicolons, newlines, tabs.
    return text
      .split(/[\n\r\t,;]+/g)
      .map((s) => s.trim())
      .filter(Boolean)
  }, [])

  const pickBestExactSuggestion = useCallback(
    (input: string) => {
      const raw = input.trim()
      if (!raw) return null
      if (suggestions.length === 0) return null

      const rawLower = raw.toLowerCase()

      // Prefer exact alias/name matches (server already ranked).
      const exactByMatch = suggestions.find((s) => s.match === "alias_exact" || s.match === "name_exact")
      if (exactByMatch) return exactByMatch

      const exactByName = suggestions.find((s) => s.name.trim().toLowerCase() === rawLower)
      return exactByName || null
    },
    [suggestions]
  )

  const addRawSkill = useCallback(() => {
    const best = pickBestExactSuggestion(skillInput)
    if (best) {
      rememberSkillMeta(best)
      addSkillValue(best.name, { category: best.category })
      return
    }
    addSkillValue(skillInput)
  }, [addSkillValue, pickBestExactSuggestion, rememberSkillMeta, skillInput])

  const addSuggestedSkill = useCallback(
    (index: number) => {
      const item = suggestions[index]
      if (!item) return
      rememberSkillMeta(item)
      addSkillValue(item.name, { category: item.category })
    },
    [addSkillValue, rememberSkillMeta, suggestions]
  )

  const removeSkill = useCallback(
    (skill: string) => {
      setSkills(skills.filter((s) => s !== skill))

      const key = skill.trim().toLowerCase()
      setSkillMetaByKey((prev) => {
        if (!prev[key]) return prev
        const next = { ...prev }
        delete next[key]
        return next
      })
    },
    [skills, setSkills]
  )

  const handleSkillInputKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === "ArrowDown") {
        if (suggestions.length > 0) {
          e.preventDefault()
          setActiveIndex((prev) => {
            const next = prev + 1
            return next >= suggestions.length ? 0 : next
          })
        }
        return
      }

      if (e.key === "ArrowUp") {
        if (suggestions.length > 0) {
          e.preventDefault()
          setActiveIndex((prev) => {
            const next = prev - 1
            return next < 0 ? suggestions.length - 1 : next
          })
        }
        return
      }

      if (e.key === "Escape") {
        setActiveIndex(-1)
        setIsFocused(false)
        return
      }

      if (e.key === "Tab") {
        if (suggestions.length > 0 && activeIndex >= 0) {
          e.preventDefault()
          addSuggestedSkill(activeIndex)
        }
        return
      }

      if (e.key === "," || e.key === ";") {
        if (skillInput.trim().length > 0) {
          e.preventDefault()
          addRawSkill()
        }
        return
      }

      if (e.key === "Backspace") {
        if (skillInput.trim().length === 0 && skills.length > 0) {
          e.preventDefault()
          setSkills(skills.slice(0, -1))
        }
        return
      }

      if (e.key === "Enter") {
        e.preventDefault()
        if (suggestions.length > 0 && activeIndex >= 0) {
          addSuggestedSkill(activeIndex)
          return
        }

        // If no selection is active, prefer an exact match (alias/name) when available.
        addRawSkill()
      }
    },
    [activeIndex, addRawSkill, addSuggestedSkill, skillInput, skills, setSkills, suggestions.length]
  )

  const showSuggestions = enableSuggestions && isFocused && skillInput.trim().length > 0

  const handleChange = useCallback((value: string) => {
    setSkillInput(value)
    setActiveIndex(-1)
  }, [])

  const handlePaste = useCallback(
    (e: React.ClipboardEvent<HTMLInputElement>) => {
      const text = e.clipboardData.getData("text")
      if (!text) return

      const isMultiToken = /[\n\r\t,;]/.test(text)
      if (!isMultiToken) return

      e.preventDefault()
      addManySkillValues(parseSkillsFromText(text))
    },
    [addManySkillValues, parseSkillsFromText]
  )

  const handleInputChange = useCallback((e: { target: { value: string } }) => handleChange(e.target.value), [handleChange])
  const handleFocus = useCallback(() => setIsFocused(true), [])
  const handleBlur = useCallback(() => setIsFocused(false), [])

  const groupedSkills = useMemo(() => {
    const groups = new Map<string, string[]>()

    for (const raw of skills) {
      const trimmed = raw.trim()
      if (!trimmed) continue

      const key = trimmed.toLowerCase()
      const category = skillMetaByKey[key]?.category || "Other"

      const list = groups.get(category) || []
      list.push(trimmed)
      groups.set(category, list)
    }

    const sortedCategories = [...groups.keys()].sort((a, b) => {
      if (a === "Other" && b !== "Other") return 1
      if (b === "Other" && a !== "Other") return -1
      return a.localeCompare(b)
    })

    return sortedCategories.map((c) => [c, (groups.get(c) || []).sort((a, b) => a.localeCompare(b))] as const)
  }, [skills, skillMetaByKey])

  const categories = useMemo(() => {
    const list = categoriesQuery.data || []
    return list.filter(Boolean)
  }, [categoriesQuery.data])

  return (
    <div className="space-y-2">
      {showLabel && <Label htmlFor={inputId}>{label}</Label>}
      <div className="flex gap-2">
        <div className="w-44">
          <Select value={selectedCategory} onValueChange={setSelectedCategory}>
            <SelectTrigger aria-label="Category">
              <SelectValue placeholder="All" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_CATEGORIES_VALUE}>All</SelectItem>
              {categories.map((c) => (
                <SelectItem key={c} value={c}>
                  {c}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Input
          id={inputId}
          placeholder={placeholder}
          value={skillInput}
          onChange={handleInputChange}
          onKeyDown={handleSkillInputKeyDown}
          onFocus={handleFocus}
          onBlur={handleBlur}
          onPaste={handlePaste}
        />
        <Button type="button" onClick={addRawSkill} variant="secondary">
          Add
        </Button>
      </div>

      {showSuggestions && (
        <div className="rounded-md border border-border bg-background">
          {suggestQuery.isFetching && (
            <div className="px-3 py-2 text-sm text-muted-foreground">Searching…</div>
          )}

          {!suggestQuery.isFetching && suggestions.length === 0 && (
            <div className="px-3 py-2 text-sm text-muted-foreground">{t("skillsTypeahead.noSuggestions")}</div>
          )}

          {suggestions.map((item, itemIndex) => (
            <SuggestionItem
              key={item.id}
              item={item}
              isActive={suggestions[activeIndex]?.id === item.id}
              itemIndex={itemIndex}
              onActivate={setActiveIndex}
              onSelect={addSkillValue}
            />
          ))}
        </div>
      )}

      {skills.length > 0 && (
        <div className="mt-2 space-y-3">
          {groupedSkills.map(([category, items]) => (
            <div key={category} className="space-y-2">
              <div className="text-xs font-medium text-muted-foreground">{category}</div>
              <div className="flex flex-wrap gap-2">
                {items.map((skill) => (
                  <SkillBadge key={skill} skill={skill} onRemove={removeSkill} />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {showHelpText && (
        <p className="text-xs text-muted-foreground">
          Press Enter or click Add. Click on a skill to remove it. Max {maxSkills} skills.
        </p>
      )}
    </div>
  )
}

interface SkillBadgeProps {
  skill: string
  onRemove: (skill: string) => void
}

function SkillBadge({ skill, onRemove }: SkillBadgeProps) {
  const handleRemove = useCallback(() => {
    onRemove(skill)
  }, [onRemove, skill])

  return (
    <Badge
      variant="secondary"
      className="cursor-pointer hover:bg-destructive hover:text-destructive-foreground"
      onClick={handleRemove}
    >
      {skill} ×
    </Badge>
  )
}
