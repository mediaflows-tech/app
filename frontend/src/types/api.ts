export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  hasMore: boolean
}

export type AssetStatus =
  | 'Draft'
  | 'Submitted'
  | 'PendingReview'
  | 'Approved'
  | 'Published'
  | 'Rejected'
  | 'ChangesRequested'
  | 'Archived'
  | 'Quarantined'

export interface MediaAssetSummaryDto {
  id: number
  title: string
  thumbnailUrl: string | null
  previewUrl: string | null
  status: AssetStatus
  contentType: string
  fileSize: number
  creatorName: string
  createdAt: string
  publishedAt: string | null
  tags: string[]
}

export interface AssetDetailDto {
  id: number
  title: string
  description: string | null
  s3Key: string
  thumbnailUrl: string | null
  contentType: string
  fileSize: number
  status: AssetStatus
  creatorId: string
  creatorName: string
  viewCount: number
  isBookmarked: boolean
  commentCount: number
  versionCount: number
  scheduledPublishAt: string | null
  publishedAt: string | null
  metadata: MediaMetadata
  createdAt: string
  updatedAt: string
}

/** Wire shape of GET /assets/{id} — the asset is wrapped in an envelope. */
export interface AssetDetailApiResponse {
  asset: AssetDetailDto
  mediaUrl: string
  viewCount?: number
  comments: unknown[]
  commentCount: number
}

/** Asset detail as consumed by the UI — the envelope flattened onto the asset. */
export type AssetDetailView = AssetDetailDto & { mediaUrl: string }

export interface MediaMetadata {
  width: number | null
  height: number | null
  durationSeconds: number | null
  autoTags: AssetTag[]
  tags: string[]
}

export interface AssetTag {
  name: string
  confidence: number
}

export interface CommentDto {
  id: number
  assetId: number
  userId: string
  userName: string
  content: string
  parentCommentId: number | null
  replies: CommentDto[]
  isOwner: boolean
  createdAt: string
  updatedAt: string
}

export interface SearchResultDto {
  id: number
  title: string
  headline: string
  thumbnailUrl: string | null
  contentType: string
  creatorName: string
  createdAt: string
}

export interface UploadPresignedUrlResponse {
  uploadUrl: string
  s3Key: string
  expiresAt: string
}

export interface ReviewListItemDto {
  id: number
  assetId: number
  title: string
  thumbnailUrl: string | null
  contentType: string
  status: AssetStatus
  fileSize: number
  creatorName: string
  createdAt: string
}

export interface ReviewDetailsDto {
  asset: AssetDetailDto
  mediaUrl: string
  canReview: boolean
  canSchedule: boolean
  reviewHistory: ReviewHistoryItemDto[]
}

/** Shape the review detail API actually returns (assetId instead of id, reviewHistory inside asset) */
export interface ReviewDetailsApiResponse {
  asset: {
    assetId: number
    title: string
    creatorName: string
    status: AssetStatus
    description?: string | null
    contentType?: string
    fileSize?: number
    thumbnailUrl?: string | null
    createdAt?: string
    updatedAt?: string
    scheduledPublishAt?: string | null
    reviewHistory: ReviewHistoryItemDto[]
  }
  mediaUrl: string
  canReview: boolean
  canSchedule: boolean
}

export interface ReviewHistoryItemDto {
  decision: string
  reviewerName: string
  comments: string | null
  reviewedAt: string
}

export interface CognitoUserDto {
  userId: string
  username: string
  email: string
  displayName: string
  role: string
  status: string
  enabled: boolean
  createdAt: string
  lastModifiedAt: string | null
}

export interface AuditLogDto {
  id: number
  action: string
  entityType: string
  entityId: string
  userId: string | null
  userEmail: string | null
  details: string | null
  timestamp: string
}

export interface AnalyticsSnapshotDto {
  totalUsers: number
  totalAssets: number
  pendingReviews: number
  storageUsedBytes: number
  cpuUtilization: number
  memoryUtilization: number
  requestLatencyP95: number
  errorRate: number
  errorCount: number
  lambdaColdStarts: number
  estimatedDailyCost: number | null
  systemHealth?: string
  uploadsPerMinute: number
  reviewsPerMinute: number
  activeAlarms: CloudWatchAlarmDto[]
}

export interface CloudWatchAlarmDto {
  alarmName: string
  stateValue: string
  metricName: string
  stateUpdatedTimestamp: string
}

export interface NotificationDto {
  id: number
  title: string
  message: string
  type: string
  isRead: boolean
  createdAt: string
}

export type ReviewDecision = 'Approved' | 'Rejected' | 'ChangesRequested'

// Actual /api/v1/admin/summary response shape (AdminDashboardViewModel)
export interface AdminDashboardViewModelDto {
  totalUsers: number
  totalAssets: number
  storageUsedFormatted: string
  pendingReviews: number
  systemHealth: string
  activeAlarms: CloudWatchAlarmDto[]
  activityLabels: string[]
  activityData: number[]
  storageTypeLabels: string[]
  storageTypeData: number[]
}

// Actual /api/v1/admin/audit-logs response shape
export interface AuditLogsResponseDto {
  logs: PagedResult<AuditLogDto>
  actionTypes: string[]
}

export interface ScheduleEventDto {
  id: string
  title: string
  start: string
  backgroundColor: string
  extendedProps: {
    assetId: number
    thumbnailUrl: string
    status: string
  }
}

export interface ScheduledPublishDto {
  assetId: number
  title: string
  thumbnailUrl: string | null
  scheduledPublishAt: string | null
  status: AssetStatus
}

export interface AvailableAssetDto {
  id: number
  title: string
}

export interface AdminSummaryDto {
  totalUsers: number
  totalAssets: number
  pendingReviews: number
  storageUsedBytes: number
  storageUsedFormatted?: string
  storageLimitBytes: number
  storageByType: StorageByType[]
  alarms: CloudWatchAlarmDto[]
  activityLabels?: string[]
  activityData?: number[]
  systemHealth?: string
}

export interface StorageByType {
  name: string
  value: number
}

export interface ActivityDataPoint {
  date: string
  uploads: number
  reviews: number
}
