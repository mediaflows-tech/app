'use client'

import { Users, ImageIcon, HardDrive, Clock } from 'lucide-react'
import { MetricCard } from './metric-card'
import { formatBytes } from '@/lib/utils'
import type { AdminSummaryDto } from '@/types/api'

interface MetricCardsProps {
  summary: AdminSummaryDto
}

export function MetricCards({ summary }: MetricCardsProps) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <MetricCard
        title="Total Users"
        value={summary.totalUsers.toLocaleString()}
        icon={<Users className="h-4 w-4" />}
        description="Active platform users"
      />
      <MetricCard
        title="Total Assets"
        value={summary.totalAssets.toLocaleString()}
        icon={<ImageIcon className="h-4 w-4" />}
        description="Uploaded media files"
      />
      <MetricCard
        title="Storage Used"
        value={summary.storageUsedFormatted ?? formatBytes(summary.storageUsedBytes)}
        icon={<HardDrive className="h-4 w-4" />}
        description="Total media storage"
      />
      <MetricCard
        title="Pending Reviews"
        value={summary.pendingReviews.toLocaleString()}
        icon={<Clock className="h-4 w-4" />}
        description="Awaiting approval"
      />
    </div>
  )
}
