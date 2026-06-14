'use client'

import Link from 'next/link'
import Image from 'next/image'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { StatusBadge } from '@/components/ui/status-badge'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Badge } from '@/components/ui/badge'
import { cn, formatBytes, formatRelativeTime } from '@/lib/utils'
import { MoreHorizontal, Eye, GitBranch, Send, Trash2, FileImage, FileVideo, FileAudio, FileText } from 'lucide-react'
import type { MediaAssetSummaryDto, AssetStatus } from '@/types/api'

function getFileIcon(contentType: string) {
  if (contentType.startsWith('image/')) return FileImage
  if (contentType.startsWith('video/')) return FileVideo
  if (contentType.startsWith('audio/')) return FileAudio
  return FileText
}

function getContentTypeLabel(contentType: string): string {
  if (contentType.startsWith('video/')) return 'Video'
  if (contentType.startsWith('audio/')) return 'Audio'
  if (contentType.startsWith('image/')) return 'Image'
  return 'Document'
}

interface AssetCardProps {
  asset: MediaAssetSummaryDto
  onSubmit?: (id: number) => void
  onDelete?: (id: number) => void
  selected?: boolean
  onSelect?: (id: number, selected: boolean) => void
  anySelected?: boolean
}

export function AssetCard({ asset, onSubmit, onDelete, selected, onSelect, anySelected }: AssetCardProps) {
  const FileTypeIcon = getFileIcon(asset.contentType)

  return (
    <div className="group relative overflow-hidden rounded-lg border bg-card transition-colors hover:bg-accent/50">
      {/* Thumbnail */}
      <Link href={`/creator/assets/${asset.id}`}>
        <div className="relative aspect-video w-full bg-muted">
          {onSelect && (
            <div
              className={cn(
                'absolute left-2 top-2 z-10 transition-opacity',
                selected || anySelected ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
              )}
              onClick={(e) => e.stopPropagation()}
            >
              <Checkbox checked={!!selected} onCheckedChange={(checked) => onSelect(asset.id, !!checked)} />
            </div>
          )}
          {asset.thumbnailUrl ? (
            <Image
              src={asset.thumbnailUrl}
              alt={asset.title}
              fill
              className="object-cover"
              sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
            />
          ) : (
            <div className="flex h-full items-center justify-center">
              <FileTypeIcon className="h-8 w-8 text-muted-foreground" />
            </div>
          )}
          {selected && <div className="absolute inset-0 z-[5] bg-primary/30 transition-opacity" />}
          <Badge
            variant="secondary"
            className="absolute right-2 top-2 text-[10px] font-medium uppercase tracking-wider"
          >
            {getContentTypeLabel(asset.contentType)}
          </Badge>
        </div>
      </Link>

      {/* Info */}
      <div className="p-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            <Link href={`/creator/assets/${asset.id}`}>
              <h3 className="truncate text-sm font-medium hover:underline">{asset.title}</h3>
            </Link>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {formatBytes(asset.fileSize)} &middot; {formatRelativeTime(asset.createdAt)}
            </p>
          </div>

          <DropdownMenu>
            <DropdownMenuTrigger
              render={<Button variant="ghost" size="icon" className="h-7 w-7 opacity-0 group-hover:opacity-100" />}
            >
              <MoreHorizontal className="h-4 w-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem render={<Link href={`/creator/assets/${asset.id}`} />}>
                <Eye className="mr-2 h-4 w-4" />
                View Details
              </DropdownMenuItem>
              <DropdownMenuItem render={<Link href={`/creator/assets/${asset.id}/versions`} />}>
                <GitBranch className="mr-2 h-4 w-4" />
                Versions
              </DropdownMenuItem>
              {asset.status === 'Draft' && onSubmit && (
                <DropdownMenuItem onClick={() => onSubmit(asset.id)}>
                  <Send className="mr-2 h-4 w-4" />
                  Submit for Review
                </DropdownMenuItem>
              )}
              {(asset.status === 'Draft' || asset.status === 'Rejected') && onDelete && (
                <DropdownMenuItem onClick={() => onDelete(asset.id)} variant="destructive">
                  <Trash2 className="mr-2 h-4 w-4" />
                  Delete
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <div className="mt-2">
          <StatusBadge status={asset.status} />
        </div>
      </div>
    </div>
  )
}
