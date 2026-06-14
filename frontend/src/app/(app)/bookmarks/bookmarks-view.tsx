'use client'

import { useBookmarks, useToggleBookmark } from '@/hooks/use-bookmarks'
import { MediaCard } from '@/components/media/media-card'
import { MasonryGrid } from '@/components/media/masonry-grid'
import { InfiniteScrollTrigger } from '@/components/shared/infinite-scroll'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Bookmark, X } from 'lucide-react'

export function BookmarksView() {
  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useBookmarks()
  const toggleBookmark = useToggleBookmark()

  const allBookmarks = data?.pages.flatMap((page) => page.items) ?? []

  if (isLoading) {
    return (
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
    )
  }

  if (allBookmarks.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <Bookmark className="h-10 w-10 text-muted-foreground/40" />
        <p className="mt-3 text-sm text-muted-foreground">
          No bookmarks yet. Browse the catalog and save assets you like.
        </p>
      </div>
    )
  }

  return (
    <>
      <MasonryGrid>
        {allBookmarks.map((asset) => (
          <div key={asset.id} className="group relative mb-4">
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
            {/* Remove bookmark button */}
            <Button
              variant="secondary"
              size="icon"
              className="absolute right-2 top-2 z-10 h-7 w-7 opacity-0 shadow-sm transition-opacity group-hover:opacity-100"
              onClick={(e) => {
                e.preventDefault()
                e.stopPropagation()
                toggleBookmark.mutate(asset.id)
              }}
              disabled={toggleBookmark.isPending}
            >
              <X className="h-3.5 w-3.5" />
            </Button>
          </div>
        ))}
      </MasonryGrid>

      <InfiniteScrollTrigger onLoadMore={fetchNextPage} hasMore={!!hasNextPage} isLoading={isFetchingNextPage} />
    </>
  )
}
