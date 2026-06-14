import { Suspense } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { MonitoringContent } from './monitoring-content'

export const metadata = {
  title: 'Monitoring | MediaFlows Admin'
}

export default function MonitoringPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Monitoring</h1>
        <p className="text-sm text-muted-foreground">Real-time system metrics and performance</p>
      </div>
      <Suspense fallback={<MonitoringSkeleton />}>
        <MonitoringContent />
      </Suspense>
    </div>
  )
}

function MonitoringSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-[100px]" />
        ))}
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-[260px]" />
        ))}
      </div>
      <Skeleton className="h-[200px]" />
    </div>
  )
}
