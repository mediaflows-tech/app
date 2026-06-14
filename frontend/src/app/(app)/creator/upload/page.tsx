'use client'

import { UploadDropzone } from '@/components/upload/upload-dropzone'
import { UploadProgressList } from '@/components/upload/upload-progress'
import { useUploadFiles } from '@/hooks/use-upload'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CheckCircle2, ArrowRight, RotateCcw } from 'lucide-react'
import Link from 'next/link'

export default function UploadPage() {
  const { files, startUpload, clearCompleted, reset, isUploading, completedCount, errorCount } = useUploadFiles()

  const hasFiles = files.length > 0
  const allDone = hasFiles && !isUploading && errorCount === 0

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Upload</h1>
        <p className="text-sm text-muted-foreground">Upload media files to your asset library</p>
      </div>

      <Card>
        <CardContent>
          <UploadDropzone onFilesAccepted={startUpload} disabled={isUploading} />
        </CardContent>
      </Card>

      {hasFiles && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
            <CardTitle className="text-base font-medium">
              {isUploading ? 'Uploading...' : `${completedCount} of ${files.length} complete`}
            </CardTitle>
            {!isUploading && (
              <div className="flex gap-2">
                {completedCount > 0 && (
                  <Button variant="ghost" size="sm" onClick={clearCompleted}>
                    Clear completed
                  </Button>
                )}
                <Button variant="ghost" size="sm" onClick={reset}>
                  <RotateCcw className="mr-1 h-3 w-3" />
                  Reset
                </Button>
              </div>
            )}
          </CardHeader>
          <CardContent>
            <UploadProgressList files={files} />
          </CardContent>
        </Card>
      )}

      {allDone && (
        <Card className="border-emerald-500/30 bg-emerald-500/5">
          <CardContent className="flex items-center justify-between py-4">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="h-5 w-5 text-emerald-500" />
              <div>
                <p className="text-sm font-medium">
                  {completedCount} {completedCount === 1 ? 'file' : 'files'} uploaded successfully
                </p>
                <p className="text-xs text-muted-foreground">Your assets are ready in the library</p>
              </div>
            </div>
            <Button render={<Link href="/creator/assets" />} size="sm">
              View Library
              <ArrowRight className="ml-1 h-3 w-3" />
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
