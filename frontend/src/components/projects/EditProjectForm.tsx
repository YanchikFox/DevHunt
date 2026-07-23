import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Input } from "@/components/ui/input"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useForm } from "react-hook-form"
import type { UseFormRegister, UseFormWatch, FieldErrors } from "react-hook-form"
import { useCallback, useState } from "react"
import { useTranslations } from "next-intl"
import { Plus, Trash2 } from "lucide-react"
import type { Project } from "@/lib/api/schema"
import { SkillsChipsTypeahead } from "@/components/profile/SkillsChipsTypeahead"
import type { OpenRoleInput } from "@/lib/api/queries/teams"

type EditableProject = Omit<Project, "maxTeamSize"> & { maxTeamSize?: number | null }

type ProjectFormValues = {
  title: string
  description: string
  visibility: "public" | "private" | "members" | "subscribers"
  slug: string
}

export interface EditProjectFormProps {
  project: EditableProject
  onSave: (data: Partial<EditableProject>) => Promise<void>
  onCancel: () => void
}

interface OpenRoleRowProps {
  entry: OpenRoleInput
  index: number
  onChange: (index: number, field: keyof OpenRoleInput, value: string | number | boolean | null) => void
  onRemove: (index: number) => void
}

function OpenRoleRow({ entry, index, onChange, onRemove }: OpenRoleRowProps) {
  const t = useTranslations()
  const handleRole = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => onChange(index, "role", e.target.value),
    [onChange, index]
  )
  const handleTotal = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => onChange(index, "totalNeeded", parseInt(e.target.value) || 1),
    [onChange, index]
  )
  const handleHours = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const v = e.target.value
      onChange(index, "hoursPerWeek", v === "" ? null : parseInt(v) || null)
    },
    [onChange, index]
  )
  const handleEquity = useCallback(
    (checked: boolean) => onChange(index, "equityOptional", checked),
    [onChange, index]
  )
  const handleRemove = useCallback(() => onRemove(index), [onRemove, index])

  return (
    <div className="rounded-[10px] border border-border bg-background p-3 space-y-3">
      <div className="grid grid-cols-[1fr_auto] gap-2">
        <Input
          value={entry.role}
          onChange={handleRole}
          placeholder={t("projects.rolePlaceholder")}
          className="h-8 text-[13px]"
        />
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={handleRemove}
          className="h-8 w-8 p-0 text-muted-foreground hover:text-destructive"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </Button>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="space-y-1">
          <Label className="font-mono text-[10px] text-muted-foreground uppercase">{t("projects.slotsNeeded")}</Label>
          <Input
            type="number"
            min={1}
            max={20}
            value={entry.totalNeeded}
            onChange={handleTotal}
            className="h-7 text-[12px]"
          />
        </div>
        <div className="space-y-1">
          <Label className="font-mono text-[10px] text-muted-foreground uppercase">{t("projects.hoursPerWeek")}</Label>
          <Input
            type="number"
            min={1}
            max={80}
            value={entry.hoursPerWeek ?? ""}
            onChange={handleHours}
            placeholder="—"
            className="h-7 text-[12px]"
          />
        </div>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox
          id={`equity-${index}`}
          checked={entry.equityOptional}
          onCheckedChange={handleEquity}
        />
        <Label htmlFor={`equity-${index}`} className="text-[12px] text-muted-foreground cursor-pointer">
          {t("projects.equityOptional")}
        </Label>
      </div>
    </div>
  )
}

interface ProjectBasicFieldsProps {
  register: UseFormRegister<ProjectFormValues>
  errors: FieldErrors<ProjectFormValues>
  watch: UseFormWatch<ProjectFormValues>
  t: ReturnType<typeof useTranslations>
}

function ProjectBasicFields({ register, errors, watch, t }: ProjectBasicFieldsProps) {
  return (
    <>
      <div className="space-y-2">
        <Label htmlFor="edit-title">{t("projects.projectTitle")}</Label>
        <Input
          id="edit-title"
          {...register("title", { required: t("projects.projectTitleRequired") })}
          placeholder={t("projects.projectTitlePlaceholder")}
        />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message as string}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor="edit-slug" className="flex items-center gap-1.5">
          {t("projects.slugLabel") || "URL handle"}
          <span className="font-mono text-[10px] font-normal text-muted-foreground">
            /projects/<span className="text-foreground">{watch("slug")?.trim().toLowerCase() || "your-handle"}</span>
          </span>
        </Label>
        <Input
          id="edit-slug"
          {...register("slug", {
            pattern: {
              value: /^[a-z0-9]+(?:-[a-z0-9]+)*$/,
              message: t("projects.slugInvalid") || "Use lowercase letters, digits, and hyphens only",
            },
            maxLength: { value: 48, message: t("projects.slugTooLong") || "Handle must be 48 characters or less" },
          })}
          placeholder="helix"
          className="font-mono text-[13px] lowercase"
          autoComplete="off"
        />
        <p className="text-[11px] text-muted-foreground">
          {t("projects.slugHint") || "Used to build a pretty URL for this project. Leave empty to use the project ID."}
        </p>
        {errors.slug && <p className="text-sm text-destructive">{errors.slug.message as string}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor="edit-description">{t("projects.projectDescription")}</Label>
        <textarea
          id="edit-description"
          {...register("description", { required: t("projects.projectDescriptionRequired") })}
          rows={4}
          className="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          placeholder={t("projects.projectDescriptionPlaceholder")}
        />
        {errors.description && <p className="text-sm text-destructive">{errors.description.message as string}</p>}
      </div>
    </>
  )
}

