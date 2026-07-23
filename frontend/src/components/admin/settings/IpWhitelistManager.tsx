"use client"

import { useState, useCallback } from "react"
import { useIpWhitelist, useAddIpToWhitelist, useRemoveIpFromWhitelist } from "@/lib/api/queries/superadmin"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Loader2, Plus, Trash2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

export function IpWhitelistManager() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const { data: entries, isLoading } = useIpWhitelist()
  const addIp = useAddIpToWhitelist()
  const removeIp = useRemoveIpFromWhitelist()

  const [newIp, setNewIp] = useState("")
  const [password, setPassword] = useState("")
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)
  const [deletePassword, setDeletePassword] = useState("")

  const handleAdd = useCallback(async () => {
    if (!newIp.trim() || !password) return
    try {
      await addIp.mutateAsync({ ip: newIp.trim(), confirmPassword: password })
      toast({ title: t("ipAdded") })
      setNewIp("")
      setPassword("")
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [newIp, password, addIp, toast, t])

  const handleRemove = useCallback(async () => {
    if (!deleteTarget || !deletePassword) return
    try {
      await removeIp.mutateAsync({ ip: deleteTarget, confirmPassword: deletePassword })
      toast({ title: t("ipRemoved") })
      setDeleteTarget(null)
      setDeletePassword("")
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [deleteTarget, deletePassword, removeIp, toast, t])

  const handleNewIpChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setNewIp(e.target.value)
  }, [])

  const handlePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value)
  }, [])

  const handleDeletePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDeletePassword(e.target.value)
  }, [])

  const handleCloseDeleteDialog = useCallback((open: boolean) => {
    if (!open) {
      setDeleteTarget(null)
      setDeletePassword("")
    }
  }, [])

  const handleCancelDelete = useCallback(() => {
    setDeleteTarget(null)
  }, [])

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">{t("ipWhitelist")}</h3>

      {/* Add form */}
      <div className="flex items-center gap-3">
        <Input placeholder="192.168.1.1" value={newIp} onChange={handleNewIpChange} className="w-48" />
        <Input type="password" placeholder={t("confirmPassword")} value={password} onChange={handlePasswordChange} className="w-48" />
        <Button onClick={handleAdd} disabled={addIp.isPending || !newIp.trim() || !password} size="sm">
          {addIp.isPending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : <Plus className="h-4 w-4 mr-1" />}
          {t("addIp")}
        </Button>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-4">
          <Loader2 className="h-5 w-5 animate-spin" />
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>IP</TableHead>
              <TableHead>{t("addedAt")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {(entries ?? []).map((entry) => (
              <IpRow key={entry.ip} ip={entry.ip} updatedAt={entry.updatedAt} onDelete={setDeleteTarget} />
            ))}
            {(entries ?? []).length === 0 && (
              <TableRow>
                <TableCell colSpan={3} className="text-center text-muted-foreground py-4">
                  {t("noWhitelistedIps")}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}

      {/* Delete Confirmation */}
      <Dialog open={deleteTarget !== null} onOpenChange={handleCloseDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("removeIp")}</DialogTitle>
            <DialogDescription>{t("removeIpConfirm", { ip: deleteTarget ?? "" })}</DialogDescription>
          </DialogHeader>
          <Input
            type="password"
            placeholder={t("confirmPassword")}
            value={deletePassword}
            onChange={handleDeletePasswordChange}
          />
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelDelete}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={handleRemove} disabled={removeIp.isPending || !deletePassword}>
              {removeIp.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("remove")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function IpRow({ ip, updatedAt, onDelete }: { ip: string; updatedAt: string; onDelete: (ip: string) => void }) {
  const handleDelete = useCallback(() => onDelete(ip), [ip, onDelete])

  return (
    <TableRow>
      <TableCell className="font-mono text-sm">{ip}</TableCell>
      <TableCell className="text-sm text-muted-foreground">{new Date(updatedAt).toLocaleDateString()}</TableCell>
      <TableCell>
        <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      </TableCell>
    </TableRow>
  )
}
