'use client'

import { MetricCards } from '@/components/admin/metric-cards'
import { ActivityChart } from '@/components/charts/activity-chart'
import { StorageChart } from '@/components/charts/storage-chart'
import { AlarmTable } from '@/components/admin/alarm-table'
import { useSummary } from '@/hooks/use-admin-summary'
import { useAnalyticsStream } from '@/hooks/use-monitoring'
import { Skeleton } from '@/components/ui/skeleton'

export function AdminDashboardContent() {
  const { data: summary, isLoading, error, refetch } = useSummary()
  const { snapshot } = useAnalyticsStream()

  // Merge live snapshot data into summary when available
  const liveSummary = summary
    ? {
        ...summary,
        totalUsers: snapshot?.totalUsers ?? summary.totalUsers ?? 0,
        totalAssets: snapshot?.totalAssets ?? summary.totalAssets ?? 0,
        pendingReviews: snapshot?.pendingReviews ?? summary.pendingReviews ?? 0,
        storageByType: summary.storageByType ?? [],
        alarms: summary.alarms ?? []
      }
    : null

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-[120px]" />
          ))}
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-6">
        <div className="flex flex-col items-center py-16 text-center">
          <p className="text-sm text-muted-foreground">Failed to load dashboard data.</p>
          <button onClick={() => refetch()} className="mt-3 rounded-md border px-3 py-1.5 text-sm hover:bg-muted">
            Retry
          </button>
        </div>
      </div>
    )
  }

  if (!liveSummary) {
    return (
      <div className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-[120px]" />
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <MetricCards summary={liveSummary} />

      <div className="grid gap-4 lg:grid-cols-2">
        <ActivityChart fallbackLabels={liveSummary.activityLabels} fallbackData={liveSummary.activityData} />
        <StorageChart data={liveSummary.storageByType} />
      </div>

      <AlarmTable alarms={liveSummary.alarms} />
    </div>
  )
}
