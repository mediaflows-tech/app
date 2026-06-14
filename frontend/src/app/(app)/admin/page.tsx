import { Suspense } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { AdminDashboardContent } from './dashboard-content'

export const metadata = {
  title: 'Admin Dashboard | MediaFlows'
}

export default function AdminDashboardPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Summary</h1>
        <p className="text-sm text-muted-foreground">System overview and key metrics</p>
      </div>
      <Suspense fallback={<DashboardSkeleton />}>
        <AdminDashboardContent />
      </Suspense>
    </div>
  )
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-[120px]" />
        ))}
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="h-[300px]" />
        <Skeleton className="h-[300px]" />
      </div>
      <Skeleton className="h-[200px]" />
    </div>
  )
}
