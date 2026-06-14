'use client'

import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

const STATUS_COLORS: Record<string, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Submitted: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  PendingReview: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  Approved: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
  Published: 'bg-blue-500/10 text-blue-600 dark:text-blue-400',
  Rejected: 'bg-red-500/10 text-red-600 dark:text-red-400',
  ChangesRequested: 'bg-orange-500/10 text-orange-600 dark:text-orange-400',
  Scheduled: 'bg-violet-500/10 text-violet-600 dark:text-violet-400',
  Archived: 'bg-muted text-muted-foreground',
  Quarantined: 'bg-red-500/10 text-red-600 dark:text-red-400'
}

function formatStatus(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2')
}

interface StatusBadgeProps {
  status: string
  className?: string
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  return (
    <Badge variant="secondary" className={cn('text-xs', STATUS_COLORS[status], className)}>
      {formatStatus(status)}
    </Badge>
  )
}
