'use client'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'

interface ApprovePublishConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  isSubmitting: boolean
  onConfirm: () => void
}

/**
 * Confirm dialog for the single-asset Approve & Publish action on the review
 * details page. Mirrors the BatchApproveDialog used in the queue's batch flow.
 */
export function ApprovePublishConfirmDialog({
  open,
  onOpenChange,
  isSubmitting,
  onConfirm
}: ApprovePublishConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Approve & Publish Asset</DialogTitle>
          <DialogDescription>Are you sure you want to approve and immediately publish this asset?</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={onConfirm} disabled={isSubmitting}>
            {isSubmitting ? 'Publishing...' : 'Approve & Publish'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
