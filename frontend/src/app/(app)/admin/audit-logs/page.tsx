'use client'

import { useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { AuditLogFilters } from '@/components/admin/audit-log-filters'
import { AuditLogTable } from '@/components/admin/audit-log-table'
import { useAuditLogs } from '@/hooks/use-audit-logs'

interface Filters {
  query?: string
  actionType?: string
  from?: string
  to?: string
}

export default function AuditLogsPage() {
  const searchParams = useSearchParams()

  const [filters, setFilters] = useState<Filters>({
    query: searchParams.get('query') ?? undefined,
    actionType: searchParams.get('actionType') ?? undefined,
    from: searchParams.get('from') ?? undefined,
    to: searchParams.get('to') ?? undefined
  })

  const { data, isLoading, isFetchingNextPage, hasNextPage, fetchNextPage, error, refetch } = useAuditLogs(filters)

  // keepPreviousData in the query hook ensures data (and actionTypes) never
  // goes undefined when filters change, so no memoization is needed.
  const actionTypes = data?.pages?.[0]?.actionTypes ?? []

  const handleFilterChange = (newFilters: Filters) => {
    setFilters(newFilters)
    // Sync URL for bookmarkability without triggering Next.js navigation/transitions
    const params = new URLSearchParams()
    if (newFilters.query) params.set('query', newFilters.query)
    if (newFilters.actionType) params.set('actionType', newFilters.actionType)
    if (newFilters.from) params.set('from', newFilters.from)
    if (newFilters.to) params.set('to', newFilters.to)
    const qs = params.toString()
    window.history.replaceState(null, '', `/admin/audit-logs${qs ? `?${qs}` : ''}`)
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Audit Logs</h1>
        <p className="text-sm text-muted-foreground">Search and filter platform activity logs</p>
      </div>

      <AuditLogFilters filters={filters} onFilterChange={handleFilterChange} actionTypes={actionTypes} />

      {error ? (
        <div className="flex flex-col items-center py-12 text-center">
          <p className="text-sm text-muted-foreground">Failed to load audit logs.</p>
          <button onClick={() => refetch()} className="mt-3 rounded-md border px-3 py-1.5 text-sm hover:bg-muted">
            Retry
          </button>
        </div>
      ) : (
        <AuditLogTable
          data={data}
          isLoading={isLoading}
          isFetchingNextPage={isFetchingNextPage}
          hasNextPage={hasNextPage ?? false}
          fetchNextPage={fetchNextPage}
        />
      )}
    </div>
  )
}
