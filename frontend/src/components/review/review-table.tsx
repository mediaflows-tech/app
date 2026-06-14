'use client'

import { Fragment, useMemo } from 'react'
import type { ReviewListItemDto } from '@/types/api'
import { REVIEW_ACTIONABLE_STATUSES } from '@/lib/review-ui'
import { ReviewRow } from './review-row'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Checkbox } from '@/components/ui/checkbox'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/shared/empty-state'
import { Skeleton } from '@/components/ui/skeleton'
import { Inbox } from 'lucide-react'

interface ReviewTableProps {
  items: ReviewListItemDto[]
  isLoading: boolean
  selectedIds: Set<number>
  onSelectAll: (checked: boolean) => void
  onToggleSelect: (id: number, checked: boolean) => void
  viewMode: 'list' | 'grouped'
}

function getDateGroup(dateStr: string): string {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  const weekAgo = new Date(today)
  weekAgo.setDate(weekAgo.getDate() - 7)
  const d = new Date(dateStr)
  d.setHours(0, 0, 0, 0)
  if (d >= today) return 'Today'
  if (d >= yesterday) return 'Yesterday'
  if (d >= weekAgo) return 'This Week'
  return 'Earlier'
}

function LoadingRows() {
  return (
    <>
      {Array.from({ length: 6 }).map((_, i) => (
        <TableRow key={i}>
          <td className="p-2">
            <Skeleton className="h-4 w-4" />
          </td>
          <td className="p-2">
            <div className="flex items-center gap-3">
              <Skeleton className="h-12 w-12 rounded-md" />
              <div className="space-y-1.5">
                <Skeleton className="h-4 w-32" />
                <Skeleton className="h-3 w-16" />
              </div>
            </div>
          </td>
          <td className="p-2">
            <Skeleton className="h-4 w-20" />
          </td>
          <td className="p-2">
            <Skeleton className="h-5 w-16 rounded-full" />
          </td>
          <td className="p-2">
            <Skeleton className="h-5 w-20 rounded-full" />
          </td>
          <td className="p-2">
            <Skeleton className="h-4 w-14" />
          </td>
          <td className="p-2">
            <Skeleton className="h-8 w-8" />
          </td>
        </TableRow>
      ))}
    </>
  )
}

export function ReviewTable({
  items,
  isLoading,
  selectedIds,
  onSelectAll,
  onToggleSelect,
  viewMode
}: ReviewTableProps) {
  const groupedItems = useMemo(() => {
    if (viewMode !== 'grouped') return null
    const groups: { label: string; items: ReviewListItemDto[] }[] = []
    let lastGroup = ''
    for (const item of items) {
      const group = getDateGroup(item.createdAt)
      if (group !== lastGroup) {
        groups.push({ label: group, items: [] })
        lastGroup = group
      }
      groups[groups.length - 1].items.push(item)
    }
    return groups
  }, [items, viewMode])

  if (!isLoading && items.length === 0) {
    return (
      <EmptyState
        icon={Inbox}
        title="No reviews found"
        description="There are no reviews matching your current filters."
      />
    )
  }

  const allSelectableChecked =
    items
      .filter((i) => REVIEW_ACTIONABLE_STATUSES.includes(i.status))
      .every((i) => selectedIds.has(i.assetId)) && selectedIds.size > 0

  return (
    <Card className="p-0">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-10">
              <Checkbox checked={allSelectableChecked} onCheckedChange={onSelectAll} />
            </TableHead>
            <TableHead>Asset</TableHead>
            <TableHead>Creator</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Submitted</TableHead>
            <TableHead className="w-[100px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading ? (
            <LoadingRows />
          ) : viewMode === 'grouped' && groupedItems ? (
            groupedItems.map((group) => (
              <Fragment key={`group-${group.label}`}>
                <TableRow>
                  <td colSpan={7} className="py-2 px-3 bg-muted/50">
                    <span className="text-sm font-semibold text-muted-foreground">{group.label}</span>
                  </td>
                </TableRow>
                {group.items.map((item) => (
                  <ReviewRow
                    key={item.assetId}
                    item={item}
                    isSelected={selectedIds.has(item.assetId)}
                    onToggleSelect={onToggleSelect}
                  />
                ))}
              </Fragment>
            ))
          ) : (
            items.map((item) => (
              <ReviewRow
                key={item.assetId}
                item={item}
                isSelected={selectedIds.has(item.assetId)}
                onToggleSelect={onToggleSelect}
              />
            ))
          )}
        </TableBody>
      </Table>
    </Card>
  )
}
