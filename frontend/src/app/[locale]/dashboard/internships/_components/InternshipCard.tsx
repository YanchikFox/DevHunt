"use client"

import { useTranslations } from "next-intl"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { MapPin } from "lucide-react"
import type { UseFormRegister, FieldErrors } from "react-hook-form"
import type { Internship } from "@/lib/api/schema"
import { InternshipCardDetails, InternshipApplicationDialog } from "./InternshipCardContent"

export interface InternshipCardProps {
    readonly internship: Internship
    readonly getTypeColor: (type: string) => string
    readonly getTypeLabel: (type: string) => string
    readonly onSubmit: (e: React.FormEvent) => void
    readonly register: UseFormRegister<any>
    readonly errors: FieldErrors<any>
}

export function InternshipCard({
    internship,
    getTypeColor,
    getTypeLabel,
    onSubmit,
    register,
    errors,
}: InternshipCardProps) {
    const t = useTranslations()

    return (
        <Card className="group transition hover:shadow-lg">
            <CardHeader>
                <div className="flex items-start justify-between mb-2">
                    <Badge className={getTypeColor(internship.type)}>{getTypeLabel(internship.type)}</Badge>
                    {internship.isRemote && (
                        <Badge variant="outline">
                            <MapPin className="mr-1 h-3 w-3" />
                            {t("internships.remote")}
                        </Badge>
                    )}
                </div>
                <CardTitle>{internship.title}</CardTitle>
                <CardDescription className="line-clamp-2">{internship.description}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
                <InternshipCardDetails internship={internship} />
                <InternshipApplicationDialog
                    internship={internship}
                    onSubmit={onSubmit}
                    register={register}
                    errors={errors}
                />
            </CardContent>
        </Card>
    )
}
