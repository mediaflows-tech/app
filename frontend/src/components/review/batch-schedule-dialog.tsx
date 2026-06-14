'use client'

import { useMemo, useState } from 'react'
import type { ReviewListItemDto } from '@/types/api'
import { REVIEW_ACTIONABLE_STATUSES } from '@/lib/review-ui'
import { useBatchApproveAndSchedule } from '@/hooks/use-reviews'
import { getMinDateTime } from '@/lib/utils'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { DateTimePickerInput } from '@/components/ui/datetime-picker-input'
import { AlertTriangle } from 'lucide-react'

interface BatchScheduleDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  selectedItems: ReviewListItemDto[]
  onSuccess: () => void
}

export function BatchScheduleDialog({ open, onOpenChange, selectedItems, onSuccess }: BatchScheduleDialogProps) {
  const [scheduledDate, setScheduledDate] = useState('')
  const batchSchedule = useBatchApproveAndSchedule()

  const { eligible, skipped } = useMemo(() => {
    const eligible = selectedItems.filter((i) => REVIEW_ACTIONABLE_STATUSES.includes(i.status))
    const skipped = selectedItems.length - eligible.length
    return { eligible, skipped }
  }, [selectedItems])

  const minDateTime = getMinDateTime()

  const handleConfirm = () => {
    if (!scheduledDate || eligible.length === 0) return
    batchSchedule.mutate(
      {
        assetIds: eligible.map((i) => i.assetId),
        scheduledPublishAt: new Date(scheduledDate).toISOString()
      },
      {
        onSuccess: () => {
          setScheduledDate('')
          onOpenChange(false)
          onSuccess()
        }
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Approve & Schedule Selected Assets</DialogTitle>
          <DialogDescription>
            Approve and schedule {eligible.length} selected asset(s) for publishing.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 px-4">
          <div className="space-y-2">
            <Label>Publish Date & Time</Label>
            <DateTimePickerInput value={scheduledDate} min={minDateTime} onChange={setScheduledDate} />
          </div>

          {skipped > 0 && (
            <div className="flex items-center gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:bg-amber-950 dark:text-amber-200">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>{skipped} ineligible asset(s) were excluded.</span>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleConfirm} disabled={!scheduledDate || eligible.length === 0 || batchSchedule.isPending}>
            {batchSchedule.isPending ? 'Scheduling...' : 'Schedule'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
