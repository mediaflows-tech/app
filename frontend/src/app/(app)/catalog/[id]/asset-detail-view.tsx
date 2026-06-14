'use client'

import { useState } from 'react'
import Link from 'next/link'
import { useCatalogDetail, useCatalog } from '@/hooks/use-catalog'
import { useToggleBookmark } from '@/hooks/use-bookmarks'
import { api } from '@/lib/api'
import { MediaPlayer } from '@/components/media/media-player'
import { MediaCard } from '@/components/media/media-card'
import { CommentThread } from '@/components/comments/comment-thread'
import { ShareDialog } from '@/components/catalog/share-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { formatBytes, formatDate } from '@/lib/utils'
import {
  Bookmark,
  BookmarkCheck,
  ArrowLeft,
  Download,
  Share2,
  Eye,
  Calendar,
  User,
  HardDrive,
  MessageSquare,
  Layers,
  Sparkles
} from 'lucide-react'

interface AssetDetailViewProps {
  assetId: number
}

export function AssetDetailView({ assetId }: AssetDetailViewProps) {
  const { data: asset, isLoading, error, refetch } = useCatalogDetail(assetId)
  const toggleBookmark = useToggleBookmark()
  const { data: relatedData } = useCatalog()
  const [shareOpen, setShareOpen] = useState(false)

  if (isLoading) {
    return (
      <div className="flex-1 space-y-6 p-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="aspect-video w-full" />
        <div className="space-y-2">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <p className="text-sm text-muted-foreground">Failed to load asset details.</p>
        <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
          Retry
        </Button>
      </div>
    )
  }

  if (!asset) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <p className="text-sm text-muted-foreground">Asset not found.</p>
      </div>
    )
  }

  const handleDownload = async () => {
    const { url } = await api.get<{ url: string }>(`/catalog/${asset.id}/download-url`)
    window.open(url, '_blank')
  }

  const handleShare = async () => {
    const shareUrl = `${window.location.origin}/catalog/${asset.id}`
    if (navigator.share) {
      try {
        await navigator.share({ title: asset.title, url: shareUrl })
        return
      } catch {
        // user cancelled or share failed — fall through to modal
      }
    }
    setShareOpen(true)
  }

  const mediaUrl = (asset as any).mediaUrl || `/api/v1/catalog/${asset.id}/media`
  const relatedAssets =
    relatedData?.pages
      .flatMap((p) => p.items)
      .filter((a) => a.id !== asset.id)
      .slice(0, 4) ?? []

  return (
    <div className="flex-1 space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href="/catalog">
          <Button variant="ghost" size="icon">
            <ArrowLeft className="h-4 w-4" />
          </Button>
        </Link>
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{asset.title}</h1>
          <p className="text-sm text-muted-foreground">Uploaded {formatDate(asset.createdAt, 'long')}</p>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        {/* Main content */}
        <div className="space-y-6">
          {/* Media player */}
          <MediaPlayer
            src={mediaUrl}
            contentType={asset.contentType}
            title={asset.title}
            thumbnailUrl={asset.thumbnailUrl}
            className="w-full"
          />

          {/* Description and actions */}
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0">
              {asset.description && <p className="text-sm text-muted-foreground">{asset.description}</p>}
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <Button variant="outline" size="sm" onClick={handleShare}>
                <Share2 className="mr-1.5 h-3.5 w-3.5" />
                Share
              </Button>
              <Button variant="outline" size="sm" onClick={handleDownload}>
                <Download className="mr-1.5 h-3.5 w-3.5" />
                Download
              </Button>
              <Button
                variant={asset.isBookmarked ? 'default' : 'outline'}
                size="sm"
                onClick={() => toggleBookmark.mutate(asset.id)}
                disabled={toggleBookmark.isPending}
              >
                {asset.isBookmarked ? (
                  <BookmarkCheck className="mr-1.5 h-3.5 w-3.5" />
                ) : (
                  <Bookmark className="mr-1.5 h-3.5 w-3.5" />
                )}
                {asset.isBookmarked ? 'Bookmarked' : 'Bookmark'}
              </Button>
            </div>
          </div>

          <ShareDialog
            open={shareOpen}
            onOpenChange={setShareOpen}
            title={asset.title}
            shareUrl={
              typeof window !== 'undefined' ? `${window.location.origin}/catalog/${asset.id}` : `/catalog/${asset.id}`
            }
          />

          <Separator />

          {/* Comments section */}
          <div>
            <h2 className="mb-4 flex items-center gap-2 text-sm font-medium">
              <MessageSquare className="h-4 w-4" />
              Comments ({asset.commentCount ?? 0})
            </h2>
            <CommentThread assetId={asset.id} />
          </div>
        </div>

        {/* Sidebar metadata */}
        <aside className="space-y-6">
          {/* Asset info card */}
          <div className="rounded-md border p-4 space-y-3">
            <h3 className="text-sm font-medium">Details</h3>
            <div className="space-y-2.5 text-sm">
              <div className="flex items-center gap-2 text-muted-foreground">
                <User className="h-3.5 w-3.5" />
                <span>{asset.creatorName}</span>
              </div>
              <div className="flex items-center gap-2 text-muted-foreground">
                <Calendar className="h-3.5 w-3.5" />
                <span>{formatDate(asset.publishedAt ?? asset.createdAt, 'long')}</span>
              </div>
              <div className="flex items-center gap-2 text-muted-foreground">
                <HardDrive className="h-3.5 w-3.5" />
                <span>{formatBytes(asset.fileSize)}</span>
              </div>
              {(asset.viewCount ?? 0) > 0 && (
                <div className="flex items-center gap-2 text-muted-foreground">
                  <Eye className="h-3.5 w-3.5" />
                  <span>{asset.viewCount.toLocaleString()} views</span>
                </div>
              )}
              <div className="flex items-center gap-2 text-muted-foreground">
                <Layers className="h-3.5 w-3.5" />
                <span>
                  {asset.versionCount ?? 0} version{(asset.versionCount ?? 0) !== 1 ? 's' : ''}
                </span>
              </div>
            </div>

            {/* Dimensions for images/video */}
            {asset.metadata?.width && asset.metadata?.height && (
              <div className="pt-1">
                <p className="text-xs font-mono text-muted-foreground">
                  {asset.metadata.width} x {asset.metadata.height}
                </p>
              </div>
            )}

            {/* Duration for video/audio */}
            {asset.metadata?.durationSeconds && (
              <div className="pt-1">
                <p className="text-xs font-mono text-muted-foreground">
                  {formatDuration(asset.metadata.durationSeconds)}
                </p>
              </div>
            )}
          </div>

          {/* Tags */}
          {((asset.metadata?.tags?.length ?? 0) > 0 || (asset.metadata?.autoTags?.length ?? 0) > 0) && (
            <div className="rounded-md border p-4 space-y-3">
              <h3 className="text-sm font-medium">Tags</h3>
              {(asset.metadata?.tags?.length ?? 0) > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {asset.metadata.tags.map((tag) => (
                    <Badge key={tag} variant="secondary" className="text-xs">
                      {tag}
                    </Badge>
                  ))}
                </div>
              )}
              {(asset.metadata?.tags?.length ?? 0) > 0 && (asset.metadata?.autoTags?.length ?? 0) > 0 && <Separator />}
              {(asset.metadata?.autoTags?.length ?? 0) > 0 && (
                <div className="space-y-2">
                  <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                    <Sparkles className="h-3 w-3" />
                    AI-detected tags
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {asset.metadata.autoTags.map((tag) => (
                      <Badge key={tag.name} variant="outline" className="text-[10px] font-mono">
                        {tag.name} <span className="ml-1 text-muted-foreground">{tag.confidence.toFixed(1)}%</span>
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Related assets */}
          {relatedAssets.length > 0 && (
            <div className="space-y-3">
              <h3 className="text-sm font-medium">Related</h3>
              <div className="grid grid-cols-2 gap-2">
                {relatedAssets.map((related) => (
                  <MediaCard
                    key={related.id}
                    id={related.id}
                    title={related.title}
                    thumbnailUrl={related.thumbnailUrl}
                    contentType={related.contentType}
                    creatorName={related.creatorName}
                    createdAt={related.createdAt}
                    publishedAt={related.publishedAt}
                  />
                ))}
              </div>
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}

function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = Math.floor(seconds % 60)
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
  return `${m}:${String(s).padStart(2, '0')}`
}
