import type { ReviewDetailsApiResponse, ReviewDetailsDto } from '@/types/api'

/**
 * Normalises the review-detail response shape emitted by the backend
 * (which uses `assetId` and nests reviewHistory inside `asset`) into the
 * flat `ReviewDetailsDto` shape the frontend components expect.
 *
 * Fields not present on the review-detail payload (s3Key, creatorId,
 * viewCount, bookmarks, commentCount, versionCount, metadata) are filled
 * with empty/zero defaults — the review detail page doesn't use them,
 * but `ReviewDetailsDto.asset` reuses the fuller `AssetDetailDto` type.
 */
export function toReviewDetails(raw: ReviewDetailsApiResponse, fallbackAssetId: number): ReviewDetailsDto {
  const a = raw.asset
  return {
    asset: {
      id: a.assetId ?? fallbackAssetId,
      title: a.title ?? '',
      description: a.description ?? null,
      s3Key: '',
      thumbnailUrl: a.thumbnailUrl ?? null,
      contentType: a.contentType ?? '',
      fileSize: a.fileSize ?? 0,
      status: a.status,
      creatorId: '',
      creatorName: a.creatorName ?? '',
      viewCount: 0,
      isBookmarked: false,
      commentCount: 0,
      versionCount: 0,
      scheduledPublishAt: a.scheduledPublishAt ?? null,
      publishedAt: null,
      metadata: { width: null, height: null, durationSeconds: null, autoTags: [], tags: [] },
      createdAt: a.createdAt ?? '',
      updatedAt: a.updatedAt ?? ''
    },
    mediaUrl: raw.mediaUrl,
    canReview: raw.canReview,
    canSchedule: raw.canSchedule,
    reviewHistory: a.reviewHistory ?? []
  }
}
