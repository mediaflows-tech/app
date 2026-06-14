'use client'

import { useState } from 'react'
import { useBatchDecide } from '@/hooks/use-reviews'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'

interface BatchRejectDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  assetIds: number[]
  onSuccess: () => void
}

export function BatchRejectDialog({ open, onOpenChange, assetIds, onSuccess }: BatchRejectDialogProps) {
  const [comments, setComments] = useState('')
  const batchDecide = useBatchDecide()

  const handleConfirm = () => {
    if (!comments.trim()) return
    batchDecide.mutate(
      { assetIds, decision: 'Rejected', comments },
      {
        onSuccess: () => {
          setComments('')
          onOpenChange(false)
          onSuccess()
        }
      }
    )
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Reject Selected Assets</AlertDialogTitle>
          <AlertDialogDescription>This will reject {assetIds.length} selected asset(s).</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2 px-4">
          <Label htmlFor="reject-comments">Reason for rejection</Label>
          <Textarea
            id="reject-comments"
            placeholder="Provide a reason for rejecting these assets..."
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            rows={3}
            required
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={!comments.trim() || batchDecide.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {batchDecide.isPending ? 'Rejecting...' : 'Reject'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
