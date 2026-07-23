"use client"

import { useState, useEffect, useCallback } from "react"
import { useTranslations } from "next-intl"
import {
  Plus, FileText, MoreVertical, Pencil, Trash2, Save, X,
  Sparkles, Network, Loader2, ChevronDown,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import {
  useProjectDocuments,
  useProjectDocument,
  useCreateDocument,
  useUpdateDocument,
  useDeleteDocument,
  useGeneratePassportDocs,
  useGenerateDiagramDoc,
  isAiDocument,
  type ProjectDocument,
} from "@/lib/api/queries/documents"
import { cn } from "@/lib/utils"
import { MermaidDiagram } from "@/components/ui/MermaidDiagram"
import { MarkdownWithMermaid } from "@/components/ui/MarkdownWithMermaid"

// ── Diagram type options ──

const DIAGRAM_TYPES = [
  { value: "architecture", label: "Architecture" },
  { value: "sequence", label: "Sequence" },
  { value: "erd", label: "ER Diagram" },
  { value: "user_flow", label: "User Flow" },
  { value: "deployment", label: "Deployment" },
  { value: "state", label: "State Machine" },
] as const

// ── Doc list item ──

interface DocListItemProps {
  doc: ProjectDocument
  isSelected: boolean
  canManage: boolean
  onSelect: (doc: ProjectDocument) => void
  onDeleteClick: (doc: ProjectDocument) => void
  tCommon: (key: string) => string
}

function DocListItem({ doc, isSelected, canManage, onSelect, onDeleteClick, tCommon }: DocListItemProps) {
  const handleSelect = useCallback(() => onSelect(doc), [onSelect, doc])
  const handleDelete = useCallback(() => onDeleteClick(doc), [onDeleteClick, doc])
  const handleStopPropagation = useCallback((e: React.MouseEvent) => e.stopPropagation(), [])
  const isAi = isAiDocument(doc.documentType)
  const isDiagram = doc.documentType === "ai-diagram" || doc.contentFormat === "mermaid"

  return (
    <div
      className={cn(
        "flex items-center justify-between p-2 rounded-md cursor-pointer hover:bg-accent transition-colors group",
        isSelected && "bg-accent"
      )}
      onClick={handleSelect}
    >
      <div className="flex items-center gap-2 overflow-hidden">
        {isDiagram ? (
          <Network className="h-4 w-4 flex-shrink-0 text-primary" />
        ) : isAi ? (
          <Sparkles className="h-4 w-4 flex-shrink-0 text-amber-500" />
        ) : (
          <FileText className="h-4 w-4 flex-shrink-0 text-muted-foreground" />
        )}
        <span className="text-sm truncate">{doc.title}</span>
        {isAi && (
          <Badge variant="secondary" className="text-[10px] px-1 py-0 h-4 shrink-0">
            AI
          </Badge>
        )}
      </div>
      {canManage && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="h-6 w-6 opacity-0 group-hover:opacity-100"
              onClick={handleStopPropagation}
            >
              <MoreVertical className="h-3 w-3" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={handleDelete} className="text-destructive">
              <Trash2 className="mr-2 h-3 w-3" />
              {tCommon("delete")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}

// ── Sub-components ──

interface DocsDialogsProps {
  isCreateDialogOpen: boolean; setIsCreateDialogOpen: (open: boolean) => void
  isDiagramDialogOpen: boolean; setIsDiagramDialogOpen: (open: boolean) => void
  isDeleteDialogOpen: boolean; setIsDeleteDialogOpen: (open: boolean) => void
  title: string; content: string; diagramType: string
  docToDelete: ProjectDocument | null
  createPending: boolean; diagramPending: boolean
  onTitleChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onContentChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onDiagramTypeChange: (v: string) => void
  onCreateSubmit: () => void; onGenerateDiagram: () => void; onConfirmDelete: () => void
  tCommon: ReturnType<typeof useTranslations>; tDocs: ReturnType<typeof useTranslations>
  tDialogs: ReturnType<typeof useTranslations>; tPlaceholders: ReturnType<typeof useTranslations>
  tPDocs: ReturnType<typeof useTranslations>
}

function DocsDialogs({ isCreateDialogOpen, setIsCreateDialogOpen, isDiagramDialogOpen, setIsDiagramDialogOpen, isDeleteDialogOpen, setIsDeleteDialogOpen, title, content, diagramType, docToDelete, createPending, diagramPending, onTitleChange, onContentChange, onDiagramTypeChange, onCreateSubmit, onGenerateDiagram, onConfirmDelete, tCommon, tDocs, tDialogs, tPlaceholders, tPDocs }: DocsDialogsProps) {
  return (
    <>
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>{tDialogs("createDocument")}</DialogTitle></DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{tDocs("title")}</label>
              <Input value={title} onChange={onTitleChange} placeholder={tPlaceholders("documentTitle")} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">{tDocs("content")}</label>
              <Textarea value={content} onChange={onContentChange} placeholder={tPlaceholders("initialContent")} className="h-32" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>{tCommon("cancel")}</Button>
            <Button onClick={onCreateSubmit} disabled={createPending}>{tCommon("create")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isDiagramDialogOpen} onOpenChange={setIsDiagramDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle><div className="flex items-center gap-2"><Network className="h-5 w-5" />{tPDocs("generateDiagram") || "Generate Diagram"}</div></DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{tPDocs("selectDiagramType") || "Diagram type"}</label>
              <Select value={diagramType} onValueChange={onDiagramTypeChange}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{DIAGRAM_TYPES.map((dt) => <SelectItem key={dt.value} value={dt.value}>{dt.label}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsDiagramDialogOpen(false)}>{tCommon("cancel")}</Button>
            <Button onClick={onGenerateDiagram} disabled={diagramPending}>
              {diagramPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              {tPDocs("generateDiagram") || "Generate"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{tDialogs("deleteDocument")}</AlertDialogTitle>
            <AlertDialogDescription>{tDocs("deleteConfirmDescription", { title: docToDelete?.title ?? "" })}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => setIsDeleteDialogOpen(false)}>{tCommon("cancel")}</AlertDialogCancel>
            <AlertDialogAction onClick={onConfirmDelete} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">{tCommon("delete")}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}

// ── Main component ──

interface ProjectDocsProps {
  projectId: string
  canManage: boolean
  techStack?: string
  idea?: string
}

function useProjectDocsState(projectId: string, techStack: string | undefined, idea: string | undefined) {
  const t = useTranslations()
  const tCommon = useTranslations("common")
  const tDocs = useTranslations("docs")
  const tDialogs = useTranslations("dialogs")
  const tPlaceholders = useTranslations("placeholders")
  const tPDocs = useTranslations("projectDocs")
  const { toast } = useToast()
  const { data: documents, isLoading } = useProjectDocuments(projectId)
  const createDocument = useCreateDocument()
  const updateDocument = useUpdateDocument()
  const deleteDocument = useDeleteDocument()
  const generatePassport = useGeneratePassportDocs(projectId)
  const generateDiagram = useGenerateDiagramDoc(projectId)

  const [selectedDocId, setSelectedDocId] = useState<string | null>(null)
  const { data: fullSelectedDoc, isLoading: isLoadingDoc } = useProjectDocument(projectId, selectedDocId)
  const [isEditing, setIsEditing] = useState(false)
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false)
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false)
  const [isDiagramDialogOpen, setIsDiagramDialogOpen] = useState(false)
  const [docToDelete, setDocToDelete] = useState<ProjectDocument | null>(null)
  const [diagramType, setDiagramType] = useState("architecture")
  const [title, setTitle] = useState("")
  const [content, setContent] = useState("")

  useEffect(() => { if (fullSelectedDoc) { setContent(fullSelectedDoc.content || ""); if (!isEditing) setTitle(fullSelectedDoc.title) } }, [fullSelectedDoc, isEditing])

  const handleSelectDoc = useCallback((doc: ProjectDocument) => {
    if (isEditing && !confirm(t("projectDocs.unsavedWarning") || "You have unsaved changes. Switch anyway?")) return
    setSelectedDocId(doc.id); setIsEditing(false); setTitle(doc.title); setContent("")
  }, [isEditing, t])

  const handleCreateClick = useCallback(() => { setTitle(""); setContent(""); setIsCreateDialogOpen(true) }, [])
  const handleCreateSubmit = useCallback(async () => {
    if (!title.trim()) { toast({ title: tCommon("error"), description: tPDocs("titleRequired"), variant: "destructive" }); return }
    try { await createDocument.mutateAsync({ projectId, data: { title, content: content || tPDocs("startWriting"), documentType: "general", isPublic: true } }); toast({ title: tCommon("success"), description: tPDocs("documentCreated") }); setIsCreateDialogOpen(false); setTitle(""); setContent("") }
    catch { toast({ title: tCommon("error"), description: tPDocs("documentCreateFailed"), variant: "destructive" }) }
  }, [title, content, projectId, createDocument, toast, tCommon, tPDocs])

  const handleEditClick = useCallback(() => { if (!fullSelectedDoc) return; setTitle(fullSelectedDoc.title); setContent(fullSelectedDoc.content || ""); setIsEditing(true) }, [fullSelectedDoc])
  const handleCancelEdit = useCallback(() => { if (!fullSelectedDoc) return; setTitle(fullSelectedDoc.title); setContent(fullSelectedDoc.content || ""); setIsEditing(false) }, [fullSelectedDoc])
  const handleSaveEdit = useCallback(async () => {
    if (!selectedDocId || !title.trim()) { toast({ title: tCommon("error"), description: tPDocs("titleRequired"), variant: "destructive" }); return }
    try { await updateDocument.mutateAsync({ projectId, documentId: selectedDocId, data: { title, content } }); toast({ title: tCommon("success"), description: tPDocs("documentUpdated") }); setIsEditing(false) }
    catch { toast({ title: tCommon("error"), description: tPDocs("documentUpdateFailed"), variant: "destructive" }) }
  }, [selectedDocId, title, content, projectId, updateDocument, toast, tCommon, tPDocs])

  const handleDeleteClick = useCallback((doc: ProjectDocument) => { setDocToDelete(doc); setIsDeleteDialogOpen(true) }, [])
  const handleTitleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setTitle(e.target.value), [])
  const handleContentChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setContent(e.target.value), [])
  const handleOpenDiagramDialog = useCallback(() => setIsDiagramDialogOpen(true), [])
  const handleConfirmDelete = useCallback(async () => {
    if (!docToDelete) return
    try { await deleteDocument.mutateAsync({ projectId, documentId: docToDelete.id }); toast({ title: tCommon("success"), description: tPDocs("documentDeleted") }); if (selectedDocId === docToDelete.id) { setSelectedDocId(null); setIsEditing(false) }; setIsDeleteDialogOpen(false); setDocToDelete(null) }
    catch { toast({ title: tCommon("error"), description: tPDocs("documentDeleteFailed"), variant: "destructive" }) }
  }, [docToDelete, selectedDocId, projectId, deleteDocument, toast, tCommon, tPDocs])

  const handleGeneratePassport = useCallback(async () => {
    try { await generatePassport.mutateAsync(); toast({ title: tCommon("success"), description: tPDocs("passportGenerated") || "AI passport sections created" }) }
    catch { toast({ title: tCommon("error"), description: "Failed to generate passport", variant: "destructive" }) }
  }, [generatePassport, toast, tCommon, tPDocs])

  const handleGenerateDiagram = useCallback(async () => {
    try { await generateDiagram.mutateAsync({ techStack: techStack || "Not specified", idea, diagramType }); toast({ title: tCommon("success"), description: tPDocs("diagramGenerated") || "Diagram saved as document" }); setIsDiagramDialogOpen(false) }
    catch { toast({ title: tCommon("error"), description: "Failed to generate diagram", variant: "destructive" }) }
  }, [generateDiagram, techStack, idea, diagramType, toast, tCommon, tPDocs])

  const isGenerating = generatePassport.isPending || generateDiagram.isPending
  const selectedDoc = documents?.find((d) => d.id === selectedDocId)
  const isMermaidDoc = selectedDoc?.contentFormat === "mermaid" || selectedDoc?.documentType === "ai-diagram"
  return { t, tCommon, tDocs, tDialogs, tPlaceholders, tPDocs, documents, isLoading, selectedDocId, setSelectedDocId, fullSelectedDoc, isLoadingDoc, isEditing, setIsEditing, isCreateDialogOpen, setIsCreateDialogOpen, isDeleteDialogOpen, setIsDeleteDialogOpen, isDiagramDialogOpen, setIsDiagramDialogOpen, docToDelete, diagramType, setDiagramType, title, content, generatePassport, generateDiagram, createDocument, isGenerating, selectedDoc, isMermaidDoc, handleSelectDoc, handleCreateClick, handleCreateSubmit, handleEditClick, handleCancelEdit, handleSaveEdit, handleDeleteClick, handleTitleChange, handleContentChange, handleOpenDiagramDialog, handleConfirmDelete, handleGeneratePassport, handleGenerateDiagram }
}

type DocsCardProps = ReturnType<typeof useProjectDocsState> & { canManage: boolean }

function DocsSidebarCard(s: DocsCardProps) {
  const { tPDocs, tCommon, tDocs, documents, isLoading, selectedDocId, isGenerating, generatePassport, canManage } = s
  return (
    <Card className="md:col-span-1 flex flex-col h-full border-border/70 bg-card/80 shadow-sm">
      <CardHeader className="p-4 border-b">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">{tPDocs("documents")}</CardTitle>
          {canManage && (
            <div className="flex items-center gap-1">
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button size="sm" variant="ghost" disabled={isGenerating}>
                    {isGenerating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
                    <ChevronDown className="h-3 w-3 ml-1" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={s.handleGeneratePassport} disabled={generatePassport.isPending}>
                    <Sparkles className="mr-2 h-3 w-3" />{tPDocs("generatePassport") || "Generate Passport"}
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={s.handleOpenDiagramDialog}>
                    <Network className="mr-2 h-3 w-3" />{tPDocs("generateDiagram") || "Generate Diagram"}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
              <Button size="sm" variant="ghost" onClick={s.handleCreateClick}><Plus className="h-4 w-4" /></Button>
            </div>
          )}
        </div>
      </CardHeader>
      <CardContent className="p-0 flex-1 overflow-hidden">
        <ScrollArea className="h-full">
          <div className="flex flex-col p-2 gap-1">
            {isLoading ? (
              <p className="text-sm text-muted-foreground p-4">{tCommon("loading")}</p>
            ) : documents?.length === 0 ? (
              <div className="flex flex-col items-center gap-3 p-6 text-center">
                <FileText className="h-8 w-8 text-muted-foreground/30" />
                <p className="text-sm text-muted-foreground">{tDocs("noDocuments")}</p>
                {canManage && (<Button size="sm" variant="outline" onClick={s.handleGeneratePassport} disabled={isGenerating}><Sparkles className="h-3 w-3 mr-2" />{tPDocs("generatePassport") || "Generate Passport"}</Button>)}
              </div>
            ) : (
              documents?.map((doc) => (
                <DocListItem key={doc.id} doc={doc} isSelected={selectedDocId === doc.id} canManage={canManage} onSelect={s.handleSelectDoc} onDeleteClick={s.handleDeleteClick} tCommon={tCommon} />
              ))
            )}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  )
}

function DocsMainCard(s: DocsCardProps) {
  const { t, tCommon, tDocs, tPlaceholders, selectedDoc, isEditing, title, content, isLoadingDoc, isMermaidDoc, canManage } = s
  function renderDocContent() {
    if (isLoadingDoc) return <div className="flex items-center justify-center h-32 text-muted-foreground">{tDocs("loadingContent")}</div>
    if (isMermaidDoc) return <MermaidDiagram code={content} />
    return <MarkdownWithMermaid content={content} />
  }
  function renderEditor() {
    if (isMermaidDoc) return (
      <div className="grid grid-cols-2 h-full divide-x">
        <Textarea value={content} onChange={s.handleContentChange} className="w-full h-full resize-none p-4 border-0 focus-visible:ring-0 rounded-none font-mono text-xs" placeholder="flowchart TD&#10;    A --> B" />
        <ScrollArea className="h-full"><div className="p-4"><MermaidDiagram code={content} /></div></ScrollArea>
      </div>
    )
    return <Textarea value={content} onChange={s.handleContentChange} className="w-full h-full resize-none p-4 border-0 focus-visible:ring-0 rounded-none font-mono text-sm" placeholder={tPlaceholders("writeDocumentation")} />
  }
  return (
    <Card className="md:col-span-3 flex flex-col h-full border-border/70 bg-card/80 shadow-sm">
      {selectedDoc ? (
        <>
          <CardHeader className="p-4 border-b flex flex-row items-center justify-between space-y-0">
            {isEditing ? (
              <Input value={title} onChange={s.handleTitleChange} className="text-lg font-semibold h-auto py-1 px-2" />
            ) : (
              <div>
                <div className="flex items-center gap-2">
                  <CardTitle>{selectedDoc.title}</CardTitle>
                  {isAiDocument(selectedDoc.documentType) && (<Badge variant="secondary" className="text-xs"><Sparkles className="h-3 w-3 mr-1" />AI</Badge>)}
                </div>
                <CardDescription>{t("common.lastUpdated") || "Last updated"}: {new Date(selectedDoc.updatedAt).toLocaleDateString()}</CardDescription>
              </div>
            )}
            {canManage && (
              <div className="flex items-center gap-2">
                {isEditing ? (
                  <>
                    <Button size="sm" variant="ghost" onClick={s.handleCancelEdit}><X className="h-4 w-4 mr-2" />{tCommon("cancel")}</Button>
                    <Button size="sm" onClick={s.handleSaveEdit}><Save className="h-4 w-4 mr-2" />{tCommon("save")}</Button>
                  </>
                ) : (
                  <Button size="sm" variant="outline" onClick={s.handleEditClick}><Pencil className="h-4 w-4 mr-2" />{tCommon("edit")}</Button>
                )}
              </div>
            )}
          </CardHeader>
          <CardContent className="p-0 flex-1 overflow-hidden">
            {isEditing ? renderEditor() : <ScrollArea className="h-full"><div className="p-6">{renderDocContent()}</div></ScrollArea>}
          </CardContent>
        </>
      ) : (
        <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
          <FileText className="h-12 w-12 mb-4 opacity-20" /><p>{tDocs("selectDocumentToView")}</p>
        </div>
      )}
    </Card>
  )
}

export function ProjectDocs({ projectId, canManage, techStack, idea }: Readonly<ProjectDocsProps>) {
  const s = useProjectDocsState(projectId, techStack, idea)

  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-6 h-[600px]">
      <DocsSidebarCard {...s} canManage={canManage} />
      <DocsMainCard {...s} canManage={canManage} />
      <DocsDialogs
        isCreateDialogOpen={s.isCreateDialogOpen} setIsCreateDialogOpen={s.setIsCreateDialogOpen}
        isDiagramDialogOpen={s.isDiagramDialogOpen} setIsDiagramDialogOpen={s.setIsDiagramDialogOpen}
        isDeleteDialogOpen={s.isDeleteDialogOpen} setIsDeleteDialogOpen={s.setIsDeleteDialogOpen}
        title={s.title} content={s.content} diagramType={s.diagramType}
        docToDelete={s.docToDelete}
        createPending={s.createDocument.isPending} diagramPending={s.generateDiagram.isPending}
        onTitleChange={s.handleTitleChange} onContentChange={s.handleContentChange}
        onDiagramTypeChange={s.setDiagramType}
        onCreateSubmit={s.handleCreateSubmit} onGenerateDiagram={s.handleGenerateDiagram} onConfirmDelete={s.handleConfirmDelete}
        tCommon={s.tCommon} tDocs={s.tDocs} tDialogs={s.tDialogs} tPlaceholders={s.tPlaceholders} tPDocs={s.tPDocs}
      />
    </div>
  )
}
