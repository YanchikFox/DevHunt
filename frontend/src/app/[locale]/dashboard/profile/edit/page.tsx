"use client"

import { useCallback, useEffect, useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"

import { ProfileSettingsNav } from "@/components/profile/ProfileSettingsNav"
import { AvatarUploadSection } from "@/components/profile/AvatarUploadSection"
import { SkillsChipsTypeahead } from "@/components/profile/SkillsChipsTypeahead"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Textarea } from "@/components/ui/textarea"

import { useToast } from "@/hooks/use-toast"
import { useRouter } from "@/i18n/routing"
import { useProfile, useUpdateProfile } from "@/lib/api/queries/profile"
import { useCheckUsername } from "@/lib/api/queries/auth"
import { Check, X, Loader2 } from "lucide-react"

const USERNAME_FORMAT = /^[a-zA-Z0-9_\-.]+$/

const editProfileSchema = z.object({
  fullName: z.string().min(2, "Full name must be at least 2 characters"),
  bio: z.string().max(500, "Bio must be less than 500 characters").optional().or(z.literal("")),
  experience: z.number().min(0).max(50).optional(),
  github: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  githubUsername: z.string().regex(/^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$/, "Must be a valid GitHub username").optional().or(z.literal("")),
  linkedin: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  website: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  username: z.string()
    .regex(USERNAME_FORMAT, "Only letters, digits, underscore, hyphen, dot")
    .min(3, "At least 3 characters")
    .max(50)
    .optional()
    .or(z.literal("")),
})

type EditProfileForm = z.infer<typeof editProfileSchema>

