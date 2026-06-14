'use client'

import { useCompare } from '@/hooks/use-versions'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatBytes, formatDate } from '@/lib/utils'
import Image from 'next/image'

interface VersionCompareProps {
  assetId: number
  versionAId: number
  versionBId: number
}

export function VersionCompare({ assetId, versionAId, versionBId }: VersionCompareProps) {
  const { data, isLoading, error } = useCompare(assetId, versionAId, versionBId)

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 gap-4">
        <Skeleton className="aspect-video w-full rounded-lg" />
        <Skeleton className="aspect-video w-full rounded-lg" />
      </div>
    )
  }

  if (error) {
    return (
      <Card>
        <CardContent className="py-8 text-center">
          <p className="text-sm text-destructive">
            {error.message || 'Failed to load comparison. Please select two valid versions.'}
          </p>
        </CardContent>
      </Card>
    )
  }

  if (!data) return null

  const { versionA, versionB } = data

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-medium">Comparison</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {/* Version A */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <Badge variant="secondary" className="font-mono text-xs">
                v{versionA.versionNumber}
              </Badge>
              <span className="text-xs text-muted-foreground">{formatDate(versionA.createdAt, 'long')}</span>
            </div>
            <div className="relative aspect-video overflow-hidden rounded-md bg-muted">
              {versionA.mediaUrl && versionA.contentType?.startsWith('image/') ? (
                <Image
                  src={versionA.mediaUrl}
                  alt={`Version ${versionA.versionNumber}`}
                  fill
                  className="object-contain"
                  sizes="(max-width: 768px) 100vw, 50vw"
                />
              ) : versionA.mediaUrl && versionA.contentType?.startsWith('video/') ? (
                <video className="h-full w-full object-contain" controls muted preload="metadata">
                  <source src={versionA.mediaUrl} type={versionA.contentType} />
                </video>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  Preview not available for this file type
                </div>
              )}
            </div>
            <p className="text-xs text-muted-foreground">{formatBytes(versionA.fileSize)}</p>
          </div>

          {/* Version B */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <Badge variant="secondary" className="font-mono text-xs">
                v{versionB.versionNumber}
              </Badge>
              <span className="text-xs text-muted-foreground">{formatDate(versionB.createdAt, 'long')}</span>
            </div>
            <div className="relative aspect-video overflow-hidden rounded-md bg-muted">
              {versionB.mediaUrl && versionB.contentType?.startsWith('image/') ? (
                <Image
                  src={versionB.mediaUrl}
                  alt={`Version ${versionB.versionNumber}`}
                  fill
                  className="object-contain"
                  sizes="(max-width: 768px) 100vw, 50vw"
                />
              ) : versionB.mediaUrl && versionB.contentType?.startsWith('video/') ? (
                <video className="h-full w-full object-contain" controls muted preload="metadata">
                  <source src={versionB.mediaUrl} type={versionB.contentType} />
                </video>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  Preview not available for this file type
                </div>
              )}
            </div>
            <p className="text-xs text-muted-foreground">{formatBytes(versionB.fileSize)}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
