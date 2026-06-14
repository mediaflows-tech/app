'use client'

import { useAsset } from '@/hooks/use-assets'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { StatusBadge } from '@/components/ui/status-badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { MediaPlayer } from '@/components/media/media-player'
import { CommentThread } from '@/components/comments/comment-thread'
import { formatBytes, formatDate, formatStatus } from '@/lib/utils'
import { ArrowLeft, GitBranch, Send, FileImage, FileVideo, FileAudio, FileText } from 'lucide-react'
import { TagEditor } from '@/components/tags/tag-editor'
import Link from 'next/link'
import { useSubmitAsset } from '@/hooks/use-assets'
import { toast } from '@/lib/toast'
import type { AssetStatus } from '@/types/api'

function getFileTypeLabel(contentType?: string | null) {
  if (!contentType) return 'Document'
  if (contentType.startsWith('image/')) return 'Image'
  if (contentType.startsWith('video/')) return 'Video'
  if (contentType.startsWith('audio/')) return 'Audio'
  return 'Document'
}

function getFileIcon(contentType?: string | null) {
  if (!contentType) return FileText
  if (contentType.startsWith('image/')) return FileImage
  if (contentType.startsWith('video/')) return FileVideo
  if (contentType.startsWith('audio/')) return FileAudio
  return FileText
}

interface AssetDetailContentProps {
  assetId: number
}

export function AssetDetailContent({ assetId }: AssetDetailContentProps) {
  const { data: asset, isLoading, error, refetch } = useAsset(assetId)
  const submitAsset = useSubmitAsset()

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="aspect-video w-full rounded-lg" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center py-20 text-center">
        <p className="text-sm text-muted-foreground">Failed to load asset details.</p>
        <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
          Retry
        </Button>
      </div>
    )
  }

  if (!asset) {
    return <div className="py-20 text-center text-muted-foreground">Asset not found</div>
  }

  const Icon = getFileIcon(asset.contentType)

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/creator/assets">
            <Button variant="ghost" size="icon">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{asset.title}</h1>
            <p className="text-sm text-muted-foreground">Uploaded {formatDate(asset.createdAt, 'long')}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {asset.status === 'Draft' && (
            <Button
              size="sm"
              onClick={() =>
                submitAsset.mutate(assetId, {
                  onSuccess: () => toast.success('Asset submitted for review'),
                  onError: () => toast.error('Failed to submit asset')
                })
              }
              disabled={submitAsset.isPending}
            >
              <Send className="mr-1 h-3.5 w-3.5" />
              Submit for Review
            </Button>
          )}
          <Button variant="outline" size="sm" render={<Link href={`/creator/assets/${assetId}/versions`} />}>
            <GitBranch className="mr-1 h-3.5 w-3.5" />
            Versions
          </Button>
        </div>
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Media Preview */}
        <div className="lg:col-span-2">
          <MediaPlayer
            src={(asset as any).mediaUrl || `/api/v1/assets/${assetId}/media`}
            contentType={asset.contentType}
            title={asset.title}
            thumbnailUrl={asset.thumbnailUrl}
            className="w-full"
          />
        </div>

        {/* Metadata Sidebar */}
        <div className="space-y-4">
          {/* Status & Info */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Status</span>
                <StatusBadge status={asset.status} />
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Type</span>
                <div className="flex items-center gap-1.5 text-sm">
                  <Icon className="h-3.5 w-3.5" />
                  {getFileTypeLabel(asset.contentType)}
                </div>
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Size</span>
                <span className="text-sm">{formatBytes(asset.fileSize)}</span>
              </div>
              {asset.metadata?.width && asset.metadata?.height && (
                <>
                  <Separator />
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-muted-foreground">Dimensions</span>
                    <span className="text-sm">
                      {asset.metadata.width} x {asset.metadata.height}
                    </span>
                  </div>
                </>
              )}
              {asset.metadata?.durationSeconds && (
                <>
                  <Separator />
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-muted-foreground">Duration</span>
                    <span className="text-sm">
                      {Math.floor(asset.metadata.durationSeconds / 60)}:
                      {String(Math.floor(asset.metadata.durationSeconds % 60)).padStart(2, '0')}
                    </span>
                  </div>
                </>
              )}
              <Separator />
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Versions</span>
                <span className="text-sm">{asset.versionCount}</span>
              </div>
              {asset.viewCount > 0 && (
                <>
                  <Separator />
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-muted-foreground">Views</span>
                    <span className="text-sm">{asset.viewCount}</span>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          {/* Tag Editor (inline title, description, tags) */}
          <TagEditor assetId={assetId} />
        </div>
      </div>

      {/* Comments */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium">Comments ({asset.commentCount})</CardTitle>
        </CardHeader>
        <CardContent>
          <CommentThread assetId={assetId} />
        </CardContent>
      </Card>
    </div>
  )
}
