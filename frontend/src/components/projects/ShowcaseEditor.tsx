"use client"

import { useCallback, useEffect } from "react"
import { useForm, useFieldArray, type Control } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Loader2, Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl"

import { Button } from "@/components/ui/button"
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { useToast } from "@/hooks/use-toast"
import { useShowcase, useCreateShowcase, useUpdateShowcase } from "@/lib/api/queries/showcase"

const createShowcaseSchema = (t: (key: string) => string) => z.object({
  summary: z.string().min(10, t("showcase.summaryMinLength")),
  demoUrl: z.string().url(t("validation.invalidUrl")).optional().or(z.literal("")),
  demoVideoUrl: z.string().url(t("validation.invalidUrl")).optional().or(z.literal("")),
  repositoryUrl: z.string().url(t("validation.invalidUrl")).optional().or(z.literal("")),
  screenshots: z.array(z.object({ value: z.string().url(t("validation.invalidUrl")) })),
})

type ShowcaseFormValues = z.infer<ReturnType<typeof createShowcaseSchema>>

function ShowcaseScreenshotsField({ fields, control, canManage, onAppend, onRemove, t }: {
  fields: { id: string }[]
  control: Control<ShowcaseFormValues>
  canManage: boolean
  onAppend: () => void
  onRemove: (index: number) => void
  t: (key: string) => string
}) {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <FormLabel>{t("showcase.screenshots")}</FormLabel>
        {canManage && (
          <Button type="button" variant="outline" size="sm" onClick={onAppend}>
            <Plus className="h-4 w-4 mr-2" />
            {t("showcase.addUrl")}
          </Button>
        )}
      </div>
      {fields.map((field, index) => (
        <ScreenshotFieldRow key={field.id} field={field} index={index} control={control} canManage={canManage} onRemove={onRemove} />
      ))}
      {fields.length === 0 && (
        <p className="text-sm text-muted-foreground italic">{t("showcase.noScreenshotsAdded")}</p>
      )}
    </div>
  )
}

function ShowcaseUrlField({ control, name, label, placeholder, disabled }: {
  control: Control<ShowcaseFormValues>
  name: "demoUrl" | "repositoryUrl" | "demoVideoUrl"
  label: string
  placeholder: string
  disabled: boolean
}) {
  return (
    <FormField
      control={control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <Input placeholder={placeholder} {...field} disabled={disabled} />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  )
}

interface ScreenshotFieldRowProps {
  field: { id: string }
  index: number
  control: Control<ShowcaseFormValues>
  canManage: boolean
  onRemove: (index: number) => void
}

function ScreenshotFieldRow({ field, index, control, canManage, onRemove }: ScreenshotFieldRowProps) {
  const handleRemove = useCallback(() => onRemove(index), [onRemove, index])
  return (
    <div key={field.id} className="flex gap-2">
      <FormField
        control={control}
        name={`screenshots.${index}.value`}
        render={({ field: inputField }) => (
          <FormItem className="flex-1">
            <FormControl>
              <Input placeholder="https://..." {...inputField} disabled={!canManage} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      {canManage && (
        <Button type="button" variant="ghost" size="icon" onClick={handleRemove}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      )}
    </div>
  )
}

interface ShowcaseEditorProps {
  projectId: string
  canManage: boolean
}

export function ShowcaseEditor({ projectId, canManage }: ShowcaseEditorProps) {
  const t = useTranslations()
  const { toast } = useToast()
  const { data: showcase, isLoading } = useShowcase(projectId)
  const createShowcase = useCreateShowcase()
  const updateShowcase = useUpdateShowcase()

  const form = useForm<ShowcaseFormValues>({
    resolver: zodResolver(createShowcaseSchema(t)),
    defaultValues: {
      summary: "",
      demoUrl: "",
      demoVideoUrl: "",
      repositoryUrl: "",
      screenshots: [],
    },
  })

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: "screenshots",
  })

  const handleAppendScreenshot = useCallback(() => append({ value: "" }), [append])

  useEffect(() => {
    if (showcase) {
      form.reset({
        summary: showcase.summary || "",
        demoUrl: showcase.demoUrl || "",
        demoVideoUrl: showcase.demoVideoUrl || "",
        repositoryUrl: showcase.repositoryUrl || "",
        screenshots: showcase.screenshots?.map((s) => ({ value: s })) || [],
      })
    }
  }, [showcase, form])

  const onSubmit = async (data: ShowcaseFormValues) => {
    try {
      const payload = {
        summary: data.summary,
        demoUrl: data.demoUrl || undefined,
        demoVideoUrl: data.demoVideoUrl || undefined,
        repositoryUrl: data.repositoryUrl || undefined,
        screenshots: data.screenshots.map((s) => s.value),
      }

      if (showcase) {
        await updateShowcase.mutateAsync({ projectId, data: payload })
        toast({
          title: t("common.success"),
          description: t("showcase.updatedSuccess"),
        })
      } else {
        await createShowcase.mutateAsync({ projectId, data: payload })
        toast({
          title: t("common.success"),
          description: t("showcase.createdSuccess"),
        })
      }
    } catch (error) {
      console.error("Failed to save showcase:", error)
      toast({
        title: t("common.error"),
        description: t("showcase.saveFailed"),
        variant: "destructive",
      })
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    )
  }

  if (!canManage && !showcase) {
    return (
      <Card>
        <CardContent className="p-8 text-center text-muted-foreground">
          {t("showcase.notSetUpYet")}
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="border-border/70 bg-card/80 shadow-sm">
      <CardHeader>
        <CardTitle>{t("showcase.title")}</CardTitle>
        <CardDescription>
          {t("showcase.description")}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            <FormField
              control={form.control}
              name="summary"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("showcase.summary")}</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder={t("showcase.summaryPlaceholder")}
                      className="min-h-[100px]"
                      {...field}
                      disabled={!canManage}
                    />
                  </FormControl>
                  <FormDescription>
                    {t("showcase.summaryDescription")}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-4 md:grid-cols-2">
              <ShowcaseUrlField control={form.control} name="demoUrl" label={t("showcase.demoUrl")} placeholder={t("showcase.urlPlaceholder")} disabled={!canManage} />
              <ShowcaseUrlField control={form.control} name="repositoryUrl" label={t("showcase.repositoryUrl")} placeholder={t("showcase.repositoryUrlPlaceholder")} disabled={!canManage} />
            </div>

            <FormField
              control={form.control}
              name="demoVideoUrl"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("showcase.demoVideoUrl")}</FormLabel>
                  <FormControl>
                    <Input placeholder={t("showcase.demoVideoUrlPlaceholder")} {...field} disabled={!canManage} />
                  </FormControl>
                  <FormDescription>{t("showcase.demoVideoUrlDescription")}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <ShowcaseScreenshotsField fields={fields} control={form.control} canManage={canManage} onAppend={handleAppendScreenshot} onRemove={remove} t={t} />

            {canManage && (
              <div className="flex justify-end">
                <Button type="submit" disabled={createShowcase.isPending || updateShowcase.isPending}>
                  {(createShowcase.isPending || updateShowcase.isPending) && (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  )}
                  {t("showcase.saveChanges")}
                </Button>
              </div>
            )}
          </form>
        </Form>
      </CardContent>
    </Card>
  )
}
