export const ROLES = {
  PARTICIPANT: "participant",
  COMPANY: "company",
  CURATOR: "curator",
  ADMIN: "admin",
  SUPER_ADMIN: "superadmin",
} as const

export type UserRole = (typeof ROLES)[keyof typeof ROLES]

export function isAdminOrCurator(role?: string): boolean {
  return role === ROLES.ADMIN || role === ROLES.CURATOR || role === ROLES.SUPER_ADMIN
}

export function isSuperAdmin(role?: string): boolean {
  return role === ROLES.SUPER_ADMIN
}
