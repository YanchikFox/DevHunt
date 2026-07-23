"use client"

import { useState, useRef, useEffect, useCallback, memo, useMemo } from "react"
import { Send, Loader2, Bot, Sparkles } from "lucide-react"
import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm"
import { aiChatRehypePlugins } from "@/lib/security/ai-chat-rehype-plugins"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { cn } from "@/lib/utils"
import { useTranslations, useLocale } from "next-intl"
import { useProjectContext, formatProjectContextForAI } from "@/lib/api/queries/project-context"
import { useProjectPermissions } from "@/lib/api/queries/projects"
import { usePlatformFeatureFlags } from "@/lib/api/queries/settings"
import { useToolExecutor } from "./useToolExecutor"
import { ToolConfirmDialog } from "./ToolConfirmDialog"
import { AiDisclaimerBanner, ProjectContextBanner, AgentModeBanner } from "./AiChatBanners"
import { useAiChatSuggestions } from "./useAiChatSuggestions"
import { useAiChatMessaging, type ChatMessage } from "./useAiChatMessaging"

// Markdown component renderers - extracted to avoid re-creating on every render
const markdownComponents: Partial<Components> = {
    a: ({ children, href }) => (
        <a href={href} className="text-primary underline" target="_blank" rel="noopener noreferrer">
            {children}
        </a>
    ),
    code: ({ children, className }) => {
        const isInline = !className
        return isInline ? (
            <code className="bg-background/50 px-1 py-0.5 rounded text-[13px]">{children}</code>
        ) : (
            <code className={`block bg-background/50 p-2 rounded text-[13px] overflow-x-auto ${className || ''}`}>
                {children}
            </code>
        )
    },
    p: ({ children }) => <p className="my-1">{children}</p>,
    table: ({ children }) => (
        <div className="overflow-x-auto my-2">
            <table className="min-w-full text-sm border-collapse border border-border rounded">
                {children}
            </table>
        </div>
    ),
    th: ({ children }) => (
        <th className="border border-border bg-muted/50 px-3 py-1.5 text-left font-medium">
            {children}
        </th>
    ),
    td: ({ children }) => (
        <td className="border border-border px-3 py-1.5">{children}</td>
    ),
}

function SuggestionButton({ suggestion, onSelect }: { suggestion: string; onSelect: (value: string) => void }) {
    const handleClick = useCallback(() => onSelect(suggestion), [onSelect, suggestion])
    return (
        <button
            key={suggestion}
            type="button"
            onClick={handleClick}
            className="px-3 py-1.5 text-xs bg-muted hover:bg-muted/80 rounded-full transition-colors"
        >
            {suggestion}
        </button>
    )
}

interface AiChatWindowProps {
    /** Project ID to load context for */
    projectId?: string | null
    /** Additional CSS classes */
    className?: string
    /** Enable agentic mode with tool calling (default: true) */
    enableAgent?: boolean
}

/**
 * AI Chat Window component with Agentic capabilities.
 * Provides a conversational interface with the AI Architect Assistant.
 * When projectId is provided, loads full project context (team, tasks, tech stack).
 * In agent mode, supports function calling (create/update/delete tasks, etc.)
 * 
 * SECURITY: Agent mode is only enabled for project team members.
 * Non-members can view public project info but cannot execute tools.
 */
