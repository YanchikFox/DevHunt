"use client"

import { useCallback } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { useCreateTicket } from "@/lib/api/queries/support"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form"
import { Loader2, ArrowLeft } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { Link, useRouter } from "@/i18n/routing"

const createTicketSchema = (t: (key: string) => string) =>
  z.object({
    category: z.string().min(1, t("validation.required")),
    subject: z.string().min(3, t("subjectMinLength")).max(200, t("subjectMaxLength")),
    description: z.string().min(10, t("descriptionMinLength")).max(5000, t("descriptionMaxLength")),
  })

type TicketFormValues = z.infer<ReturnType<typeof createTicketSchema>>

export default function NewTicketPage() {
  const t = useTranslations("support")
  const { toast } = useToast()
  const router = useRouter()
  const createTicket = useCreateTicket()

  const form = useForm<TicketFormValues>({
    resolver: zodResolver(createTicketSchema(t)),
    defaultValues: {
      category: "",
      subject: "",
      description: "",
    },
  })

  const onSubmit = useCallback(async (values: TicketFormValues) => {
    try {
      const ticket = await createTicket.mutateAsync(values)
      toast({ title: t("ticketCreated") })
      router.push(`/dashboard/support/${ticket.id}`)
    } catch {
      toast({ title: t("ticketCreateFailed"), variant: "destructive" })
    }
  }, [createTicket, toast, t, router])

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="flex items-center gap-3">
        <Link href="/dashboard/support">
          <Button variant="ghost" size="sm">
            <ArrowLeft className="h-4 w-4 mr-1" /> {t("backToTickets")}
          </Button>
        </Link>
      </div>

      <div>
        <h1 className="text-2xl font-bold">{t("createTicket")}</h1>
        <p className="text-muted-foreground">{t("createTicketDescription")}</p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <FormField
            control={form.control}
            name="category"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t("category")}</FormLabel>
                <Select onValueChange={field.onChange} defaultValue={field.value}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder={t("selectCategory")} />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem value="question">{t("categoryQuestion")}</SelectItem>
                    <SelectItem value="bug">{t("categoryBug")}</SelectItem>
                    <SelectItem value="feature">{t("categoryFeature")}</SelectItem>
                    <SelectItem value="billing">{t("categoryBilling")}</SelectItem>
                    <SelectItem value="other">{t("categoryOther")}</SelectItem>
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="subject"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t("subject")}</FormLabel>
                <FormControl>
                  <Input placeholder={t("subjectPlaceholder")} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="description"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t("descriptionLabel")}</FormLabel>
                <FormControl>
                  <Textarea
                    placeholder={t("descriptionPlaceholder")}
                    rows={6}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="flex justify-end">
            <Button type="submit" disabled={createTicket.isPending}>
              {createTicket.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              {t("submitTicket")}
            </Button>
          </div>
        </form>
      </Form>
    </div>
  )
}
