'use client'

import { useCallback } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useCatalog } from '@/hooks/use-catalog'
import { MediaCard } from '@/components/media/media-card'
import { MasonryGrid } from '@/components/media/masonry-grid'
import { InfiniteScrollTrigger } from '@/components/shared/infinite-scroll'
import { Skeleton } from '@/components/ui/skeleton'
import { ImageIcon } from 'lucide-react'

const CONTENT_TYPES = [
  { value: 'all', label: 'All' },
  { value: 'image', label: 'Images' },
  { value: 'video', label: 'Videos' },
  { value: 'audio', label: 'Audio' },
  { value: 'document', label: 'Documents' }
] as const

const SORT_OPTIONS = [
  { value: 'newest', label: 'Newest' },
  { value: 'trending', label: 'Trending' }
] as const

interface CatalogViewProps {
  initialType?: string
  initialSort?: string
}

export function CatalogView({ initialType, initialSort }: CatalogViewProps) {
  const router = useRouter()
  const searchParams = useSearchParams()
  const currentType = searchParams.get('type') ?? initialType ?? 'all'
  const currentSort = searchParams.get('sort') ?? initialSort ?? 'newest'

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useCatalog({
    type: currentType === 'all' ? undefined : currentType,
    sort: currentSort === 'newest' ? undefined : currentSort
  })

  const handleTypeChange = useCallback(
    (value: string) => {
      const params = new URLSearchParams(searchParams.toString())
      if (value === 'all') {
        params.delete('type')
      } else {
        params.set('type', value)
      }
      router.push(`/catalog?${params.toString()}`)
    },
    [router, searchParams]
  )

  const handleSortChange = useCallback(
    (value: string | null) => {
      const params = new URLSearchParams(searchParams.toString())
      if (!value || value === 'newest') {
        params.delete('sort')
      } else {
        params.set('sort', value)
      }
      router.push(`/catalog?${params.toString()}`)
    },
    [router, searchParams]
  )

  const allAssets = data?.pages.flatMap((page) => page.items) ?? []

  return (
    <div className="space-y-4">
      {/* Type filter tabs + Sort select */}
      <div className="flex items-center justify-between gap-3">
        <Tabs value={currentType} onValueChange={handleTypeChange}>
          <TabsList>
            {CONTENT_TYPES.map((type) => (
              <TabsTrigger key={type.value} value={type.value}>
                {type.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <Select value={currentSort} onValueChange={handleSortChange}>
          <SelectTrigger className="w-32" data-testid="catalog-sort">
            <SelectValue>
              {(value: string | null) => SORT_OPTIONS.find((opt) => opt.value === value)?.label ?? value}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {SORT_OPTIONS.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Loading skeleton */}
      {isLoading && (
        <MasonryGrid>
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="mb-4">
              <Skeleton className="aspect-[4/3] w-full rounded-md" />
              <div className="mt-2 space-y-1">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-1/2" />
              </div>
            </div>
          ))}
        </MasonryGrid>
      )}

      {/* Results grid */}
      {!isLoading && allAssets.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <ImageIcon className="h-10 w-10 text-muted-foreground/40" />
          <p className="mt-3 text-sm text-muted-foreground">No assets found for this filter.</p>
        </div>
      )}

      {!isLoading && allAssets.length > 0 && (
        <>
          <MasonryGrid>
            {allAssets.map((asset) => (
              <div key={asset.id} className="mb-4">
                <MediaCard
                  id={asset.id}
                  title={asset.title}
                  thumbnailUrl={asset.thumbnailUrl}
                  contentType={asset.contentType}
                  creatorName={asset.creatorName}
                  createdAt={asset.createdAt}
                  publishedAt={asset.publishedAt}
                  fileSize={asset.fileSize}
                  tags={asset.tags}
                />
              </div>
            ))}
          </MasonryGrid>

          <InfiniteScrollTrigger onLoadMore={fetchNextPage} hasMore={!!hasNextPage} isLoading={isFetchingNextPage} />
        </>
      )}
    </div>
  )
}
