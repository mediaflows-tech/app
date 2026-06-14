'use client'

import { useEffect, useRef, type ReactNode } from 'react'
import { Loader2 } from 'lucide-react'

interface InfiniteScrollProps {
  hasMore: boolean
  isLoading: boolean
  onLoadMore: () => void
  children: ReactNode
}

export function InfiniteScroll({ hasMore, isLoading, onLoadMore, children }: InfiniteScrollProps) {
  const sentinelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const sentinel = sentinelRef.current
    if (!sentinel || !hasMore) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !isLoading) {
          onLoadMore()
        }
      },
      { rootMargin: '200px' }
    )

    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [hasMore, isLoading, onLoadMore])

  return (
    <div>
      {children}
      <div ref={sentinelRef} className="py-4">
        {isLoading && (
          <div className="flex items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        )}
      </div>
    </div>
  )
}

interface InfiniteScrollTriggerProps {
  onLoadMore: () => void
  hasMore: boolean
  isLoading: boolean
  rootMargin?: string
}

export function InfiniteScrollTrigger({
  onLoadMore,
  hasMore,
  isLoading,
  rootMargin = '200px'
}: InfiniteScrollTriggerProps) {
  const triggerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const trigger = triggerRef.current
    if (!trigger || !hasMore || isLoading) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) {
          onLoadMore()
        }
      },
      { rootMargin }
    )

    observer.observe(trigger)
    return () => observer.disconnect()
  }, [onLoadMore, hasMore, isLoading, rootMargin])

  if (!hasMore) return null

  return (
    <div ref={triggerRef} className="flex items-center justify-center py-8">
      {isLoading && <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />}
    </div>
  )
}
