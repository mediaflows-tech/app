import { useInfiniteQuery, useQuery, type InfiniteData } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult, SearchResultDto } from '@/types/api'

export const searchKeys = {
  all: ['search'] as const,
  results: (params: SearchParams) => [...searchKeys.all, params] as const,
  autocomplete: (prefix: string) => [...searchKeys.all, 'autocomplete', prefix] as const
}

export interface SearchParams {
  q: string
  category?: string
  fileType?: string
}

export function useSearch(params: SearchParams) {
  return useInfiniteQuery<
    PagedResult<SearchResultDto>,
    Error,
    InfiniteData<PagedResult<SearchResultDto>>,
    ReturnType<typeof searchKeys.results>,
    number
  >({
    queryKey: searchKeys.results(params),
    queryFn: async ({ pageParam }) => {
      const searchParams = new URLSearchParams()
      searchParams.set('q', params.q)
      searchParams.set('page', String(pageParam))
      if (params.category) searchParams.set('category', params.category)
      if (params.fileType) searchParams.set('fileType', params.fileType)
      return api.get<PagedResult<SearchResultDto>>(`/search?${searchParams.toString()}`)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage?.hasMore ? (lastPage.page ?? 0) + 1 : undefined),
    enabled: params.q.length >= 2,
    staleTime: 30 * 1000
  })
}

export function useAutocomplete(prefix: string) {
  return useQuery<string[]>({
    queryKey: searchKeys.autocomplete(prefix),
    queryFn: () => api.get<string[]>(`/search/autocomplete?prefix=${encodeURIComponent(prefix)}`),
    enabled: prefix.length >= 2,
    staleTime: 60 * 1000
  })
}
