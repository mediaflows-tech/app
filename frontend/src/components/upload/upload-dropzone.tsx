'use client'

import { useCallback } from 'react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { Upload, FileWarning } from 'lucide-react'
import { cn } from '@/lib/utils'
import { toast } from '@/lib/toast'

const ACCEPTED_TYPES: Record<string, string[]> = {
  'image/*': ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp', '.tiff'],
  'video/*': ['.mp4', '.mov', '.avi', '.webm', '.mkv'],
  'audio/*': ['.mp3', '.wav', '.aac', '.ogg', '.flac'],
  'application/pdf': ['.pdf']
}

const MAX_FILE_SIZE = 500 * 1024 * 1024 // 500 MB

interface UploadDropzoneProps {
  onFilesAccepted: (files: File[]) => void
  disabled?: boolean
}

export function UploadDropzone({ onFilesAccepted, disabled = false }: UploadDropzoneProps) {
  const onDrop = useCallback(
    (acceptedFiles: File[], rejections: FileRejection[]) => {
      if (rejections.length > 0) {
        rejections.forEach((rejection) => {
          const errors = rejection.errors.map((e) => e.message).join(', ')
          toast.error(`${rejection.file.name}: ${errors}`)
        })
      }
      if (acceptedFiles.length > 0) {
        onFilesAccepted(acceptedFiles)
      }
    },
    [onFilesAccepted]
  )

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept: ACCEPTED_TYPES,
    maxSize: MAX_FILE_SIZE,
    disabled,
    multiple: true
  })

  return (
    <div
      {...getRootProps()}
      className={cn(
        'relative flex min-h-[300px] cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed transition-colors',
        isDragActive && !isDragReject && 'border-primary bg-primary/5',
        isDragReject && 'border-destructive bg-destructive/5',
        !isDragActive && 'border-muted-foreground/25 hover:border-muted-foreground/50',
        disabled && 'cursor-not-allowed opacity-50'
      )}
    >
      <input {...getInputProps()} />
      {isDragReject ? (
        <>
          <FileWarning className="mb-4 h-10 w-10 text-destructive" />
          <p className="text-sm font-medium text-destructive">Unsupported file type</p>
        </>
      ) : (
        <>
          <Upload className={cn('mb-4 h-10 w-10', isDragActive ? 'text-primary' : 'text-muted-foreground')} />
          <p className="text-sm font-medium">
            {isDragActive ? 'Drop files here' : 'Drag & drop files, or click to browse'}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">Images, videos, audio, and PDFs up to 500 MB</p>
        </>
      )}
    </div>
  )
}
