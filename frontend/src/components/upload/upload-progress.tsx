'use client'

import { Progress } from '@/components/ui/progress'
import { Badge } from '@/components/ui/badge'
import { formatBytes } from '@/lib/utils'
import { CheckCircle2, AlertCircle, Loader2, FileIcon } from 'lucide-react'
import type { UploadFileState } from '@/hooks/use-upload'

const statusConfig = {
  pending: { label: 'Waiting', Icon: FileIcon, color: 'secondary' as const },
  presigning: { label: 'Preparing', Icon: Loader2, color: 'secondary' as const },
  uploading: { label: 'Uploading', Icon: Loader2, color: 'secondary' as const },
  confirming: { label: 'Scanning', Icon: Loader2, color: 'secondary' as const },
  done: { label: 'Complete', Icon: CheckCircle2, color: 'default' as const },
  error: { label: 'Failed', Icon: AlertCircle, color: 'destructive' as const }
}

interface UploadProgressProps {
  fileState: UploadFileState
}

export function UploadProgressItem({ fileState }: UploadProgressProps) {
  const config = statusConfig[fileState.status]
  const { Icon } = config
  const isAnimating =
    fileState.status === 'presigning' || fileState.status === 'uploading' || fileState.status === 'confirming'

  return (
    <div className="flex items-center gap-3 rounded-md border px-4 py-3">
      <Icon
        className={`h-4 w-4 shrink-0 ${
          isAnimating ? 'animate-spin text-muted-foreground' : ''
        } ${fileState.status === 'done' ? 'text-emerald-500' : ''} ${
          fileState.status === 'error' ? 'text-destructive' : ''
        }`}
      />
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <p className="truncate text-sm font-medium">{fileState.file.name}</p>
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">{formatBytes(fileState.file.size)}</span>
            <Badge variant={config.color} className="text-xs">
              {config.label}
            </Badge>
          </div>
        </div>
        {(fileState.status === 'uploading' || fileState.status === 'confirming') && (
          <Progress value={fileState.progress} className="mt-2" />
        )}
        {fileState.error && <p className="mt-1 text-xs text-destructive">{fileState.error}</p>}
      </div>
    </div>
  )
}

interface UploadProgressListProps {
  files: UploadFileState[]
}

export function UploadProgressList({ files }: UploadProgressListProps) {
  if (files.length === 0) return null

  return (
    <div className="space-y-2">
      {files.map((fileState, index) => (
        <UploadProgressItem key={`${fileState.file.name}-${index}`} fileState={fileState} />
      ))}
    </div>
  )
}
