import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

export interface UserApiKeyView {
  readonly id: string
  readonly provider: string
  readonly keyHint: string
  readonly label?: string | null
  readonly isActive: boolean
  readonly lastValidatedAt?: string | null
  readonly lastUsedAt?: string | null
  readonly createdAt: string
}

export interface LlmProviderView {
  readonly providerId: string
  readonly displayName: string
  readonly supportsKeyValidation: boolean
  readonly supportsStreaming: boolean
}

export interface LlmModelView {
  readonly id: string
  readonly provider: string
  readonly modelId: string
  readonly displayName: string
  readonly description?: string | null
  readonly tier: "cheap" | "balanced" | "smart" | string
  readonly contextWindow: number
  readonly maxOutputTokens?: number | null
  readonly supportsTools: boolean
  readonly supportsStreaming: boolean
  readonly supportsVision: boolean
  readonly inputPricePer1M?: number | null
  readonly outputPricePer1M?: number | null
  readonly sortOrder: number
}

export interface LlmModelQuery {
  readonly provider?: string | null
  readonly requiresTools?: boolean
  readonly requiresVision?: boolean
  readonly requiresStreaming?: boolean
}

export interface LlmModelSyncResult {
  readonly provider: string
  readonly providerModelCount: number
  readonly created: number
  readonly updated: number
  readonly missingPricing: number
}

export interface LlmChatEstimateRequest {
  readonly conversationId: string
  readonly message: string
  readonly provider?: string | null
  readonly modelId?: string | null
  readonly enableTools?: boolean
  readonly useHistory?: boolean | null
  readonly maxOutputTokens?: number | null
}

export interface LlmChatEstimateResponse {
  readonly provider: string
  readonly modelId: string
  readonly promptTokens: number
  readonly maxOutputTokens: number
  readonly minCostUsd?: number | null
  readonly maxCostUsd?: number | null
  readonly isApproximate: boolean
}

export const llmQueryKeys = {
  providers: ["llm", "providers"] as const,
  keys: ["llm", "api-keys"] as const,
  models: (query: LlmModelQuery = {}) =>
    ["llm", "models", query.provider ?? null, query.requiresTools ?? null, query.requiresVision ?? null, query.requiresStreaming ?? null] as const,
  chatEstimate: (request: LlmChatEstimateRequest) =>
    [
      "llm",
      "chat-estimate",
      request.conversationId,
      request.message,
      request.provider ?? null,
      request.modelId ?? null,
      request.enableTools ?? false,
      request.useHistory ?? null,
      request.maxOutputTokens ?? null,
    ] as const,
}

export function useLlmProviders() {
  return useQuery({
    queryKey: llmQueryKeys.providers,
    queryFn: async (): Promise<LlmProviderView[]> => {
      const response = await apiClient.get<LlmProviderView[]>("/llm/providers")
      return response.data
    },
  })
}

export function useUserApiKeys() {
  return useQuery({
    queryKey: llmQueryKeys.keys,
    queryFn: async (): Promise<UserApiKeyView[]> => {
      const response = await apiClient.get<UserApiKeyView[]>("/me/api-keys")
      return response.data
    },
  })
}

export function useLlmModels(query: LlmModelQuery = {}) {
  return useQuery({
    queryKey: llmQueryKeys.models(query),
    queryFn: async (): Promise<LlmModelView[]> => {
      const response = await apiClient.get<LlmModelView[]>("/llm/models", {
        params: {
          provider: query.provider || undefined,
          requiresTools: query.requiresTools,
          requiresVision: query.requiresVision,
          requiresStreaming: query.requiresStreaming,
        },
      })
      return response.data
    },
  })
}

export function useLlmChatEstimate(
  request: LlmChatEstimateRequest | null,
  enabled: boolean
) {
  return useQuery({
    queryKey: request ? llmQueryKeys.chatEstimate(request) : ["llm", "chat-estimate", null],
    queryFn: async (): Promise<LlmChatEstimateResponse> => {
      if (!request) throw new Error("Estimate request is missing")
      const response = await apiClient.post<LlmChatEstimateResponse>(
        `/ai/chat/conversations/${request.conversationId}/estimate`,
        {
          message: request.message,
          provider: request.provider ?? null,
          modelId: request.modelId ?? null,
          enableTools: request.enableTools ?? false,
          useHistory: request.useHistory ?? null,
          maxOutputTokens: request.maxOutputTokens ?? null,
        }
      )
      return response.data
    },
    enabled: Boolean(request) && enabled,
    staleTime: 30_000,
    retry: false,
  })
}

export function useSaveUserApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: {
      provider: string
      apiKey: string
      label?: string | null
    }): Promise<UserApiKeyView> => {
      const response = await apiClient.post<UserApiKeyView>("/me/api-keys", data)
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: llmQueryKeys.keys })
      queryClient.invalidateQueries({ queryKey: llmQueryKeys.providers })
    },
  })
}

export function useDeleteUserApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (keyId: string): Promise<void> => {
      await apiClient.delete(`/me/api-keys/${keyId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: llmQueryKeys.keys })
    },
  })
}

/**
 * Cancels an in-flight LLM stream by request id. The backend only honors the
 * call when the requesting user owns the request — other users get a no-op
 * 200, so we don't expose request-id existence by error code.
 */
export function useCancelAiStream() {
  return useMutation({
    mutationFn: async ({
      conversationId,
      requestId,
    }: {
      conversationId: string
      requestId: string
    }): Promise<void> => {
      await apiClient.post(`/ai/chat/conversations/${conversationId}/cancel/${requestId}`)
    },
  })
}

/**
 * Re-runs the user's most recent /ai command in the conversation against the
 * current (or overridden) model selection. Backend pulls the prompt from the
 * persisted user message, so we don't have to round-trip through the input
 * box.
 */
export function useRegenerateAiMessage() {
  return useMutation({
    mutationFn: async ({
      conversationId,
      provider,
      modelId,
      enableTools,
      useHistory,
    }: {
      conversationId: string
      provider?: string | null
      modelId?: string | null
      enableTools?: boolean
      useHistory?: boolean | null
    }): Promise<unknown> => {
      const response = await apiClient.post(`/ai/chat/conversations/${conversationId}/regenerate`, {
        provider: provider ?? null,
        modelId: modelId ?? null,
        enableTools: enableTools ?? false,
        useHistory: useHistory ?? null,
      })
      return response.data
    },
  })
}

export function useSyncLlmModels() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (provider: string): Promise<LlmModelSyncResult> => {
      const response = await apiClient.post<LlmModelSyncResult>(`/llm/providers/${provider}/models/sync`)
      return response.data
    },
    onSuccess: (_result, provider) => {
      queryClient.invalidateQueries({ queryKey: ["llm", "models"] })
      queryClient.invalidateQueries({ queryKey: llmQueryKeys.models({ provider }) })
    },
  })
}
