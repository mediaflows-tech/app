'use client'

import { useState } from 'react'
import type { ReviewListItemDto } from '@/types/api'
import { Button } from '@/components/ui/button'
import { BatchApproveDialog } from './batch-approve-dialog'
import { BatchRejectDialog } from './batch-reject-dialog'
import { BatchScheduleDialog } from './batch-schedule-dialog'
import { Check, X, CalendarClock } from 'lucide-react'

interface BatchActionBarProps {
  selectedIds: number[]
  selectedItems: ReviewListItemDto[]
  onClearSelection: () => void
}

export function BatchActionBar({ selectedIds, selectedItems, onClearSelection }: BatchActionBarProps) {
  const [approveOpen, setApproveOpen] = useState(false)
  const [rejectOpen, setRejectOpen] = useState(false)
  const [scheduleOpen, setScheduleOpen] = useState(false)

  return (
    <>
      <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3 rounded-lg border border-[var(--glass-border)] bg-[var(--glass-bg)] backdrop-blur-2xl px-4 py-3 shadow-lg">
        <span className="text-sm font-medium">{selectedIds.length} selected</span>
        <div className="h-4 w-px bg-border" />
        <Button size="sm" onClick={() => setApproveOpen(true)}>
          <Check className="mr-1 h-4 w-4" />
          Approve & Publish
        </Button>
        <Button size="sm" variant="outline" onClick={() => setRejectOpen(true)}>
          <X className="mr-1 h-4 w-4" />
          Reject
        </Button>
        <Button size="sm" variant="outline" onClick={() => setScheduleOpen(true)}>
          <CalendarClock className="mr-1 h-4 w-4" />
          Schedule
        </Button>
        <div className="h-4 w-px bg-border" />
        <Button size="sm" variant="ghost" onClick={onClearSelection}>
          Clear
        </Button>
      </div>

      <BatchApproveDialog
        open={approveOpen}
        onOpenChange={setApproveOpen}
        assetIds={selectedIds}
        onSuccess={onClearSelection}
      />
      <BatchRejectDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        assetIds={selectedIds}
        onSuccess={onClearSelection}
      />
      <BatchScheduleDialog
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        selectedItems={selectedItems}
        onSuccess={onClearSelection}
      />
    </>
  )
}
