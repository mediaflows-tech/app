'use client'

import { useBatchPublish } from '@/hooks/use-reviews'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'

interface BatchApproveDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  assetIds: number[]
  onSuccess: () => void
}

export function BatchApproveDialog({ open, onOpenChange, assetIds, onSuccess }: BatchApproveDialogProps) {
  const batchPublish = useBatchPublish()

  const handleConfirm = () => {
    batchPublish.mutate(
      { assetIds },
      {
        onSuccess: () => {
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
          <DialogTitle>Approve & Publish Selected Assets</DialogTitle>
          <DialogDescription>
            Are you sure you want to approve and immediately publish <strong>{assetIds.length}</strong> selected
            asset(s)?
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleConfirm} disabled={batchPublish.isPending}>
            {batchPublish.isPending ? 'Publishing...' : 'Approve & Publish'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
