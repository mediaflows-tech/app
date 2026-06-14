'use client'

import Link from 'next/link'
import { useQuery } from '@tanstack/react-query'
import { Package } from 'lucide-react'
import { api } from '@/lib/api'
import type { PagedResult, MediaAssetSummaryDto } from '@/types/api'

export function RecentAssets() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard', 'recent-assets'],
    queryFn: () => api.get<PagedResult<MediaAssetSummaryDto>>('/catalog?page=1'),
    staleTime: 60_000
  })

  const assets = data?.items?.slice(0, 8) ?? []

  if (isLoading) {
    return (
      <section className="space-y-3">
        <h3 className="text-sm font-medium text-muted-foreground">Recently Published</h3>
        <div className="flex gap-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-24 w-24 shrink-0 animate-pulse rounded-lg bg-muted" />
          ))}
        </div>
      </section>
    )
  }

  if (assets.length === 0) return null

  return (
    <section className="space-y-3">
      <h3 className="text-sm font-medium text-muted-foreground">Recently Published</h3>
      <div className="flex gap-3 overflow-x-auto pb-2">
        {assets.map((asset) => (
          <Link key={asset.id} href={`/catalog/${asset.id}`} className="group shrink-0">
            <div className="h-24 w-24 overflow-hidden rounded-lg border border-border/50 bg-muted">
              {asset.thumbnailUrl ? (
                <img
                  src={asset.thumbnailUrl}
                  alt={asset.title}
                  className="h-full w-full object-cover transition-transform group-hover:scale-105"
                />
              ) : (
                <div className="flex h-full items-center justify-center text-muted-foreground">
                  <Package className="h-6 w-6" />
                </div>
              )}
            </div>
            <p className="mt-1 w-24 truncate text-xs text-muted-foreground transition-colors group-hover:text-foreground">
              {asset.title}
            </p>
          </Link>
        ))}
      </div>
    </section>
  )
}