export default function EditProfilePage() {
  const { data: profile, isLoading, error } = useProfile()
  const updateProfile = useUpdateProfile()
  const { toast } = useToast()
  const router = useRouter()

  const [skills, setSkills] = useState<string[]>([])
  const [rawUsername, setRawUsername] = useState("")
  const [debouncedUsername, setDebouncedUsername] = useState("")

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<EditProfileForm>({
    resolver: zodResolver(editProfileSchema),
    defaultValues: {
      fullName: "",
      bio: "",
      experience: undefined,
      github: "",
      githubUsername: "",
      linkedin: "",
      website: "",
      username: "",
    },
  })

  useEffect(() => {
    if (!profile) return
    reset({
      fullName: profile.fullName ?? "",
      bio: profile.bio ?? "",
      experience: profile.experience ?? undefined,
      github: profile.github ?? "",
      githubUsername: profile.githubUsername ?? "",
      linkedin: profile.linkedin ?? "",
      website: profile.website ?? "",
      username: profile.username ?? "",
    })
    setRawUsername(profile.username ?? "")
    setSkills(Array.isArray(profile.skills) ? profile.skills : [])
  }, [profile, reset])

  // Debounce username 400ms
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedUsername(rawUsername), 400)
    return () => clearTimeout(timer)
  }, [rawUsername])

  // Only check if username changed from the profile's current value
  const currentUsername = profile?.username ?? ""
  const usernameChanged = debouncedUsername !== currentUsername
  const { data: usernameCheck, isFetching: checkingUsername } = useCheckUsername(
    usernameChanged ? debouncedUsername : ""
  )

  const handleUsernameChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setRawUsername(e.target.value)
  }, [])

  const onSubmit = useCallback(
    async (data: EditProfileForm) => {
      try {
        const experienceValue =
          typeof data.experience === "number" && Number.isFinite(data.experience)
            ? data.experience
            : undefined

        // username: null clears it, undefined means "no change" — use null for empty input
        const usernameValue = data.username?.trim()
          ? data.username.trim()
          : data.username === ""
            ? null
            : undefined

        await updateProfile.mutateAsync({
          fullName: data.fullName.trim(),
          bio: data.bio?.trim() ? data.bio.trim() : undefined,
          skills: skills.length > 0 ? skills : undefined,
          experience: experienceValue,
          github: data.github?.trim() ? data.github.trim() : undefined,
          githubUsername: data.githubUsername?.trim() ? data.githubUsername.trim() : undefined,
          linkedin: data.linkedin?.trim() ? data.linkedin.trim() : undefined,
          website: data.website?.trim() ? data.website.trim() : undefined,
          username: usernameValue,
        })

        toast({ title: "Profile updated" })
        router.push("/dashboard/profile")
      } catch (error: unknown) {
        const err = error as { userMessage?: string; message?: string }
        toast({
          title: "Failed to save",
          description: err.userMessage || err.message || "Please try again",
          variant: "destructive",
        })
      }
    },
    [updateProfile, skills, toast, router]
  )

  const handleCancel = useCallback(() => {
    router.push("/dashboard/profile")
  }, [router])

  const usernameStatusNode = () => {
    if (!usernameChanged || debouncedUsername.length < 3) return null
    if (!USERNAME_FORMAT.test(debouncedUsername)) return null
    if (checkingUsername) return <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
    if (usernameCheck?.available === true) return <Check className="h-4 w-4 text-green-500" />
    if (usernameCheck?.available === false) return <X className="h-4 w-4 text-destructive" />
    return null
  }

  const usernameHintNode = () => {
    if (!usernameChanged || debouncedUsername.length < 3 || errors.username) return null
    if (!USERNAME_FORMAT.test(debouncedUsername)) return null
    if (checkingUsername) return <p className="text-xs text-muted-foreground">Checking…</p>
    if (usernameCheck?.available === true) return <p className="text-xs text-green-600">Available</p>
    if (usernameCheck?.available === false) return <p className="text-xs text-destructive">Already taken</p>
    return null
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-56" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (error || !profile) {
    return (
      <Card>
        <CardContent className="p-8 text-center">
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : "Failed to load profile"}
          </p>
        </CardContent>
      </Card>
    )
  }

  return (
    <div className="max-w-3xl space-y-5 fade-in">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <span className="caption mb-1.5 block">[Settings]</span>
          <h1 className="font-serif text-[28px] font-normal tracking-[-0.4px] leading-none text-foreground">Edit Profile</h1>
          <p className="text-[13px] text-muted-foreground mt-1.5">Update your name, avatar, and profile details.</p>
        </div>
        <ProfileSettingsNav />
      </div>

      <AvatarUploadSection
        profile={{
          fullName: profile.fullName,
          email: profile.email,
          avatarUrl: profile.avatarUrl,
        }}
      />

      <div className="rounded-[14px] border border-border bg-card">
        <div className="px-4 py-3 border-b border-border">
          <span className="caption">[Profile details]</span>
          <p className="text-[12px] text-muted-foreground mt-0.5">These details are shown on your profile.</p>
        </div>
        <div className="p-4">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div className="space-y-2">
              <Label htmlFor="fullName">Full name</Label>
              <Input
                id="fullName"
                autoComplete="name"
                {...register("fullName")}
                aria-invalid={errors.fullName ? "true" : "false"}
                className={errors.fullName ? "border-destructive" : ""}
              />
              {errors.fullName && <p className="text-sm text-destructive">{errors.fullName.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="username">
                Username <span className="text-xs text-muted-foreground">(optional)</span>
              </Label>
              <div className="relative">
                <Input
                  id="username"
                  placeholder="cool_dev42"
                  autoComplete="username"
                  {...register("username", { onChange: handleUsernameChange })}
                  aria-invalid={errors.username ? "true" : "false"}
                  className={`pr-9 ${errors.username ? "border-destructive" : ""}`}
                />
                <div className="absolute right-3 top-1/2 -translate-y-1/2">
                  {usernameStatusNode()}
                </div>
              </div>
              <p className="text-xs text-muted-foreground">Letters, digits, underscore, hyphen, dot. Leave empty to remove.</p>
              {errors.username
                ? <p className="text-sm text-destructive">{errors.username.message}</p>
                : usernameHintNode()
              }
            </div>

            <div className="space-y-2">
              <Label htmlFor="bio">Bio</Label>
              <Textarea id="bio" maxLength={500} className="min-h-[100px] resize-none" {...register("bio")} />
              {errors.bio && <p className="text-sm text-destructive">{errors.bio.message}</p>}
            </div>

            <SkillsChipsTypeahead
              inputId="skills"
              label="Skills"
              placeholder="e.g., React, Python, UI/UX"
              skills={skills}
              setSkills={setSkills}
              maxSkills={20}
            />

            <div className="space-y-2">
              <Label htmlFor="experience">Years of experience</Label>
              <Input
                id="experience"
                type="number"
                min="0"
                max="50"
                {...register("experience", { valueAsNumber: true })}
              />
              {errors.experience && <p className="text-sm text-destructive">{errors.experience.message}</p>}
            </div>

            <div className="space-y-3 pt-3 border-t border-border">
              <span className="caption">[Social links]</span>

              <div className="space-y-2">
                <Label htmlFor="github">GitHub Profile URL</Label>
                <Input
                  id="github"
                  placeholder="https://github.com/username"
                  {...register("github")}
                  aria-invalid={errors.github ? "true" : "false"}
                  className={errors.github ? "border-destructive" : ""}
                />
                {errors.github && <p className="text-sm text-destructive">{errors.github.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="githubUsername">GitHub Username (for issue sync)</Label>
                <Input
                  id="githubUsername"
                  placeholder="octocat"
                  {...register("githubUsername")}
                  aria-invalid={errors.githubUsername ? "true" : "false"}
                  className={errors.githubUsername ? "border-destructive" : ""}
                />
                <p className="text-xs text-muted-foreground">Used to assign GitHub issues to you when tasks are synced</p>
                {errors.githubUsername && <p className="text-sm text-destructive">{errors.githubUsername.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="linkedin">LinkedIn</Label>
                <Input
                  id="linkedin"
                  placeholder="https://linkedin.com/in/username"
                  {...register("linkedin")}
                  aria-invalid={errors.linkedin ? "true" : "false"}
                  className={errors.linkedin ? "border-destructive" : ""}
                />
                {errors.linkedin && <p className="text-sm text-destructive">{errors.linkedin.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="website">Website</Label>
                <Input
                  id="website"
                  placeholder="https://example.com"
                  {...register("website")}
                  aria-invalid={errors.website ? "true" : "false"}
                  className={errors.website ? "border-destructive" : ""}
                />
                {errors.website && <p className="text-sm text-destructive">{errors.website.message}</p>}
              </div>
            </div>

            <div className="flex flex-col gap-2.5 pt-3 sm:flex-row border-t border-border">
              <Button type="submit" disabled={updateProfile.isPending} size="sm" className="h-8 text-[12px] rounded-[8px] sm:w-auto">
                {updateProfile.isPending ? "Saving…" : "Save changes"}
              </Button>
              <Button type="button" variant="outline" onClick={handleCancel} size="sm" className="h-8 text-[12px] rounded-[8px] sm:w-auto">
                Cancel
              </Button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
