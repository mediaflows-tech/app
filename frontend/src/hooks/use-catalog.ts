import { useInfiniteQuery, useQuery, type InfiniteData } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult, MediaAssetSummaryDto, AssetDetailDto } from '@/types/api'

export const catalogKeys = {
  all: ['catalog'] as const,
  lists: () => [...catalogKeys.all, 'list'] as const,
  list: (type?: string, sort?: string) => [...catalogKeys.lists(), { type, sort }] as const,
  details: () => [...catalogKeys.all, 'detail'] as const,
  detail: (id: number) => [...catalogKeys.details(), id] as const
}

interface UseCatalogOptions {
  type?: string
  sort?: string
}

export function useCatalog(options: UseCatalogOptions = {}) {
  const { type, sort } = options
  const isTrending = sort === 'trending'

  return useInfiniteQuery<
    PagedResult<MediaAssetSummaryDto>,
    Error,
    InfiniteData<PagedResult<MediaAssetSummaryDto>>,
    ReturnType<typeof catalogKeys.list>,
    number
  >({
    queryKey: catalogKeys.list(type, sort),
    queryFn: async ({ pageParam }) => {
      const params = new URLSearchParams()
      params.set('page', String(pageParam))
      if (type) params.set('type', type)
      if (sort) params.set('sort', sort)
      return api.get<PagedResult<MediaAssetSummaryDto>>(`/catalog?${params.toString()}`)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      if (isTrending) return undefined
      return lastPage.hasMore ? lastPage.page + 1 : undefined
    },
    staleTime: 60 * 1000
  })
}

export function useCatalogDetail(id: number) {
  return useQuery<AssetDetailDto>({
    queryKey: catalogKeys.detail(id),
    queryFn: async () => {
      const raw = await api.get<{
        asset: AssetDetailDto
        mediaUrl: string
        viewCount: number
        isBookmarked: boolean
        comments: unknown[]
        commentCount: number
        relatedAssets: unknown[]
      }>(`/catalog/${id}`)
      // Unwrap the nested response into a flat AssetDetailDto
      return {
        ...raw.asset,
        mediaUrl: raw.mediaUrl,
        viewCount: raw.viewCount ?? 0,
        isBookmarked: raw.isBookmarked ?? false,
        commentCount: raw.commentCount ?? 0
      } as AssetDetailDto & { mediaUrl: string }
    },
    staleTime: 30 * 1000
  })
}
