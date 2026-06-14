'use client'

import Link from 'next/link'
import type { ReviewListItemDto } from '@/types/api'
import { REVIEW_ACTIONABLE_STATUSES } from '@/lib/review-ui'
import { TableCell, TableRow } from '@/components/ui/table'
import { Checkbox } from '@/components/ui/checkbox'
import { Badge } from '@/components/ui/badge'
import { StatusBadge } from '@/components/ui/status-badge'
import { Button } from '@/components/ui/button'
import { ArrowRight, FileText } from 'lucide-react'
import { formatBytes, formatDate } from '@/lib/utils'

interface ReviewRowProps {
  item: ReviewListItemDto
  isSelected: boolean
  onToggleSelect: (id: number, checked: boolean) => void
}

export function ReviewRow({ item, isSelected, onToggleSelect }: ReviewRowProps) {
  const isSelectable = REVIEW_ACTIONABLE_STATUSES.includes(item.status)

  return (
    <TableRow data-date={item.createdAt.slice(0, 10)}>
      <TableCell>
        <Checkbox
          checked={isSelected}
          disabled={!isSelectable}
          onCheckedChange={(checked: boolean) => onToggleSelect(item.assetId, checked)}
        />
      </TableCell>
      <TableCell>
        <div className="flex items-center gap-3">
          <div className="h-12 w-12 shrink-0 overflow-hidden rounded-md bg-muted">
            {item.thumbnailUrl ? (
              /* eslint-disable-next-line @next/next/no-img-element */
              <img src={item.thumbnailUrl} alt="" className="h-full w-full object-cover" />
            ) : (
              <div className="flex h-full w-full items-center justify-center">
                <FileText className="h-4 w-4 text-muted-foreground" />
              </div>
            )}
          </div>
          <div>
            <p className="text-sm font-medium">{item.title}</p>
            <p className="text-xs text-muted-foreground">{formatBytes(item.fileSize)}</p>
          </div>
        </div>
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">{item.creatorName}</TableCell>
      <TableCell>
        <Badge variant="outline">{item.contentType}</Badge>
      </TableCell>
      <TableCell>
        <StatusBadge status={item.status} />
      </TableCell>
      <TableCell className="text-xs text-muted-foreground">{formatDate(item.createdAt)}</TableCell>
      <TableCell>
        <Button variant="ghost" size="icon" render={<Link href={`/review/${item.assetId}`} />}>
          <ArrowRight className="h-4 w-4" />
        </Button>
      </TableCell>
    </TableRow>
  )
}
