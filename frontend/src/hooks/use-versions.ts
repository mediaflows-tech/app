'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { assetKeys } from '@/hooks/use-assets'

export interface AssetVersionDto {
  id: number
  versionNumber: number
  s3Key: string
  fileSize: number
  contentType: string
  notes: string | null
  createdAt: string
  createdByName: string
  isCurrent: boolean
  previewUrl?: string
}

export interface VersionCompareDto {
  versionA: AssetVersionDto & { mediaUrl: string }
  versionB: AssetVersionDto & { mediaUrl: string }
}

/** Shape the API actually returns (wrapper with versions array, changeNotes instead of notes) */
interface VersionsApiResponse {
  assetId: number
  currentVersionId: number
  versions: Array<{
    id: number
    versionNumber: number
    s3Key?: string
    fileSize: number
    contentType: string
    changeNotes?: string | null
    notes?: string | null
    createdAt: string
    createdByName?: string
    isCurrent?: boolean
    previewUrl?: string
  }>
}

export const versionKeys = {
  all: (assetId: number) => ['assets', assetId, 'versions'] as const,
  list: (assetId: number) => [...versionKeys.all(assetId), 'list'] as const,
  compare: (assetId: number, a: number, b: number) => [...versionKeys.all(assetId), 'compare', a, b] as const
}

export function useVersions(assetId: number) {
  return useQuery({
    queryKey: versionKeys.list(assetId),
    queryFn: async (): Promise<AssetVersionDto[]> => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const raw: any = await api.get(`/assets/${assetId}/versions`)
      // API returns a wrapper object { assetId, currentVersionId, versions: [...] }
      const items = Array.isArray(raw) ? raw : (raw?.versions ?? [])
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return items.map((v: any) => ({
        id: v.id,
        versionNumber: v.versionNumber,
        s3Key: v.s3Key ?? '',
        fileSize: v.fileSize,
        contentType: v.contentType,
        notes: v.changeNotes ?? v.notes ?? null,
        createdAt: v.createdAt,
        createdByName: v.createdByName ?? '',
        isCurrent: v.isCurrent ?? false,
        previewUrl: v.previewUrl
      }))
    },
    enabled: assetId > 0
  })
}

export function useCompare(assetId: number, versionA: number, versionB: number) {
  return useQuery({
    queryKey: versionKeys.compare(assetId, versionA, versionB),
    queryFn: () => api.get<VersionCompareDto>(`/assets/${assetId}/versions/compare?a=${versionA}&b=${versionB}`),
    enabled: assetId > 0 && versionA > 0 && versionB > 0,
    retry: false
  })
}

export function useUploadVersion() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      assetId,
      s3Key,
      contentType,
      fileSize,
      notes
    }: {
      assetId: number
      s3Key: string
      contentType: string
      fileSize: number
      notes?: string
    }) =>
      api.post<AssetVersionDto>(`/assets/${assetId}/versions`, {
        s3Key,
        contentType,
        fileSize,
        changeNotes: notes
      }),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: versionKeys.all(variables.assetId)
      })
      queryClient.invalidateQueries({
        queryKey: assetKeys.detail(variables.assetId)
      })
    }
  })
}

export function useRevert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ assetId, versionId }: { assetId: number; versionId: number }) =>
      api.post<{ status: string }>(`/assets/${assetId}/versions/${versionId}/revert`),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: versionKeys.all(variables.assetId)
      })
      queryClient.invalidateQueries({
        queryKey: assetKeys.detail(variables.assetId)
      })
    }
  })
}
