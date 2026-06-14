'use client'

import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger
} from '@/components/ui/dialog'
import { Progress } from '@/components/ui/progress'
import { Upload, Loader2 } from 'lucide-react'
import { toast } from '@/lib/toast'
import { usePresign, uploadToS3 } from '@/hooks/use-upload'
import { useUploadVersion } from '@/hooks/use-versions'

interface VersionUploadProps {
  assetId: number
}

export function VersionUpload({ assetId }: VersionUploadProps) {
  const [open, setOpen] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [notes, setNotes] = useState('')
  const [progress, setProgress] = useState(0)
  const [isUploading, setIsUploading] = useState(false)

  const presign = usePresign()
  const uploadVersion = useUploadVersion()

  const handleUpload = async () => {
    if (!file) return

    setIsUploading(true)
    setProgress(0)

    try {
      const presigned = await presign.mutateAsync({
        fileName: file.name,
        contentType: file.type
      })

      await uploadToS3(presigned.uploadUrl, file, setProgress)

      await uploadVersion.mutateAsync({
        assetId,
        s3Key: presigned.s3Key,
        contentType: file.type,
        fileSize: file.size,
        notes: notes || undefined
      })

      toast.success('New version uploaded')
      setOpen(false)
      setFile(null)
      setNotes('')
    } catch {
      toast.error('Failed to upload new version')
    } finally {
      setIsUploading(false)
      setProgress(0)
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button size="sm" />}>
        <Upload className="mr-1 h-3.5 w-3.5" />
        Upload New Version
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Upload New Version</DialogTitle>
          <DialogDescription>
            Upload a new version of this asset. The previous version will be preserved in history.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label htmlFor="version-file">File</Label>
            <Input
              id="version-file"
              type="file"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              disabled={isUploading}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="version-notes">Notes (optional)</Label>
            <Textarea
              id="version-notes"
              placeholder="Describe what changed in this version..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              disabled={isUploading}
              rows={3}
            />
          </div>

          {isUploading && <Progress value={progress} />}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} disabled={isUploading}>
            Cancel
          </Button>
          <Button onClick={handleUpload} disabled={!file || isUploading}>
            {isUploading ? (
              <>
                <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
                Uploading...
              </>
            ) : (
              'Upload'
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
