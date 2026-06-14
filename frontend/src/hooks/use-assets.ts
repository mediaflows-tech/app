'use client'

import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type {
  PagedResult,
  MediaAssetSummaryDto,
  AssetStatus,
  AssetDetailApiResponse,
  AssetDetailView
} from '@/types/api'

export const assetKeys = {
  all: ['assets'] as const,
  lists: () => [...assetKeys.all, 'list'] as const,
  list: (filters: AssetFilters) => [...assetKeys.lists(), filters] as const,
  details: () => [...assetKeys.all, 'detail'] as const,
  detail: (id: number) => [...assetKeys.details(), id] as const
}

export interface AssetFilters {
  status?: AssetStatus | 'All'
  fileType?: string
  sort?: 'newest' | 'oldest' | 'title' | 'size'
}

export function useAssets(filters: AssetFilters = {}) {
  return useInfiniteQuery({
    queryKey: assetKeys.list(filters),
    queryFn: async ({ pageParam = 1 }) => {
      const params = new URLSearchParams({ page: String(pageParam) })
      if (filters.status && filters.status !== 'All') {
        params.set('status', filters.status)
      }
      if (filters.fileType) params.set('fileType', filters.fileType)
      if (filters.sort) params.set('sort', filters.sort)
      return api.get<PagedResult<MediaAssetSummaryDto>>(`/assets?${params.toString()}`)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.hasMore ? lastPage.page + 1 : undefined),
    staleTime: 30_000
  })
}

export function useAsset(id: number) {
  return useQuery({
    queryKey: assetKeys.detail(id),
    queryFn: async (): Promise<AssetDetailView> => {
      const raw = await api.get<AssetDetailApiResponse>(`/assets/${id}`)
      return {
        ...raw.asset,
        mediaUrl: raw.mediaUrl,
        viewCount: raw.viewCount ?? raw.asset.viewCount ?? 0,
        commentCount: raw.commentCount ?? 0
      }
    },
    staleTime: 30_000,
    enabled: id > 0
  })
}

export function useSubmitAsset() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (assetId: number) => api.post<{ status: string }>(`/assets/${assetId}/submit`),
    onSuccess: (_data, assetId) => {
      queryClient.invalidateQueries({ queryKey: assetKeys.all })
      queryClient.invalidateQueries({ queryKey: assetKeys.detail(assetId) })
    }
  })
}

export function useDeleteAsset() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (assetId: number) => api.delete(`/assets/${assetId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: assetKeys.all })
    }
  })
}

export function useBulkSubmitAssets() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (assetIds: number[]) =>
      api.post<{ submitted: number; skipped: number }>('/assets/bulk/submit', { assetIds }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assetKeys.all })
  })
}

export function useBulkDeleteAssets() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (assetIds: number[]) =>
      api.delete<{ deleted: number; skipped: number }>('/assets/bulk', {
        body: JSON.stringify({ assetIds })
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assetKeys.all })
  })
}
