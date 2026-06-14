'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { assetKeys } from '@/hooks/use-assets'
import { toast } from '@/lib/toast'

export interface AssetTagsDto {
  assetId: number
  tags: string[]
  autoTags: Array<{ name: string; confidence: number }>
}

/** Shape the API actually returns (manualTags instead of tags) */
interface AssetTagsApiResponse {
  assetId: number
  manualTags: string[]
  autoTags: Array<{ name: string; confidence: number }>
}

export const tagKeys = {
  all: (assetId: number) => ['assets', assetId, 'tags'] as const
}

export function useTags(assetId: number) {
  return useQuery({
    queryKey: tagKeys.all(assetId),
    queryFn: async (): Promise<AssetTagsDto> => {
      const raw = await api.get<AssetTagsApiResponse>(`/assets/${assetId}/tags`)
      return {
        assetId: raw.assetId,
        tags: raw.manualTags ?? [],
        autoTags: raw.autoTags ?? []
      }
    },
    enabled: assetId > 0
  })
}

export function useAddTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ assetId, tag }: { assetId: number; tag: string }) =>
      api.post<AssetTagsDto>(`/assets/${assetId}/tags`, { tagName: tag }),
    onMutate: async ({ assetId, tag }) => {
      await queryClient.cancelQueries({ queryKey: tagKeys.all(assetId) })
      const previous = queryClient.getQueryData<AssetTagsDto>(tagKeys.all(assetId))
      if (previous) {
        queryClient.setQueryData<AssetTagsDto>(tagKeys.all(assetId), {
          ...previous,
          tags: [...previous.tags, tag]
        })
      }
      return { previous }
    },
    onError: (_err, { assetId }, context) => {
      if (context?.previous) {
        queryClient.setQueryData(tagKeys.all(assetId), context.previous)
      }
      toast.error('Failed to add tag')
    },
    onSuccess: () => {
      toast.success('Tag added')
    },
    onSettled: (_data, _err, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: tagKeys.all(assetId) })
    }
  })
}

export function useRemoveTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ assetId, tag }: { assetId: number; tag: string }) =>
      api.delete<AssetTagsDto>(`/assets/${assetId}/tags/${encodeURIComponent(tag)}`),
    onMutate: async ({ assetId, tag }) => {
      await queryClient.cancelQueries({ queryKey: tagKeys.all(assetId) })
      const previous = queryClient.getQueryData<AssetTagsDto>(tagKeys.all(assetId))
      if (previous) {
        queryClient.setQueryData<AssetTagsDto>(tagKeys.all(assetId), {
          ...previous,
          tags: previous.tags.filter((t) => t !== tag)
        })
      }
      return { previous }
    },
    onError: (_err, { assetId }, context) => {
      if (context?.previous) {
        queryClient.setQueryData(tagKeys.all(assetId), context.previous)
      }
      toast.error('Failed to remove tag')
    },
    onSuccess: () => {
      toast.success('Tag removed')
    },
    onSettled: (_data, _err, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: tagKeys.all(assetId) })
    }
  })
}

export function useUpdateAsset() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ assetId, title, description }: { assetId: number; title?: string; description?: string }) =>
      api.patch<{ title: string; description: string }>(`/assets/${assetId}`, {
        title,
        description
      }),
    onSuccess: (_data, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: assetKeys.detail(assetId) })
      queryClient.invalidateQueries({ queryKey: assetKeys.all })
    }
  })
}
