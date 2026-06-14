'use client'

import { useReviewDetail } from '@/hooks/use-reviews'
import type { ReviewDetailsDto } from '@/types/api'
import { ReviewForm } from '@/components/review/review-form'
import { StatusTimeline } from '@/components/review/status-timeline'
import { MediaPlayer } from '@/components/media/media-player'
import { CommentThread } from '@/components/comments/comment-thread'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { StatusBadge } from '@/components/ui/status-badge'
import { formatBytes, formatDate } from '@/lib/utils'
import { MessageSquare, FileImage, FileVideo, FileAudio, FileText } from 'lucide-react'

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

interface ReviewDetailClientProps {
  initialData: ReviewDetailsDto
  assetId: number
}

export function ReviewDetailClient({ initialData, assetId }: ReviewDetailClientProps) {
  const { data: fetchedData } = useReviewDetail(assetId)
  const review = fetchedData ?? initialData
  const asset = review.asset

  const canReview = review.canReview
  const canSchedule = review.canSchedule
  const isScheduled = asset.status === 'Approved' && !!asset.scheduledPublishAt

  const Icon = getFileIcon(asset.contentType)

  return (
    <>
      {/* Header */}
      <div>
        <h1 className="text-2xl font-semibold">{asset.title}</h1>
        <div className="mt-2 flex items-center gap-3">
          <StatusBadge status={asset.status} />
          <span className="text-sm text-muted-foreground">by {asset.creatorName}</span>
          <span className="text-sm text-muted-foreground">{formatDate(asset.createdAt, 'long')}</span>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_400px]">
        {/* Left: media preview */}
        <div className="space-y-4">
          <Card className="overflow-hidden p-0">
            <MediaPlayer
              src={review.mediaUrl}
              contentType={asset.contentType}
              title={asset.title}
              thumbnailUrl={asset.thumbnailUrl}
            />
          </Card>
        </div>

        {/* Right: details + review form + history */}
        <div className="space-y-6">
          {/* Asset details — matches /creator/assets/[id] design */}
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
                <span className="text-sm text-muted-foreground">Creator</span>
                <span className="text-sm">{asset.creatorName}</span>
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Uploaded</span>
                <span className="text-sm">{formatDate(asset.createdAt, 'long')}</span>
              </div>
              {asset.description && (
                <>
                  <Separator />
                  <div className="text-sm">
                    <p className="text-muted-foreground">Description</p>
                    <p className="mt-1">{asset.description}</p>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <ReviewForm
            assetId={asset.id}
            status={asset.status}
            scheduledPublishAt={asset.scheduledPublishAt}
            canReview={canReview}
            canSchedule={canSchedule}
            isScheduled={isScheduled}
          />

          <div>
            <h3 className="mb-4 text-lg font-semibold">Review History</h3>
            <StatusTimeline history={review.reviewHistory} />
          </div>
        </div>
      </div>

      {/* Comments — visible to everyone authorized to view this asset */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-medium">
            <MessageSquare className="h-4 w-4" />
            Comments
          </CardTitle>
        </CardHeader>
        <CardContent>
          <CommentThread assetId={asset.id} />
        </CardContent>
      </Card>
    </>
  )
}