export const AiChatWindow = memo(function AiChatWindow({
    projectId,
    className,
    enableAgent = true,
}: AiChatWindowProps) {
    const t = useTranslations()
    const locale = useLocale()
    const [input, setInput] = useState("")
    const scrollRef = useRef<HTMLDivElement>(null)

    // Platform feature flags gate
    const { data: platformFlags } = usePlatformFeatureFlags()
    const aiEnabled = platformFlags === undefined || (platformFlags["ai_features"] ?? true)

    const projectContext = useProjectContext(projectId)

    const { data: permissions } = useProjectPermissions(projectId || "")

    // User is a team member if they have a role (owner, leader, member)
    const isMember = permissions?.role !== null && permissions?.role !== undefined
    const canUseTool = isMember && permissions?.canManageTasks

    // Only enable agent mode for team members with appropriate permissions
    const agentEnabled = enableAgent && isMember && canUseTool

    const contextString = useMemo(
        () => formatProjectContextForAI(projectContext, locale, isMember),
        [projectContext, locale, isMember]
    )

    // Tool executor - only used when user is a member
    const toolExecutor = useToolExecutor({
        projectId: projectId || "",
        onSuccess: () => {
            // success — no logging needed in production
        },
        onError: (result) => {
            console.error("Tool execution failed:", result)
        },
    })

    // Use messaging hook to handle state and logic
    const {
        messages,
        isLoading,
        error,
        pendingToolCalls,
        showToolConfirm,
        sendMessage,
        handleToolsConfirmed,
        handleToolsCancelled,
    } = useAiChatMessaging({
        agentEnabled,
        projectId,
        contextString,
        toolData: projectContext.toolData,
        locale,
    })

    // Scroll to bottom when new messages arrive
    useEffect(() => {
        scrollRef.current?.scrollIntoView({ behavior: "smooth" })
    }, [messages])

    const handleConfirmTools = useCallback(async () => {
        if (!pendingToolCalls) return

        try {
            const results = await toolExecutor.executeToolCalls(pendingToolCalls)
            handleToolsConfirmed(results)
        } catch (err) {
            console.error("Tool execution error:", err)
        }
    }, [pendingToolCalls, toolExecutor, handleToolsConfirmed])

    const handleSubmit = useCallback((e: React.FormEvent) => {
        e.preventDefault()
        sendMessage(input)
        setInput("")
    }, [sendMessage, input])

    const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setInput(e.target.value)
    }, [])

    const isEmpty = messages.length === 0
    const hasProjectContext = Boolean(projectContext.project)

    // Dynamic suggestions based on project context and agent mode
    const suggestions = useAiChatSuggestions({
        hasProjectContext,
        projectContext,
        agentEnabled,
        locale,
    })

    if (!aiEnabled) {
        return (
            <div className={cn("flex h-full flex-col items-center justify-center gap-3 bg-background text-muted-foreground", className)}>
                <Bot className="h-10 w-10 opacity-40" />
                <p className="text-sm font-medium">{t("ai.featureDisabled")}</p>
            </div>
        )
    }

    return (
        <div className={cn("flex h-full flex-col bg-background", className)}>
            {/* Status Banners */}
            <AiDisclaimerBanner t={t} />
            {hasProjectContext && (
                <>
                    <ProjectContextBanner t={t} projectContext={projectContext} />
                    <AgentModeBanner t={t} agentEnabled={agentEnabled} />
                </>
            )}

            {/* Messages Area */}
            <ScrollArea className="flex-1 px-4 py-4">
                {isEmpty ? (
                    <div className="flex h-full flex-col items-center justify-center text-muted-foreground gap-3 py-8">
                        <div className="flex h-16 w-16 items-center justify-center rounded-[14px] border border-primary/20 bg-primary/10">
                            <Sparkles className="h-8 w-8 text-primary" />
                        </div>
                        <div className="text-center space-y-1">
                            <h3 className="font-semibold text-foreground">
                                {t("ai.chatTitle") || "AI Architect Assistant"}
                            </h3>
                            <p className="text-sm max-w-[250px]">
                                {(() => {
                                    if (hasProjectContext) {
                                        return t("ai.contextLoadedAsk", { project: projectContext.project?.title || "" })
                                    }
                                    return t("ai.askAboutArchitecture")
                                })()}
                            </p>
                        </div>
                        <div className="flex flex-wrap gap-2 justify-center mt-2">
                            {suggestions.map((suggestion) => (
                                <SuggestionButton key={suggestion} suggestion={suggestion} onSelect={setInput} />
                            ))}
                        </div>
                    </div>
                ) : (
                    <div className="space-y-4">
                        {messages.map((msg) => (
                            <AiChatMessage key={msg.id} message={msg} />
                        ))}
                        {isLoading && (
                            <div className="flex gap-3">
                                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[10px] border border-primary/20 bg-primary/10">
                                    <Bot className="h-4 w-4 text-primary" />
                                </div>
                                <div className="flex items-center gap-2 px-4 py-3 bg-muted rounded-2xl rounded-bl-sm">
                                    <Loader2 className="h-4 w-4 animate-spin" />
                                    <span className="text-sm text-muted-foreground">{t("ai.thinking")}</span>
                                </div>
                            </div>
                        )}
                        <div ref={scrollRef} />
                    </div>
                )}
            </ScrollArea>

            {/* Error Display */}
            {error && (
                <div className="px-4 py-2 bg-destructive/10 border-t border-destructive/20">
                    <p className="text-xs text-destructive">{error}</p>
                </div>
            )}

            {/* Input Area */}
            <div className="p-4 bg-background border-t">
                <form onSubmit={handleSubmit} className="flex items-center gap-2">
                    <div className="flex-1 flex items-center bg-muted/50 rounded-[20px] px-4 py-1 focus-within:bg-muted/70 transition-colors">
                        <Input
                            value={input}
                            onChange={handleInputChange}
                            placeholder={t("ai.chatPlaceholder")}
                            disabled={isLoading}
                            className="flex-1 border-0 bg-transparent shadow-none focus-visible:ring-0 px-0 py-2.5 h-auto text-[15px] placeholder:text-muted-foreground"
                        />
                    </div>
                    <Button
                        type="submit"
                        size="icon"
                        disabled={isLoading || !input.trim()}
                        className="h-9 w-9 shrink-0 rounded-full bg-primary text-primary-foreground shadow-sm hover:bg-primary/90"
                    >
                        {isLoading ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                            <Send className="h-4 w-4 ml-0.5" />
                        )}
                    </Button>
                </form>
            </div>

            {/* Tool Confirmation Dialog */}
            <ToolConfirmDialog
                open={showToolConfirm}
                toolCalls={pendingToolCalls || []}
                isExecuting={toolExecutor.isExecuting}
                onConfirm={handleConfirmTools}
                onCancel={handleToolsCancelled}
            />
        </div>
    )
})

