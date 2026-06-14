'use client'

import type { ReviewHistoryItemDto, ReviewDecision } from '@/types/api'
import { Check, X, Pencil } from 'lucide-react'
import { StatusBadge } from '@/components/ui/status-badge'
import { formatDate } from '@/lib/utils'

const DECISION_ICON: Record<ReviewDecision, React.ReactNode> = {
  Approved: <Check className="h-3.5 w-3.5" />,
  Rejected: <X className="h-3 w-3" />,
  ChangesRequested: <Pencil className="h-2.5 w-2.5" />
}

interface StatusTimelineProps {
  history: ReviewHistoryItemDto[]
}

export function StatusTimeline({ history }: StatusTimelineProps) {
  if (history.length === 0) {
    return <div className="py-6 text-center text-sm text-muted-foreground">No review history yet</div>
  }

  return (
    <div className="flex flex-col">
      {history.map((item, index) => (
        <div key={index} className="relative flex gap-3 pb-6">
          {index < history.length - 1 && <div className="absolute left-[15px] top-8 bottom-0 w-px bg-border" />}

          <div className="z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted">
            {DECISION_ICON[item.decision as ReviewDecision]}
          </div>

          <div className="min-w-0 flex-1">
            <div className="mb-1 flex items-center gap-2">
              <span className="text-sm font-medium">{item.reviewerName}</span>
              <StatusBadge status={item.decision} />
            </div>
            <p className="text-xs text-muted-foreground">{formatDate(item.reviewedAt, 'long')}</p>
            {item.comments && <p className="mt-2 text-sm text-muted-foreground">{item.comments}</p>}
          </div>
        </div>
      ))}
    </div>
  )
}
