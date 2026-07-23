import { z } from "zod"

export const updateProfileSchema = z.object({
  fullName: z.string().min(2, "Name must be at least 2 characters").max(100).optional(),
  bio: z.string().max(500, "Bio must be less than 500 characters").optional(),
  timezone: z.string().optional(),
  skills: z.string().optional(),
  experience: z.number().min(0).max(50).optional(),
  github: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  linkedin: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  website: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  language: z.string().optional(),
})

export type UpdateProfileForm = z.infer<typeof updateProfileSchema>
