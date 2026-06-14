import type { AssetStatus, ReviewDecision } from '@/types/api'

type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline'

/** Statuses a reviewer can select for batch action from the review queue.
 *  Approved assets are intentionally excluded — once a decision exists, the
 *  publish / schedule / reject-approved flow is per-asset on the details page. */
export const REVIEW_ACTIONABLE_STATUSES: AssetStatus[] = ['PendingReview', 'Submitted']

/** Asset status → Badge variant, for consistent colouring across the review UI. */
export const STATUS_VARIANT: Record<AssetStatus, BadgeVariant> = {
  Draft: 'secondary',
  Submitted: 'default',
  PendingReview: 'outline',
  Approved: 'default',
  Published: 'secondary',
  Rejected: 'destructive',
  ChangesRequested: 'outline',
  Archived: 'secondary',
  Quarantined: 'destructive'
}

/** Review decision → Badge variant. */
export const DECISION_VARIANT: Record<ReviewDecision, BadgeVariant> = {
  Approved: 'default',
  Rejected: 'destructive',
  ChangesRequested: 'outline'
}
