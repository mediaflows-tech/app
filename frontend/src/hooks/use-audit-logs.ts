'use client'

import { useInfiniteQuery, keepPreviousData } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { AuditLogsResponseDto, AuditLogDto, PagedResult } from '@/types/api'

interface AuditLogFilters {
  query?: string
  actionType?: string
  from?: string
  to?: string
}

// Response shape: { logs: PagedResult<AuditLogDto>, actionTypes: string[] }
// We normalize so each page exposes logs (the paged result) and actionTypes
interface AuditLogPage {
  logs: PagedResult<AuditLogDto>
  actionTypes: string[]
}

export function useAuditLogs(filters: AuditLogFilters) {
  return useInfiniteQuery({
    queryKey: ['audit-logs', filters],
    queryFn: async ({ pageParam = 1 }): Promise<AuditLogPage> => {
      const params = new URLSearchParams()
      params.set('page', String(pageParam))
      params.set('pageSize', '50')
      if (filters.query) params.set('query', filters.query)
      if (filters.actionType) params.set('actionType', filters.actionType)
      if (filters.from) params.set('from', filters.from)
      if (filters.to) params.set('to', filters.to)

      const raw = await api.get<AuditLogsResponseDto>(`/admin/audit-logs?${params.toString()}`)
      return {
        logs: raw.logs,
        actionTypes: raw.actionTypes ?? []
      }
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      if (!lastPage?.logs?.hasMore) return undefined
      return (lastPage.logs.page ?? 0) + 1
    },
    placeholderData: keepPreviousData,
    retry: 1
  })
}
