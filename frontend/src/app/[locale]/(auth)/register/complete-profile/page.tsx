"use client"

import { useState, useCallback } from "react"
import { useTranslations } from "next-intl"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { useUpdateProfile } from "@/lib/api/queries/profile"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { useRouter } from "@/i18n/routing"
import { useToast } from "@/hooks/use-toast"
import { UserCog, Briefcase, Github, Linkedin, Globe, ArrowRight, SkipForward } from "lucide-react"
import { SkillsChipsTypeahead } from "@/components/profile/SkillsChipsTypeahead"

const completeProfileSchema = z.object({
  bio: z.string().max(500, "Bio must be less than 500 characters").optional(),
  skills: z.string().optional(),
  experience: z.number().min(0).max(50).optional(),
  github: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  linkedin: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  website: z.string().url("Must be a valid URL").optional().or(z.literal("")),
})

type CompleteProfileForm = z.infer<typeof completeProfileSchema>

export default function CompleteProfilePage() {
  const t = useTranslations()
  const router = useRouter()
  const { toast } = useToast()
  const updateProfile = useUpdateProfile()
  const [skills, setSkills] = useState<string[]>([])

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CompleteProfileForm>({
    resolver: zodResolver(completeProfileSchema),
  })

  const onSubmit = async (data: CompleteProfileForm) => {
    try {
      await updateProfile.mutateAsync({
        bio: data.bio,
        skills: skills.length > 0 ? skills : undefined,
        experience: data.experience,
        github: data.github || undefined,
        linkedin: data.linkedin || undefined,
        website: data.website || undefined,
      })

      toast({
        title: t("common.success"),
        description: "Profile completed successfully!",
      })

      router.push("/dashboard")
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : "Failed to update profile",
        variant: "destructive",
      })
    }
  }

  const handleSkip = useCallback(() => {
    router.push("/dashboard")
  }, [router])

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
      <Card className="w-full max-w-2xl shadow-xl">
        <CardHeader className="space-y-2 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <UserCog className="h-6 w-6 text-primary" />
          </div>
          <CardTitle className="text-3xl font-bold">Complete Your Profile</CardTitle>
          <CardDescription className="text-base">
            Help others get to know you better (you can skip this step and fill it later)
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            {/* Bio */}
            <div className="space-y-2">
              <Label htmlFor="bio">Bio</Label>
              <Textarea
                id="bio"
                placeholder="Tell us about yourself, your interests, and what you're passionate about..."
                className="min-h-[100px] resize-none"
                maxLength={500}
                {...register("bio")}
              />
              {errors.bio && <p className="text-sm text-destructive">{errors.bio.message}</p>}
            </div>

            {/* Skills */}
            <SkillsChipsTypeahead skills={skills} setSkills={setSkills} />

            {/* Experience */}
            <div className="space-y-2">
              <Label htmlFor="experience" className="flex items-center gap-2">
                <Briefcase className="h-4 w-4" />
                Years of Experience
              </Label>
              <Input
                id="experience"
                type="number"
                min="0"
                max="50"
                placeholder="e.g., 5"
                {...register("experience", { valueAsNumber: true })}
              />
              {errors.experience && (
                <p className="text-sm text-destructive">{errors.experience.message}</p>
              )}
            </div>

            {/* Social Links */}
            <div className="space-y-4">
              <h3 className="font-semibold">Social Links (optional)</h3>

              <div className="space-y-2">
                <Label htmlFor="github" className="flex items-center gap-2">
                  <Github className="h-4 w-4" />
                  GitHub
                </Label>
                <Input
                  id="github"
                  type="url"
                  placeholder="https://github.com/username"
                  {...register("github")}
                />
                {errors.github && (
                  <p className="text-sm text-destructive">{errors.github.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="linkedin" className="flex items-center gap-2">
                  <Linkedin className="h-4 w-4" />
                  LinkedIn
                </Label>
                <Input
                  id="linkedin"
                  type="url"
                  placeholder="https://linkedin.com/in/username"
                  {...register("linkedin")}
                />
                {errors.linkedin && (
                  <p className="text-sm text-destructive">{errors.linkedin.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="website" className="flex items-center gap-2">
                  <Globe className="h-4 w-4" />
                  Website
                </Label>
                <Input
                  id="website"
                  type="url"
                  placeholder="https://yourwebsite.com"
                  {...register("website")}
                />
                {errors.website && (
                  <p className="text-sm text-destructive">{errors.website.message}</p>
                )}
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex gap-3 pt-4">
              <Button type="button" variant="outline" className="flex-1" onClick={handleSkip}>
                <SkipForward className="mr-2 h-4 w-4" />
                Skip for Now
              </Button>
              <Button type="submit" className="flex-1 group" disabled={updateProfile.isPending}>
                {updateProfile.isPending ? (
                  t("common.loading")
                ) : (
                  <>
                    Complete Profile
                    <ArrowRight className="ml-2 h-4 w-4 transition-transform group-hover:translate-x-1" />
                  </>
                )}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
