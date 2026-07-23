"use client"

import { useState, useCallback } from "react"
import { ROLES, isSuperAdmin as checkSuperAdmin } from "@/lib/constants/roles"
import {
  useAdminUsers, useBlockUser, useUnblockUser, useChangeUserRole,
  useVerifyUser, useAdminEditProfile, type AdminUserSummary,
} from "@/lib/api/queries/admin"
import { useBadges, useAwardBadge } from "@/lib/api/queries/badges"
import { useCurrentUser } from "@/hooks/use-current-user"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
  DialogDescription, DialogTrigger, DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Shield, Ban, CheckCircle, Award, Pencil } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

// ── UserTableRow sub-component ──────────────────────────────────────────────

interface UserTableRowProps {
  user: AdminUserSummary
  isSuperAdmin: boolean
  badges: { code: string; title: string }[] | undefined
  onVerify: (userId: string) => void
  onBlock: (userId: string, reason: string) => void
  onUnblock: (userId: string) => void
  onOpenEdit: (user: AdminUserSummary) => void
  onAwardBadge: (userId: string, badgeCode: string) => void
  onChangeRole: (userId: string, newRole: string) => void
}

function UserTableRow({
  user,
  isSuperAdmin,
  badges,
  onVerify,
  onBlock,
  onUnblock,
  onOpenEdit,
  onAwardBadge,
  onChangeRole,
}: UserTableRowProps) {
  const t = useTranslations("admin")
  const tPlaceholders = useTranslations("placeholders")
  const [blockReason, setBlockReason] = useState("")
  const [selectedBadge, setSelectedBadge] = useState("")

  const handleVerify = useCallback(() => onVerify(user.id), [onVerify, user.id])
  const handleBlock = useCallback(() => onBlock(user.id, blockReason), [onBlock, user.id, blockReason])
  const handleUnblock = useCallback(() => onUnblock(user.id), [onUnblock, user.id])
  const handleOpenEdit = useCallback(() => onOpenEdit(user), [onOpenEdit, user])
  const handleAwardBadge = useCallback(() => onAwardBadge(user.id, selectedBadge), [onAwardBadge, user.id, selectedBadge])
  const handleChangeRole = useCallback((v: string) => onChangeRole(user.id, v), [onChangeRole, user.id])
  const handleBlockReasonChange = useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => setBlockReason(e.target.value),
    []
  )

  return (
    <TableRow key={user.id}>
      <TableCell>
        <div className="flex items-center gap-2">
          <div>
            <div className="flex items-center gap-1">
              {user.fullName}
              {user.isVerified && <CheckCircle className="h-4 w-4 text-blue-500" />}
            </div>
            {user.username && (
              <div className="text-xs text-muted-foreground">@{user.username}</div>
            )}
          </div>
        </div>
      </TableCell>
      <TableCell>{user.email}</TableCell>
      <TableCell>
        {(user.role === ROLES.ADMIN || user.role === ROLES.SUPER_ADMIN) && !isSuperAdmin ? (
          <Badge variant="outline">{user.role}</Badge>
        ) : (
          <Select defaultValue={user.role} onValueChange={handleChangeRole}>
            <SelectTrigger className="h-8 w-[120px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ROLES.PARTICIPANT}>{t("participant")}</SelectItem>
              <SelectItem value={ROLES.CURATOR}>{t("curator")}</SelectItem>
              {isSuperAdmin && <SelectItem value={ROLES.ADMIN}>{t("admin")}</SelectItem>}
            </SelectContent>
          </Select>
        )}
      </TableCell>
      <TableCell>
        {user.isActive ? (
          <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">{t("active")}</Badge>
        ) : (
          <Badge variant="destructive">{t("blocked")}</Badge>
        )}
      </TableCell>
      <TableCell>
        <div className="flex gap-2 flex-wrap">
          {!user.isVerified && (
            <Button size="sm" variant="outline" onClick={handleVerify}>
              <CheckCircle className="h-4 w-4 mr-1" /> {t("verify")}
            </Button>
          )}

          {user.isActive ? (
            <Dialog>
              <DialogTrigger asChild>
                <Button size="sm" variant="destructive">
                  <Ban className="h-4 w-4 mr-1" /> {t("block")}
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>{t("block")}</DialogTitle>
                  <DialogDescription className="sr-only">{t("blockReason")}</DialogDescription>
                </DialogHeader>
                <div className="py-4 space-y-2">
                  <p className="text-sm text-muted-foreground">
                    <span className="text-destructive">*</span> {t("blockReason")}
                  </p>
                  <Textarea
                    placeholder={tPlaceholders("reasonForBlocking")}
                    value={blockReason}
                    onChange={handleBlockReasonChange}
                    rows={3}
                  />
                </div>
                <DialogFooter>
                  <Button
                    onClick={handleBlock}
                    variant="destructive"
                    disabled={!blockReason.trim()}
                  >
                    {t("confirmBlock")}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          ) : (
            <Button size="sm" variant="outline" onClick={handleUnblock}>
              <Shield className="h-4 w-4 mr-1" /> {t("unblock")}
            </Button>
          )}

          <Button size="sm" variant="outline" onClick={handleOpenEdit}>
            <Pencil className="h-4 w-4 mr-1" /> {t("userManagement.editProfile")}
          </Button>

          <Dialog>
            <DialogTrigger asChild>
              <Button size="sm" variant="outline">
                <Award className="h-4 w-4 mr-1" /> {t("award")}
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>{t("awardBadge")}</DialogTitle>
                <DialogDescription className="sr-only">{t("selectBadge")}</DialogDescription>
              </DialogHeader>
              <div className="py-4">
                <Select value={selectedBadge} onValueChange={setSelectedBadge}>
                  <SelectTrigger>
                    <SelectValue placeholder={tPlaceholders("selectBadge")} />
                  </SelectTrigger>
                  <SelectContent>
                    {badges?.map((badge) => (
                      <SelectItem key={badge.code} value={badge.code}>
                        {badge.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <DialogFooter>
                <Button onClick={handleAwardBadge}>{t("award")}</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      </TableCell>
    </TableRow>
  )
}

// ── Main component ───────────────────────────────────────────────────────────

function UserFilters({ roleFilter, activeFilter, onRoleChange, onActiveChange, t }: {
  roleFilter?: string; activeFilter?: boolean
  onRoleChange: (v: string) => void; onActiveChange: (v: string) => void
  t: ReturnType<typeof useTranslations>
}) {
  return (
    <div className="flex gap-4">
      <Select value={roleFilter ?? "all"} onValueChange={onRoleChange}>
        <SelectTrigger className="w-[180px]"><SelectValue placeholder={t("filterByRole")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allRoles")}</SelectItem>
          <SelectItem value={ROLES.PARTICIPANT}>{t("participant")}</SelectItem>
          <SelectItem value={ROLES.CURATOR}>{t("curator")}</SelectItem>
          <SelectItem value={ROLES.ADMIN}>{t("admin")}</SelectItem>
        </SelectContent>
      </Select>
      <Select value={activeFilter === undefined ? "all" : activeFilter ? "active" : "blocked"} onValueChange={onActiveChange}>
        <SelectTrigger className="w-[180px]"><SelectValue placeholder={t("filterByStatus")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allStatus")}</SelectItem>
          <SelectItem value="active">{t("active")}</SelectItem>
          <SelectItem value="blocked">{t("blocked")}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  )
}

function EditProfileDialog({ target, isPending, onClose, onSave, t, tCommon }: {
  target: AdminUserSummary | null
  isPending: boolean
  onClose: () => void
  onSave: (data: { fullName: string; username: string; bio: string; reason: string }) => void
  t: (key: string) => string
  tCommon: (key: string) => string
}) {
  const [fullName, setFullName] = useState(target?.fullName ?? "")
  const [username, setUsername] = useState(target?.username ?? "")
  const [bio, setBio] = useState("")
  const [reason, setReason] = useState("")

  const handleSubmit = useCallback(() => onSave({ fullName, username, bio, reason }), [onSave, fullName, username, bio, reason])
  const handleClose = useCallback((open: boolean) => { if (!open) onClose() }, [onClose])

  return (
    <Dialog open={!!target} onOpenChange={handleClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("userManagement.editProfile")}</DialogTitle>
          <DialogDescription>{target?.email}</DialogDescription>
        </DialogHeader>
        <div className="py-4 space-y-4">
          <div className="space-y-1">
            <label className="text-sm font-medium">{t("userManagement.editProfileFullName")}</label>
            <Input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Full Name" />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">{t("userManagement.editProfileUsername")}</label>
            <Input value={username} onChange={(e) => setUsername(e.target.value)} placeholder="username" />
            <p className="text-xs text-muted-foreground">Letters, digits, _ - . only. Leave blank to clear.</p>
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">{t("userManagement.editProfileBio")}</label>
            <Textarea value={bio} onChange={(e) => setBio(e.target.value)} placeholder={t("userManagement.editProfileBioPlaceholder")} rows={3} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">
              {t("userManagement.editProfileReason")} <span className="text-destructive">*</span>
            </label>
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t("userManagement.editProfileReasonPlaceholder")} rows={2} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{tCommon("cancel")}</Button>
          <Button onClick={handleSubmit} disabled={!reason.trim() || isPending}>
            {isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
            {tCommon("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function UserManagement() {
  const t = useTranslations("admin")
  const tCommon = useTranslations("common")
  const { user: currentUser } = useCurrentUser()
  const isSuperAdmin = checkSuperAdmin(currentUser?.role)
  const [page, setPage] = useState(1)
  const [roleFilter, setRoleFilter] = useState<string | undefined>(undefined)
  const [activeFilter, setActiveFilter] = useState<boolean | undefined>(undefined)
  const { data, isLoading } = useAdminUsers(page, 20, roleFilter, activeFilter)
  const { toast } = useToast()

  const blockUser = useBlockUser()
  const unblockUser = useUnblockUser()
  const changeRole = useChangeUserRole()
  const verifyUser = useVerifyUser()
  const awardBadge = useAwardBadge()
  const editProfile = useAdminEditProfile()
  const { data: badges } = useBadges()

  const [editTarget, setEditTarget] = useState<AdminUserSummary | null>(null)

  const handleBlock = useCallback(async (userId: string, reason: string) => {
    if (!reason.trim()) return
    try {
      await blockUser.mutateAsync({ userId, reason, isPermanent: false })
      toast({ title: t("userBlocked") })
    } catch {
      toast({ title: t("blockFailed"), variant: "destructive" })
    }
  }, [blockUser, toast, t])

  const handleUnblock = useCallback(async (userId: string) => {
    try {
      await unblockUser.mutateAsync(userId)
      toast({ title: t("userUnblocked") })
    } catch {
      toast({ title: t("unblockFailed"), variant: "destructive" })
    }
  }, [unblockUser, toast, t])

  const handleVerify = useCallback(async (userId: string) => {
    try {
      await verifyUser.mutateAsync(userId)
      toast({ title: t("userVerified") })
    } catch {
      toast({ title: t("verifyFailed"), variant: "destructive" })
    }
  }, [verifyUser, toast, t])

  const handleChangeRole = useCallback(async (userId: string, newRole: string) => {
    try {
      await changeRole.mutateAsync({ userId, newRole })
      toast({ title: t("roleUpdated") })
    } catch {
      toast({ title: t("roleUpdateFailed"), variant: "destructive" })
    }
  }, [changeRole, toast, t])

  const handleAwardBadge = useCallback(async (userId: string, badgeCode: string) => {
    if (!badgeCode) return
    try {
      await awardBadge.mutateAsync({ userId, achievementCode: badgeCode })
      toast({ title: t("badgeAwarded") })
    } catch {
      toast({ title: t("badgeAwardFailed"), variant: "destructive" })
    }
  }, [awardBadge, toast, t])

  const handleEditProfile = useCallback(async (data: { fullName: string; username: string; bio: string; reason: string }) => {
    if (!editTarget || !data.reason.trim()) return
    try {
      await editProfile.mutateAsync({
        userId: editTarget.id,
        fullName: data.fullName.trim() || undefined,
        username: data.username !== (editTarget.username ?? "") ? data.username.trim() : undefined,
        bio: data.bio.trim() !== "" ? data.bio.trim() : undefined,
        reason: data.reason.trim(),
      })
      toast({ title: t("userManagement.editProfileSuccess") })
      setEditTarget(null)
    } catch {
      toast({ title: tCommon("error"), variant: "destructive" })
    }
  }, [editProfile, editTarget, toast, t, tCommon])

  const openEditDialog = useCallback((user: AdminUserSummary) => setEditTarget(user), [])

  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])

  const handleRoleFilterChange = useCallback((v: string) => {
    setRoleFilter(v === "all" ? undefined : v)
  }, [])

  const handleActiveFilterChange = useCallback((v: string) => {
    setActiveFilter(v === "all" ? undefined : v === "active")
  }, [])

  if (isLoading) return <div className="flex justify-center p-8"><Loader2 className="h-8 w-8 animate-spin" /></div>

  return (
    <div className="space-y-4">
      <UserFilters roleFilter={roleFilter} activeFilter={activeFilter} onRoleChange={handleRoleFilterChange} onActiveChange={handleActiveFilterChange} t={t} />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("name")}</TableHead>
              <TableHead>{t("email")}</TableHead>
              <TableHead>{t("role")}</TableHead>
              <TableHead>{t("status")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.data.map((user) => (
              <UserTableRow
                key={user.id}
                user={user}
                isSuperAdmin={isSuperAdmin}
                badges={badges}
                onVerify={handleVerify}
                onBlock={handleBlock}
                onUnblock={handleUnblock}
                onOpenEdit={openEditDialog}
                onAwardBadge={handleAwardBadge}
                onChangeRole={handleChangeRole}
              />
            ))}
          </TableBody>
        </Table>
      </div>

      <EditProfileDialog
        target={editTarget}
        isPending={editProfile.isPending}
        onClose={() => setEditTarget(null)}
        onSave={handleEditProfile}
        t={t}
        tCommon={tCommon}
      />

      <div className="flex justify-center gap-2">
        <Button variant="outline" disabled={page === 1} onClick={handlePrevPage}>
          {tCommon("previous")}
        </Button>
        <Button variant="outline" disabled={!data || data.data.length < 20} onClick={handleNextPage}>
          {tCommon("next")}
        </Button>
      </div>
    </div>
  )
}
