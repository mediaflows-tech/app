import { useInfiniteQuery, useMutation, useQueryClient, type InfiniteData } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult, MediaAssetSummaryDto, AssetDetailDto } from '@/types/api'
import { catalogKeys } from './use-catalog'
import { toast } from '@/lib/toast'

export const bookmarkKeys = {
  all: ['bookmarks'] as const,
  list: () => [...bookmarkKeys.all, 'list'] as const
}

export function useBookmarks() {
  return useInfiniteQuery<
    PagedResult<MediaAssetSummaryDto>,
    Error,
    InfiniteData<PagedResult<MediaAssetSummaryDto>>,
    ReturnType<typeof bookmarkKeys.list>,
    number
  >({
    queryKey: bookmarkKeys.list(),
    queryFn: async ({ pageParam }) => {
      return api.get<PagedResult<MediaAssetSummaryDto>>(`/bookmarks?page=${pageParam}`)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.hasMore ? lastPage.page + 1 : undefined),
    staleTime: 30 * 1000
  })
}

interface ToggleBookmarkResponse {
  isBookmarked: boolean
}

export function useToggleBookmark() {
  const queryClient = useQueryClient()

  return useMutation<ToggleBookmarkResponse, Error, number, { previousDetail: AssetDetailDto | undefined }>({
    mutationFn: (assetId: number) => api.post<ToggleBookmarkResponse>(`/bookmarks/${assetId}/toggle`),

    onMutate: async (assetId) => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey: catalogKeys.details() })
      await queryClient.cancelQueries({ queryKey: bookmarkKeys.all })

      // Snapshot the previous detail value for rollback
      const previousDetail = queryClient.getQueryData<AssetDetailDto>(catalogKeys.detail(assetId))

      // Optimistically update the asset detail
      if (previousDetail) {
        queryClient.setQueryData<AssetDetailDto>(catalogKeys.detail(assetId), {
          ...previousDetail,
          isBookmarked: !previousDetail.isBookmarked
        })
      }

      return { previousDetail }
    },

    onError: (_err, assetId, context) => {
      // Rollback on error
      if (context?.previousDetail) {
        queryClient.setQueryData(catalogKeys.detail(assetId), context.previousDetail)
      }
      toast.error('Failed to update bookmark')
    },

    onSettled: () => {
      // Refetch bookmarks list and catalog details
      queryClient.invalidateQueries({ queryKey: bookmarkKeys.all })
      queryClient.invalidateQueries({ queryKey: catalogKeys.details() })
    },

    onSuccess: (data) => {
      toast.success(data.isBookmarked ? 'Bookmarked' : 'Bookmark removed')
    }
  })
}
