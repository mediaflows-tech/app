'use client'

import { useSession } from 'next-auth/react'
import { useSummary } from '@/hooks/use-admin-summary'
import { Package, Users, Clock, HardDrive } from 'lucide-react'

const STATS_CONFIG = [
  { key: 'totalAssets', label: 'Total Assets', icon: Package },
  { key: 'totalUsers', label: 'Total Users', icon: Users },
  { key: 'pendingReviews', label: 'Pending Reviews', icon: Clock },
  { key: 'storageUsedFormatted', label: 'Storage Used', icon: HardDrive }
] as const

export function DashboardStats() {
  const { data: session } = useSession()
  const isAdmin = session?.user.role === 'SystemAdmin'
  const { data, isLoading } = useSummary()

  if (!isAdmin) return null

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {STATS_CONFIG.map((stat) => {
        const Icon = stat.icon
        const value = data?.[stat.key] ?? '—'

        return (
          <div
            key={stat.key}
            className="rounded-lg border border-border/50 bg-card/50 p-3 transition-colors hover:bg-card"
          >
            <div className="flex items-center gap-2">
              <Icon className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="text-xs text-muted-foreground">{stat.label}</span>
            </div>
            <p className="mt-1 text-xl font-semibold tabular-nums">{isLoading ? '...' : value}</p>
          </div>
        )
      })}
    </div>
  )
}
