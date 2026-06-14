'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { notify } from '@/components/ui/toast-config'
import { assetKeys } from '@/hooks/use-assets'
import type { UploadPresignedUrlResponse } from '@/types/api'
import { useCallback, useState } from 'react'

export interface UploadFileState {
  file: File
  progress: number
  status: 'pending' | 'presigning' | 'uploading' | 'confirming' | 'done' | 'error'
  error?: string
  assetId?: number
}

export function usePresign() {
  return useMutation({
    mutationFn: ({ fileName, contentType }: { fileName: string; contentType: string }) =>
      api.get<UploadPresignedUrlResponse>(
        `/upload/presign?fileName=${encodeURIComponent(fileName)}&contentType=${encodeURIComponent(contentType)}`
      )
  })
}

export function useConfirmUpload() {
  return useMutation({
    mutationFn: ({
      s3Key,
      fileName,
      contentType,
      fileSize
    }: {
      s3Key: string
      fileName: string
      contentType: string
      fileSize: number
    }) =>
      api.post<{ id: number; title: string; status: string }>('/upload/confirm', {
        s3Key,
        fileName,
        contentType,
        fileSize
      })
  })
}

/**
 * Uploads a file to S3 using XMLHttpRequest for progress tracking.
 * Returns a promise that resolves when the upload completes.
 *
 * CORS note: S3 presigned URLs already encode the content-type in the
 * signature. We only set Content-Type (which may trigger a preflight)
 * and avoid any other custom headers (Authorization, x-amz-*, etc.)
 * so the browser sends a simple PUT or a preflight that S3 can handle.
 * If the presigned URL was generated without a content-type constraint,
 * we skip the header entirely to avoid a signature mismatch.
 */
export function uploadToS3(url: string, file: File, onProgress: (percent: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()

    xhr.upload.addEventListener('progress', (event) => {
      if (event.lengthComputable) {
        const percent = Math.round((event.loaded / event.total) * 100)
        onProgress(percent)
      }
    })

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve()
      } else {
        reject(new Error(`S3 upload failed with status ${xhr.status}: ${xhr.statusText}`))
      }
    })

    xhr.addEventListener('error', () => {
      reject(new Error('S3 upload network error — check CORS configuration'))
    })

    xhr.addEventListener('abort', () => {
      reject(new Error('S3 upload aborted'))
    })

    xhr.open('PUT', url)

    // Only set Content-Type if the presigned URL contains a Content-Type
    // query parameter — this means the server signed for that type.
    // Setting it when the URL wasn't signed for it causes a 403.
    // Setting an unexpected Content-Type can also trigger a CORS preflight
    // that S3 may reject if the bucket CORS config doesn't allow it.
    try {
      const parsed = new URL(url)
      const signedContentType = parsed.searchParams.get('Content-Type')
      if (signedContentType) {
        xhr.setRequestHeader('Content-Type', signedContentType)
      } else if (file.type) {
        // Fallback: use the file's MIME type (presigned URL likely expects it)
        xhr.setRequestHeader('Content-Type', file.type)
      }
      // If neither is available, send without Content-Type header —
      // the browser will default to the appropriate type for the body.
    } catch {
      // If URL parsing fails, set the file type and hope for the best
      if (file.type) {
        xhr.setRequestHeader('Content-Type', file.type)
      }
    }

    xhr.send(file)
  })
}

/**
 * Full upload orchestrator hook.
 * Manages multiple file uploads: presign -> S3 PUT -> confirm.
 */
export function useUploadFiles() {
  const queryClient = useQueryClient()
  const [files, setFiles] = useState<UploadFileState[]>([])
  const presign = usePresign()
  const confirm = useConfirmUpload()

  const updateFile = useCallback((index: number, update: Partial<UploadFileState>) => {
    setFiles((prev) => prev.map((f, i) => (i === index ? { ...f, ...update } : f)))
  }, [])

  const uploadFile = useCallback(
    async (file: File, index: number) => {
      try {
        updateFile(index, { status: 'presigning' })
        const presigned = await presign.mutateAsync({
          fileName: file.name,
          contentType: file.type
        })

        updateFile(index, { status: 'uploading' })
        await uploadToS3(presigned.uploadUrl, file, (progress) => {
          updateFile(index, { progress })
        })

        // Confirm upload with API. The API waits for content moderation
        // before returning, so success is only shown after the asset is accepted.
        updateFile(index, { status: 'confirming', progress: 100 })
        const result = await confirm.mutateAsync({
          s3Key: presigned.s3Key,
          fileName: file.name,
          contentType: file.type,
          fileSize: file.size
        })

        updateFile(index, {
          status: 'done',
          progress: 100,
          assetId: result.id
        })
        notify.success(`${file.name} uploaded successfully`)
        queryClient.invalidateQueries({ queryKey: assetKeys.lists() })
      } catch (err) {
        const msg = err instanceof Error ? err.message : 'Upload failed'
        updateFile(index, {
          status: 'error',
          error: msg
        })
        notify.error(`${file.name}: ${msg}`)
      }
    },
    [presign, confirm, updateFile]
  )

  const startUpload = useCallback(
    (newFiles: File[]) => {
      const startIndex = files.length
      const newStates: UploadFileState[] = newFiles.map((file) => ({
        file,
        progress: 0,
        status: 'pending' as const
      }))
      setFiles((prev) => [...prev, ...newStates])

      // Upload files sequentially to avoid overwhelming the browser
      newFiles.reduce(async (prevPromise, file, i) => {
        await prevPromise
        return uploadFile(file, startIndex + i)
      }, Promise.resolve())
    },
    [files.length, uploadFile]
  )

  const clearCompleted = useCallback(() => {
    setFiles((prev) => prev.filter((f) => f.status !== 'done'))
  }, [])

  const reset = useCallback(() => {
    setFiles([])
  }, [])

  return {
    files,
    startUpload,
    clearCompleted,
    reset,
    isUploading: files.some((f) => f.status === 'presigning' || f.status === 'uploading' || f.status === 'confirming'),
    completedCount: files.filter((f) => f.status === 'done').length,
    errorCount: files.filter((f) => f.status === 'error').length
  }
}