interface AiChatMessageProps {
    message: ChatMessage
}

const AiChatMessage = memo(function AiChatMessage({ message }: AiChatMessageProps) {
    const isUser = message.role === "user"

    return (
        <div className={cn("flex gap-3", isUser ? "flex-row-reverse" : "flex-row")}>
            <div className={cn(
                "flex h-8 w-8 shrink-0 items-center justify-center rounded-[10px]",
                isUser
                    ? "bg-bg-subtle border border-border"
                    : "bg-primary/10 border border-primary/20"
            )}>
                {isUser ? (
                    <span className="font-mono text-sm font-semibold text-foreground">U</span>
                ) : (
                    <Bot className="h-4 w-4 text-primary" />
                )}
            </div>
            <div className={cn(
                "max-w-[80%] px-4 py-2.5 shadow-sm",
                isUser
                    ? "bg-primary text-primary-foreground rounded-[18px] rounded-br-sm"
                    : "bg-muted text-foreground rounded-[18px] rounded-bl-sm"
            )}>
                {isUser ? (
                    <div className="text-[15px] leading-relaxed whitespace-pre-wrap">
                        {message.content}
                    </div>
                ) : (
                    <div className="text-[15px] leading-relaxed prose prose-sm dark:prose-invert max-w-none prose-p:my-1 prose-ul:my-1 prose-ol:my-1 prose-li:my-0.5 prose-headings:my-2 prose-headings:text-base prose-strong:text-inherit">
                        <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={aiChatRehypePlugins} components={markdownComponents}>
                            {message.content}
                        </ReactMarkdown>
                    </div>
                )}
            </div>
        </div>
    )
})
