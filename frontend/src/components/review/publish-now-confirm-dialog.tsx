'use client'

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

interface PublishNowConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  isPublishing: boolean
  onConfirm: () => void
}

/**
 * Confirm-publish dialog shared by the canSchedule and isScheduled
 * review-form paths. Parent owns open state.
 */
export function PublishNowConfirmDialog({ open, onOpenChange, isPublishing, onConfirm }: PublishNowConfirmDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Publish this asset?</AlertDialogTitle>
          <AlertDialogDescription>
            This will publish the asset immediately and make it visible to all users.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} disabled={isPublishing}>
            {isPublishing ? 'Publishing...' : 'Publish Now'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
