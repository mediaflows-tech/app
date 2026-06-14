'use client'

import { useState } from 'react'
import { Button } from '@/components/ui/button'
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
import { Send, Trash2, Loader2 } from 'lucide-react'
import { toast } from '@/lib/toast'
import { useBulkSubmitAssets, useBulkDeleteAssets } from '@/hooks/use-assets'
import type { MediaAssetSummaryDto } from '@/types/api'

interface AssetBatchActionBarProps {
  selectedIds: number[]
  selectedAssets: MediaAssetSummaryDto[]
  onClearSelection: () => void
  onActionSuccess: () => void
}

export function AssetBatchActionBar({
  selectedIds,
  selectedAssets,
  onClearSelection,
  onActionSuccess
}: AssetBatchActionBarProps) {
  const [bulkSubmitOpen, setBulkSubmitOpen] = useState(false)
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false)

  const bulkSubmit = useBulkSubmitAssets()
  const bulkDelete = useBulkDeleteAssets()

  const hasSubmittable = selectedAssets.some((a) => a.status === 'Draft')
  const hasDeletable = selectedAssets.some((a) => a.status === 'Draft' || a.status === 'Rejected')

  const submittableIds = selectedAssets.filter((a) => a.status === 'Draft').map((a) => a.id)
  const deletableIds = selectedAssets.filter((a) => a.status === 'Draft' || a.status === 'Rejected').map((a) => a.id)

  return (
    <>
      <div
        className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3
                      rounded-lg border border-[var(--glass-border)] bg-[var(--glass-bg)]
                      backdrop-blur-2xl px-4 py-3 shadow-lg"
      >
        <span className="text-sm font-medium">{selectedIds.length} selected</span>
        <div className="h-4 w-px bg-border" />

        {hasSubmittable && (
          <Button size="sm" onClick={() => setBulkSubmitOpen(true)} disabled={bulkSubmit.isPending}>
            {bulkSubmit.isPending ? (
              <Loader2 className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-1 h-4 w-4" />
            )}
            Submit for Review
          </Button>
        )}

        {hasDeletable && (
          <Button size="sm" variant="outline" onClick={() => setBulkDeleteOpen(true)} disabled={bulkDelete.isPending}>
            {bulkDelete.isPending ? (
              <Loader2 className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <Trash2 className="mr-1 h-4 w-4" />
            )}
            Delete
          </Button>
        )}

        <div className="h-4 w-px bg-border" />
        <Button size="sm" variant="ghost" onClick={onClearSelection}>
          Clear
        </Button>
      </div>

      {/* Bulk Submit Dialog */}
      <AlertDialog open={bulkSubmitOpen} onOpenChange={setBulkSubmitOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Submit {submittableIds.length} assets for review?</AlertDialogTitle>
            <AlertDialogDescription>
              {submittableIds.length} draft asset{submittableIds.length !== 1 ? 's' : ''} will be submitted for review.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                bulkSubmit.mutate(submittableIds, {
                  onSuccess: (result) => {
                    toast.success(`${result.submitted} asset${result.submitted !== 1 ? 's' : ''} submitted for review`)
                    setBulkSubmitOpen(false)
                    onActionSuccess()
                  },
                  onError: () => toast.error('Failed to submit assets')
                })
              }}
            >
              Submit
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Bulk Delete Dialog */}
      <AlertDialog open={bulkDeleteOpen} onOpenChange={setBulkDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {deletableIds.length} assets?</AlertDialogTitle>
            <AlertDialogDescription>
              This action cannot be undone. {deletableIds.length} asset{deletableIds.length !== 1 ? 's' : ''} will be
              permanently deleted.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                bulkDelete.mutate(deletableIds, {
                  onSuccess: (result) => {
                    toast.success(`${result.deleted} asset${result.deleted !== 1 ? 's' : ''} deleted`)
                    setBulkDeleteOpen(false)
                    onActionSuccess()
                  },
                  onError: () => toast.error('Failed to delete assets')
                })
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
