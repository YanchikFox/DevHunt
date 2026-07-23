"use client"

import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
    DialogDescription,
} from "@/components/ui/dialog"
import { Label } from "@/components/ui/label"
import { Badge } from "@/components/ui/badge"
import { Briefcase, Clock, Calendar, Users, Award, Send } from "lucide-react"
import type { UseFormRegister, FieldErrors } from "react-hook-form"
import type { Internship } from "@/lib/api/schema"

interface InternshipCardContentProps {
    readonly internship: Internship
    readonly onSubmit: (e: React.FormEvent) => void
    readonly register: UseFormRegister<any>
    readonly errors: FieldErrors<any>
}

export function InternshipCardDetails({ internship }: { readonly internship: Internship }) {
    const t = useTranslations()

    return (
        <div className="space-y-4">
            {internship.company && (
                <div className="flex items-center gap-2 text-sm">
                    <Briefcase className="h-4 w-4 text-muted-foreground" />
                    <span className="font-medium">{internship.company.name}</span>
                </div>
            )}

            <div className="space-y-2 text-sm">
                <div className="flex items-center gap-2 text-muted-foreground">
                    <Clock className="h-4 w-4" />
                    <span>{internship.duration}</span>
                </div>
                <div className="flex items-center gap-2 text-muted-foreground">
                    <Calendar className="h-4 w-4" />
                    <span>
                        {t("internships.deadline")}: {new Date(internship.deadline).toLocaleDateString()}
                    </span>
                </div>
                {internship.maxParticipants && (
                    <div className="flex items-center gap-2 text-muted-foreground">
                        <Users className="h-4 w-4" />
                        <span>{t("internships.upToParticipants", { count: internship.maxParticipants })}</span>
                    </div>
                )}
            </div>

            {internship.requirements.length > 0 && (
                <div>
                    <p className="text-sm font-medium mb-2">{t("internships.requirements")}</p>
                    <div className="flex flex-wrap gap-1">
                        {internship.requirements.slice(0, 3).map((req: string) => (
                            <Badge key={req} variant="outline" className="text-xs">
                                {req}
                            </Badge>
                        ))}
                        {internship.requirements.length > 3 && (
                            <Badge variant="outline" className="text-xs">
                                +{internship.requirements.length - 3}
                            </Badge>
                        )}
                    </div>
                </div>
            )}

            {internship.benefits && internship.benefits.length > 0 && (
                <div>
                    <p className="text-sm font-medium mb-2">{t("internships.benefits")}</p>
                    <ul className="text-sm text-muted-foreground space-y-1">
                        {internship.benefits.slice(0, 2).map((benefit: string) => (
                            <li key={benefit} className="flex items-center gap-2">
                                <Award className="h-3 w-3" />
                                {benefit}
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </div>
    )
}

export function InternshipApplicationDialog({
    internship,
    onSubmit,
    register,
    errors,
}: InternshipCardContentProps) {
    const t = useTranslations()

    return (
        <Dialog>
            <DialogTrigger asChild>
                <Button size="sm" className="w-full group">
                    <Send className="mr-2 h-4 w-4 group-hover:translate-x-1 transition-transform" />
                    {t("internships.apply")}
                </Button>
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>
                        {t("internships.submitApplicationFor", { title: internship.title })}
                    </DialogTitle>
                    <DialogDescription>{t("internships.submitApplicationDescription")}</DialogDescription>
                </DialogHeader>
                <form onSubmit={onSubmit} className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="message">{t("teams.applicationMessage")}</Label>
                        <textarea
                            id="message"
                            {...register("message")}
                            rows={6}
                            className="flex min-h-[120px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                            placeholder={t("internships.tellUsAboutYourself")}
                            aria-invalid={errors.message ? "true" : "false"}
                        />
                        {errors.message && (
                            <p className="text-sm text-destructive" role="alert">
                                {errors.message?.message as string}
                            </p>
                        )}
                    </div>
                    <div className="flex justify-end gap-2">
                        <Button type="submit">{t("common.submit")}</Button>
                    </div>
                </form>
            </DialogContent>
        </Dialog>
    )
}
