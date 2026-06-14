'use client'

import { useState, useCallback } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { toast } from '@/lib/toast'

interface ShareDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  shareUrl: string
}

export function ShareDialog({ open, onOpenChange, title, shareUrl }: ShareDialogProps) {
  const [copied, setCopied] = useState(false)

  const handleCopy = useCallback(async () => {
    await navigator.clipboard.writeText(shareUrl)
    setCopied(true)
    toast.success('Link copied to clipboard')
    setTimeout(() => setCopied(false), 2000)
  }, [shareUrl])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Share &ldquo;{title}&rdquo;</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 px-4 pb-4">
          <p className="text-sm text-muted-foreground">Copy the link below to share this asset.</p>
          <div className="flex gap-2">
            <Input readOnly value={shareUrl} className="flex-1 bg-muted" />
            <Button onClick={handleCopy}>{copied ? 'Copied!' : 'Copy link'}</Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