function OpenRolesEditor({ openRoles, onAdd, onChange, onRemove, t }: {
  openRoles: OpenRoleInput[]
  onAdd: () => void
  onChange: (index: number, field: keyof OpenRoleInput, value: string | number | boolean | null) => void
  onRemove: (index: number) => void
  t: ReturnType<typeof useTranslations>
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Label className="text-[13px]">{t("projects.openRolesLabel")}</Label>
        <Button type="button" variant="outline" size="sm" onClick={onAdd} className="h-7 gap-1 text-[12px]">
          <Plus className="h-3 w-3" />
          {t("projects.addRole")}
        </Button>
      </div>
      {openRoles.length > 0 ? (
        <div className="space-y-2">
          {openRoles.map((entry, index) => (
            <OpenRoleRow key={index} entry={entry} index={index} onChange={onChange} onRemove={onRemove} />
          ))}
        </div>
      ) : (
        <p className="text-[12px] text-muted-foreground">{t("projects.noOpenRolesYet")}</p>
      )}
    </div>
  )
}

export function EditProjectForm({ project, onSave, onCancel }: EditProjectFormProps) {
  const t = useTranslations()
  const [technologies, setTechnologies] = useState<string[]>(
    Array.isArray(project.technologies) ? project.technologies : []
  )

  // Initialize openRoles from project.openRoles or fall back to requiredRoles string array
  const [openRoles, setOpenRoles] = useState<OpenRoleInput[]>(() => {
    if (Array.isArray(project.openRoles) && project.openRoles.length > 0) {
      return project.openRoles.map((r) => ({
        role: r.role,
        totalNeeded: r.totalNeeded,
        hoursPerWeek: r.hoursPerWeek ?? null,
        equityOptional: r.equityOptional,
      }))
    }
    // Migrate from legacy string array
    const legacy = Array.isArray(project.requiredRoles) ? project.requiredRoles : []
    if (legacy.length > 0) {
      const counts: Record<string, number> = {}
      for (const r of legacy) {
        counts[r] = (counts[r] || 0) + 1
      }
      return Object.entries(counts).map(([role, totalNeeded]) => ({
        role,
        totalNeeded,
        hoursPerWeek: null,
        equityOptional: false,
      }))
    }
    return []
  })

  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
  } = useForm<ProjectFormValues>({
    defaultValues: {
      title: project.title,
      description: project.description,
      visibility: (project.visibility || "public") as ProjectFormValues["visibility"],
      slug: project.slug ?? "",
    },
  })

  const visibility = watch("visibility")

  const handleVisibilityChange = useCallback(
    (value: string) => {
      setValue("visibility", value as "public" | "private")
    },
    [setValue]
  )

  const handleRoleChange = useCallback(
    (index: number, field: keyof OpenRoleInput, value: string | number | boolean | null) => {
      setOpenRoles((prev) =>
        prev.map((r, i) => (i === index ? { ...r, [field]: value } : r))
      )
    },
    []
  )

  const handleRemoveRole = useCallback((index: number) => {
    setOpenRoles((prev) => prev.filter((_, i) => i !== index))
  }, [])

  const handleAddRole = useCallback(() => {
    setOpenRoles((prev) => [
      ...prev,
      { role: "", totalNeeded: 1, hoursPerWeek: null, equityOptional: false },
    ])
  }, [])

  return (
    <form
      onSubmit={handleSubmit(async (data) => {
        const validRoles = openRoles.filter((r) => r.role.trim())
        const trimmedSlug = (data.slug ?? "").trim().toLowerCase()
        await onSave({
          title: data.title,
          description: data.description,
          technologies,
          openRoles: validRoles,
          requiredRoles: validRoles.flatMap((r) => Array(r.totalNeeded).fill(r.role)),
          visibility: data.visibility,
          slug: trimmedSlug ? trimmedSlug : null,
        })
      })}
      className="space-y-4"
    >
      <ProjectBasicFields register={register} errors={errors} watch={watch} t={t} />

      <div className="space-y-2">
        <SkillsChipsTypeahead
          inputId="edit-technologies"
          label={t("projects.technologies")}
          placeholder="React, TypeScript, Node.js"
          skills={technologies}
          setSkills={setTechnologies}
          maxSkills={20}
          showHelpText={false}
        />
      </div>

      <OpenRolesEditor openRoles={openRoles} onAdd={handleAddRole} onChange={handleRoleChange} onRemove={handleRemoveRole} t={t} />

      <div className="space-y-2">
        <Label htmlFor="edit-visibility">{t("projects.visibilityLabel")}</Label>
        <Select value={visibility} onValueChange={handleVisibilityChange}>
          <SelectTrigger id="edit-visibility">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="public">{t("projects.public")}</SelectItem>
            <SelectItem value="private">{t("projects.private")}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="flex justify-end gap-2 pt-4">
        <Button type="button" variant="outline" onClick={onCancel}>
          {t("common.cancel")}
        </Button>
        <Button type="submit">{t("common.save")}</Button>
      </div>
    </form>
  )
}
