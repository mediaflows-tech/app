'use client'

import Link from 'next/link'
import Image from 'next/image'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { StatusBadge } from '@/components/ui/status-badge'
import { formatBytes, formatRelativeTime } from '@/lib/utils'
import { Eye, GitBranch, Send, Trash2, FileImage, FileVideo, FileAudio, FileText } from 'lucide-react'
import type { MediaAssetSummaryDto, AssetStatus } from '@/types/api'

function getFileIcon(contentType: string) {
  if (contentType.startsWith('image/')) return FileImage
  if (contentType.startsWith('video/')) return FileVideo
  if (contentType.startsWith('audio/')) return FileAudio
  return FileText
}

interface AssetListProps {
  assets: MediaAssetSummaryDto[]
  onSubmit?: (id: number) => void
  onDelete?: (id: number) => void
  selectedIds?: Set<number>
  onToggleSelect?: (id: number, checked: boolean) => void
}

export function AssetList({ assets, onSubmit, onDelete, selectedIds, onToggleSelect }: AssetListProps) {
  return (
    <div className="divide-y rounded-lg border">
      {assets.map((asset) => {
        const Icon = getFileIcon(asset.contentType)
        return (
          <div key={asset.id} className="flex items-center gap-4 px-4 py-3 transition-colors hover:bg-accent/50">
            {onToggleSelect && (
              <Checkbox
                checked={selectedIds?.has(asset.id) ?? false}
                onCheckedChange={(checked) => onToggleSelect(asset.id, !!checked)}
                onClick={(e: React.MouseEvent) => e.stopPropagation()}
                className="shrink-0"
              />
            )}
            {/* Thumbnail */}
            <Link href={`/creator/assets/${asset.id}`} className="shrink-0">
              <div className="relative h-12 w-12 overflow-hidden rounded-md bg-muted">
                {asset.thumbnailUrl ? (
                  <Image src={asset.thumbnailUrl} alt={asset.title} fill className="object-cover" sizes="48px" />
                ) : (
                  <div className="flex h-full items-center justify-center">
                    <Icon className="h-5 w-5 text-muted-foreground" />
                  </div>
                )}
              </div>
            </Link>

            {/* Info */}
            <div className="min-w-0 flex-1">
              <Link href={`/creator/assets/${asset.id}`}>
                <p className="truncate text-sm font-medium hover:underline">{asset.title}</p>
              </Link>
              <p className="text-xs text-muted-foreground">
                {formatBytes(asset.fileSize)} &middot; {formatRelativeTime(asset.createdAt)}
              </p>
            </div>

            {/* Status */}
            <StatusBadge status={asset.status} className="shrink-0" />

            {/* Actions */}
            <div className="flex shrink-0 items-center gap-1">
              <Link href={`/creator/assets/${asset.id}`}>
                <Button variant="ghost" size="icon" className="h-7 w-7">
                  <Eye className="h-3.5 w-3.5" />
                </Button>
              </Link>
              <Link href={`/creator/assets/${asset.id}/versions`}>
                <Button variant="ghost" size="icon" className="h-7 w-7">
                  <GitBranch className="h-3.5 w-3.5" />
                </Button>
              </Link>
              {asset.status === 'Draft' && onSubmit && (
                <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => onSubmit(asset.id)}>
                  <Send className="h-3.5 w-3.5" />
                </Button>
              )}
              {(asset.status === 'Draft' || asset.status === 'Rejected') && onDelete && (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 text-destructive"
                  onClick={() => onDelete(asset.id)}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}
