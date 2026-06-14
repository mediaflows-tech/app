'use client'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface RejectConfirmationProps {
  open: boolean
  comments: string
  isSubmitting: boolean
  onCommentsChange: (value: string) => void
  onCancel: () => void
  onConfirm: () => void
}

/**
 * Inline "Reason for rejection" form with Confirm/Cancel buttons.
 * Parent owns the open state and the comments state so this stays pure.
 */
export function RejectConfirmation({
  open,
  comments,
  isSubmitting,
  onCommentsChange,
  onCancel,
  onConfirm
}: RejectConfirmationProps) {
  if (!open) return null

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        <Label>Reason for rejection</Label>
        <Textarea
          placeholder="Provide a reason for rejecting this asset..."
          value={comments}
          onChange={(e) => onCommentsChange(e.target.value)}
          rows={3}
          required
        />
      </div>
      <div className="flex gap-2">
        <Button size="sm" variant="destructive" onClick={onConfirm} disabled={!comments.trim() || isSubmitting}>
          Confirm Reject
        </Button>
        <Button size="sm" variant="outline" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </div>
  )
}
