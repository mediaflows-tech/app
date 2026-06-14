'use client'

import { useCallback, useMemo, useState } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'
import { useReviews, type ReviewFilters } from '@/hooks/use-reviews'
import { REVIEW_ACTIONABLE_STATUSES } from '@/lib/review-ui'
import { ReviewTable } from './review-table'
import { BatchActionBar } from './batch-action-bar'
import { PageHeader } from '@/components/shared/page-header'
import { Card, CardContent } from '@/components/ui/card'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { Button } from '@/components/ui/button'
import { List, CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react'

const STATUS_TABS = [
  { value: 'all', label: 'All' },
  { value: 'PendingReview', label: 'Pending' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' }
] as const

const CONTENT_TYPES = [
  { value: 'all', label: 'All Types' },
  { value: 'image', label: 'Images' },
  { value: 'video', label: 'Video' },
  { value: 'audio', label: 'Audio' }
] as const

export function ReviewQueue() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())
  const [viewMode, setViewMode] = useState<'list' | 'grouped'>('list')

  const filters: ReviewFilters = useMemo(
    () => ({
      page: Number(searchParams.get('page')) || 1,
      status: searchParams.get('status') || undefined,
      contentType: searchParams.get('contentType') || undefined,
      sortDir: searchParams.get('sortDir') || 'desc'
    }),
    [searchParams]
  )

  const { data, isLoading, error, refetch } = useReviews(filters)

  const updateFilter = useCallback(
    (key: string, value: string) => {
      const params = new URLSearchParams(searchParams.toString())
      if (value && value !== 'all') {
        params.set(key, value)
      } else {
        params.delete(key)
      }
      if (key !== 'page') {
        params.delete('page')
      }
      setSelectedIds(new Set())
      router.push(`/review?${params.toString()}`)
    },
    [searchParams, router]
  )

  const handleSelectAll = useCallback(
    (checked: boolean) => {
      if (!data?.items) return
      if (checked) {
        const selectableIds = data.items
          .filter((item) => REVIEW_ACTIONABLE_STATUSES.includes(item.status))
          .map((item) => item.assetId)
        setSelectedIds(new Set(selectableIds))
      } else {
        setSelectedIds(new Set())
      }
    },
    [data]
  )

  const handleToggleSelect = useCallback((id: number, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (checked) next.add(id)
      else next.delete(id)
      return next
    })
  }, [])

  // Use server-provided counts (preferred), with client-side fallback
  const pendingCount = data?.counts?.pending ?? 0
  const approvedCount = data?.counts?.approved ?? 0
  const rejectedCount = data?.counts?.rejected ?? 0

  const currentPage = filters.page ?? 1
  const totalPages = data ? Math.ceil((data.totalCount ?? 0) / (data.pageSize || 20)) : 1

  if (error) {
    return (
      <div className="space-y-8">
        <PageHeader title="Review Queue" description="Review and approve submitted assets" />
        <div className="flex flex-col items-center py-16 text-center">
          <p className="text-sm text-muted-foreground">Failed to load reviews.</p>
          <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-8">
      <PageHeader title="Review Queue" description="Review and approve submitted assets" />

      {/* Metric cards */}
      <div className="grid grid-cols-3 gap-4">
        <Card size="sm">
          <CardContent>
            <p className="text-sm text-muted-foreground">Pending</p>
            <p className="text-2xl font-semibold">{pendingCount}</p>
          </CardContent>
        </Card>
        <Card size="sm">
          <CardContent>
            <p className="text-sm text-muted-foreground">Approved</p>
            <p className="text-2xl font-semibold">{approvedCount}</p>
          </CardContent>
        </Card>
        <Card size="sm">
          <CardContent>
            <p className="text-sm text-muted-foreground">Rejected</p>
            <p className="text-2xl font-semibold">{rejectedCount}</p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <div className="flex items-center justify-between">
        <Tabs value={filters.status || 'all'} onValueChange={(v: string | null) => updateFilter('status', v ?? 'all')}>
          <TabsList>
            {STATUS_TABS.map((tab) => (
              <TabsTrigger key={tab.value} value={tab.value}>
                {tab.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <div className="flex items-center gap-2">
          <ToggleGroup
            value={[viewMode]}
            onValueChange={(v: string[]) => {
              if (v.length > 0) setViewMode(v[v.length - 1] as 'list' | 'grouped')
            }}
          >
            <ToggleGroupItem value="list" aria-label="List view">
              <List className="h-4 w-4" />
            </ToggleGroupItem>
            <ToggleGroupItem value="grouped" aria-label="Group by date">
              <CalendarDays className="h-4 w-4" />
            </ToggleGroupItem>
          </ToggleGroup>

          <Select
            value={filters.contentType || 'all'}
            onValueChange={(v: string | null) => updateFilter('contentType', v ?? 'all')}
          >
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="All Types" />
            </SelectTrigger>
            <SelectContent>
              {CONTENT_TYPES.map((ct) => (
                <SelectItem key={ct.value} value={ct.value}>
                  {ct.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={filters.sortDir || 'desc'}
            onValueChange={(v: string | null) => updateFilter('sortDir', v ?? 'desc')}
          >
            <SelectTrigger className="w-[140px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="desc">Newest First</SelectItem>
              <SelectItem value="asc">Oldest First</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Table */}
      <ReviewTable
        items={data?.items ?? []}
        isLoading={isLoading}
        selectedIds={selectedIds}
        onSelectAll={handleSelectAll}
        onToggleSelect={handleToggleSelect}
        viewMode={viewMode}
      />

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="icon-sm"
            disabled={currentPage <= 1}
            onClick={() => updateFilter('page', String(currentPage - 1))}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {currentPage} of {totalPages}
          </span>
          <Button
            variant="outline"
            size="icon-sm"
            disabled={currentPage >= totalPages}
            onClick={() => updateFilter('page', String(currentPage + 1))}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}

      {/* Batch action bar */}
      {selectedIds.size > 0 && (
        <BatchActionBar
          selectedIds={Array.from(selectedIds)}
          selectedItems={data?.items?.filter((i) => selectedIds.has(i.assetId)) ?? []}
          onClearSelection={() => setSelectedIds(new Set())}
        />
      )}
    </div>
  )
}
