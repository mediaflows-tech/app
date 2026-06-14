'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams, useRouter, usePathname } from 'next/navigation'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog'
import { LayoutGrid, List, Loader2, PackageOpen } from 'lucide-react'
import { toast } from '@/lib/toast'
import { useAssets, useSubmitAsset, useDeleteAsset } from '@/hooks/use-assets'
import type { AssetStatus } from '@/types/api'
import { AssetGrid } from './asset-grid'
import { AssetList } from './asset-list'
import { AssetBatchActionBar } from './asset-batch-action-bar'
import { InfiniteScroll } from '@/components/shared/infinite-scroll'
import { EmptyState } from '@/components/shared/empty-state'
import { Button } from '@/components/ui/button'
import Link from 'next/link'

const STATUS_TABS = ['All', 'Draft', 'PendingReview', 'Approved', 'Rejected'] as const

const FILE_TYPES = [
  { value: 'all', label: 'All Types' },
  { value: 'image', label: 'Images' },
  { value: 'video', label: 'Videos' },
  { value: 'audio', label: 'Audio' },
  { value: 'document', label: 'Documents' }
]

const SORT_OPTIONS = [
  { value: 'newest', label: 'Newest First' },
  { value: 'oldest', label: 'Oldest First' },
  { value: 'title', label: 'Title A-Z' },
  { value: 'size', label: 'Largest First' }
]

export function AssetLibrary() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()

  // Read filters from URL
  const status = (searchParams.get('status') ?? 'All') as AssetStatus | 'All'
  const fileType = searchParams.get('fileType') ?? 'all'
  const sort = (searchParams.get('sort') as 'newest' | 'oldest' | 'title' | 'size') ?? 'newest'
  const view = (searchParams.get('view') ?? 'grid') as 'grid' | 'list'

  // Sync filter to URL
  const setFilter = useCallback(
    (key: string, value: string) => {
      const params = new URLSearchParams(searchParams.toString())
      if (value === 'All' || value === 'all' || value === 'newest') {
        params.delete(key)
      } else {
        params.set(key, value)
      }
      router.replace(`${pathname}?${params.toString()}`)
    },
    [router, pathname, searchParams]
  )

  // TanStack Query
  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useAssets({
    status: status === 'All' ? undefined : status,
    fileType: fileType === 'all' ? undefined : fileType,
    sort
  })

  const assets = useMemo(() => data?.pages.flatMap((page) => page.items) ?? [], [data])

  // Selection state
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())

  const handleToggleSelect = useCallback((id: number, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (checked) next.add(id)
      else next.delete(id)
      return next
    })
  }, [])

  const handleClearSelection = useCallback(() => setSelectedIds(new Set()), [])

  // Clear selection on filter changes
  useEffect(() => {
    setSelectedIds(new Set())
  }, [status, fileType, sort])

  // Mutations
  const submitAsset = useSubmitAsset()
  const deleteAsset = useDeleteAsset()
  const [deleteId, setDeleteId] = useState<number | null>(null)

  const handleSubmit = useCallback(
    (id: number) => {
      submitAsset.mutate(id, {
        onSuccess: () => toast.success('Asset submitted for review'),
        onError: () => toast.error('Failed to submit asset')
      })
    },
    [submitAsset]
  )

  const handleDelete = useCallback((id: number) => {
    setDeleteId(id)
  }, [])

  const confirmDelete = useCallback(() => {
    if (deleteId === null) return
    deleteAsset.mutate(deleteId, {
      onSuccess: () => {
        toast.success('Asset deleted')
        setDeleteId(null)
      },
      onError: () => toast.error('Failed to delete asset')
    })
  }, [deleteId, deleteAsset])

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        {/* Status Tabs */}
        <Tabs value={status} onValueChange={(v) => setFilter('status', v as string)}>
          <TabsList>
            {STATUS_TABS.map((tab) => (
              <TabsTrigger key={tab} value={tab} className="text-xs">
                {tab === 'PendingReview' ? 'Pending' : tab}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <div className="flex items-center gap-2">
          {/* File type filter */}
          <Select value={fileType} onValueChange={(v) => setFilter('fileType', v as string)}>
            <SelectTrigger className="w-[130px] text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {FILE_TYPES.map((type) => (
                <SelectItem key={type.value} value={type.value}>
                  {type.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {/* Sort */}
          <Select value={sort} onValueChange={(v) => setFilter('sort', v as string)}>
            <SelectTrigger className="w-[140px] text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {SORT_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {/* View toggle */}
          <ToggleGroup
            value={view === 'grid' ? [view] : [view]}
            onValueChange={(v) => {
              const newView = v[0]
              if (newView) setFilter('view', newView)
            }}
          >
            <ToggleGroupItem value="grid" aria-label="Grid view" className="h-8 w-8 p-0">
              <LayoutGrid className="h-3.5 w-3.5" />
            </ToggleGroupItem>
            <ToggleGroupItem value="list" aria-label="List view" className="h-8 w-8 p-0">
              <List className="h-3.5 w-3.5" />
            </ToggleGroupItem>
          </ToggleGroup>
        </div>
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : assets.length === 0 ? (
        <EmptyState icon={PackageOpen} title="No assets found" description="Upload your first file to get started.">
          <Button render={<Link href="/creator/upload" />} size="sm">
            Upload
          </Button>
        </EmptyState>
      ) : (
        <InfiniteScroll hasMore={!!hasNextPage} isLoading={isFetchingNextPage} onLoadMore={() => fetchNextPage()}>
          {view === 'grid' ? (
            <AssetGrid
              assets={assets}
              onSubmit={handleSubmit}
              onDelete={handleDelete}
              selectedIds={selectedIds}
              onToggleSelect={handleToggleSelect}
            />
          ) : (
            <AssetList
              assets={assets}
              onSubmit={handleSubmit}
              onDelete={handleDelete}
              selectedIds={selectedIds}
              onToggleSelect={handleToggleSelect}
            />
          )}
        </InfiniteScroll>
      )}

      {/* Delete confirmation dialog */}
      <AlertDialog
        open={deleteId !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteId(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete asset?</AlertDialogTitle>
            <AlertDialogDescription>
              This action cannot be undone. The asset and all its versions will be permanently deleted.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete} variant="destructive">
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Batch action bar */}
      {selectedIds.size > 0 && (
        <AssetBatchActionBar
          selectedIds={Array.from(selectedIds)}
          selectedAssets={assets.filter((a) => selectedIds.has(a.id))}
          onClearSelection={handleClearSelection}
          onActionSuccess={handleClearSelection}
        />
      )}
    </div>
  )
}
