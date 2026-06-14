'use client'

import { useEffect, useRef } from 'react'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/utils'
import type { AuditLogDto, PagedResult } from '@/types/api'
import type { InfiniteData } from '@tanstack/react-query'

interface AuditLogPage {
  logs: PagedResult<AuditLogDto>
  actionTypes: string[]
}

interface AuditLogTableProps {
  data: InfiniteData<AuditLogPage> | undefined
  isLoading: boolean
  isFetchingNextPage: boolean
  hasNextPage: boolean
  fetchNextPage: () => void
}

function actionBadgeVariant(action: string) {
  switch (action) {
    case 'Delete':
    case 'UserDisable':
      return 'destructive' as const
    case 'Approve':
    case 'UserCreate':
      return 'default' as const
    case 'Upload':
    case 'Download':
      return 'secondary' as const
    default:
      return 'outline' as const
  }
}

export function AuditLogTable({ data, isLoading, isFetchingNextPage, hasNextPage, fetchNextPage }: AuditLogTableProps) {
  const sentinelRef = useRef<HTMLDivElement>(null)

  // Infinite scroll observer
  useEffect(() => {
    if (!sentinelRef.current || !hasNextPage) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage()
        }
      },
      { threshold: 0.1 }
    )

    observer.observe(sentinelRef.current)
    return () => observer.disconnect()
  }, [hasNextPage, isFetchingNextPage, fetchNextPage])

  const allLogs = data?.pages?.flatMap((page) => page?.logs?.items ?? []).filter(Boolean) ?? []

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 10 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    )
  }

  if (allLogs.length === 0) {
    return (
      <div className="py-12 text-center">
        <p className="text-sm text-muted-foreground">No audit logs found matching your filters.</p>
      </div>
    )
  }

  return (
    <div>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-[180px]">Time</TableHead>
              <TableHead>User</TableHead>
              <TableHead className="w-[140px]">Action</TableHead>
              <TableHead>Details</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {allLogs.map((log) => (
              <TableRow key={log.id}>
                <TableCell className="font-mono text-xs text-muted-foreground">
                  {formatDate(log.timestamp, 'long')}
                </TableCell>
                <TableCell className="text-sm">{log.userEmail ?? log.userId ?? 'System'}</TableCell>
                <TableCell>
                  <Badge variant={actionBadgeVariant(log.action)}>{log.action}</Badge>
                </TableCell>
                <TableCell className="max-w-[400px] truncate text-sm text-muted-foreground">{log.details}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Infinite scroll sentinel */}
      <div ref={sentinelRef} className="h-px" />

      {isFetchingNextPage && (
        <div className="mt-4 space-y-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      )}
    </div>
  )
}
