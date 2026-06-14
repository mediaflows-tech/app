'use client'

import { useState, useEffect, useCallback, useRef } from 'react'
import { Cpu, MemoryStick, Timer, AlertTriangle, Snowflake, DollarSign } from 'lucide-react'
import { MetricCard } from '@/components/admin/metric-card'
import { CpuMemoryChart } from '@/components/charts/cpu-memory-chart'
import { LatencyChart } from '@/components/charts/latency-chart'
import { ErrorCountChart } from '@/components/charts/error-count-chart'
import { RealtimeActivityChart } from '@/components/charts/realtime-activity-chart'
import { AlarmTable } from '@/components/admin/alarm-table'
import { useMonitoring, useAnalyticsStream } from '@/hooks/use-monitoring'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

const MAX_DATA_POINTS = 30

interface ChartPoint {
  time: string
  cpu: number
  memory: number
  p95: number
  errors: number
  uploadsPerMin: number
  reviewsPerMin: number
}

function ConnectionIndicator({ status }: { status: string }) {
  return (
    <div className="flex items-center gap-2 text-xs text-muted-foreground">
      <div
        className={cn(
          'h-2 w-2 rounded-full',
          status === 'connected' && 'bg-green-500',
          status === 'reconnecting' && 'bg-orange-500 animate-pulse',
          status === 'disconnected' && 'bg-red-500'
        )}
      />
      {status === 'connected' ? 'Live' : status === 'reconnecting' ? 'Reconnecting...' : 'Disconnected'}
    </div>
  )
}

export function MonitoringContent() {
  const { data: initialData, isLoading, error, refetch } = useMonitoring()
  const { snapshot, connectionStatus } = useAnalyticsStream()
  const [chartHistory, setChartHistory] = useState<ChartPoint[]>([])
  const lastSnapshotRef = useRef<string | null>(null)

  // Append new data points from SignalR
  const appendDataPoint = useCallback(() => {
    if (!snapshot) return

    const snapshotKey = JSON.stringify(snapshot)
    if (snapshotKey === lastSnapshotRef.current) return
    lastSnapshotRef.current = snapshotKey

    const now = new Date()
    const timeLabel = now.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    })

    const point: ChartPoint = {
      time: timeLabel,
      cpu: snapshot.cpuUtilization ?? 0,
      memory: snapshot.memoryUtilization ?? 0,
      p95: snapshot.requestLatencyP95 ?? 0,
      errors: snapshot.errorCount ?? 0,
      uploadsPerMin: snapshot.uploadsPerMinute ?? 0,
      reviewsPerMin: snapshot.reviewsPerMinute ?? 0
    }

    setChartHistory((prev) => {
      const updated = [...prev, point]
      return updated.slice(-MAX_DATA_POINTS)
    })
  }, [snapshot])

  useEffect(() => {
    appendDataPoint()
  }, [appendDataPoint])

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-[100px]" />
          ))}
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-6">
        <div className="flex flex-col items-center py-16 text-center">
          <p className="text-sm text-muted-foreground">Failed to load monitoring data.</p>
          <button onClick={() => refetch()} className="mt-3 rounded-md border px-3 py-1.5 text-sm hover:bg-muted">
            Retry
          </button>
        </div>
      </div>
    )
  }

  // Use live snapshot values if available, otherwise fall back to initial data
  const currentCpu = snapshot?.cpuUtilization ?? initialData?.cpuUtilization ?? 0
  const currentMemory = snapshot?.memoryUtilization ?? initialData?.memoryUtilization ?? 0
  const currentLatency = snapshot?.requestLatencyP95 ?? initialData?.requestLatencyP95 ?? 0
  const currentErrorRate = snapshot?.errorRate ?? initialData?.errorRate ?? 0
  const currentColdStarts = snapshot?.lambdaColdStarts ?? initialData?.lambdaColdStarts ?? 0
  const liveCost = snapshot?.estimatedDailyCost
  const initialCost = initialData?.estimatedDailyCost
  const currentCost = liveCost ?? initialCost
  const costLabel = typeof currentCost === 'number' ? `$${currentCost.toFixed(2)}` : 'N/A'

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div />
        <ConnectionIndicator status={connectionStatus} />
      </div>

      {/* Metric cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        <MetricCard title="CPU" value={`${currentCpu.toFixed(1)}%`} icon={<Cpu className="h-4 w-4" />} />
        <MetricCard title="Memory" value={`${currentMemory.toFixed(1)}%`} icon={<MemoryStick className="h-4 w-4" />} />
        <MetricCard title="Latency P95" value={`${currentLatency.toFixed(0)}ms`} icon={<Timer className="h-4 w-4" />} />
        <MetricCard
          title="Error Rate (Last 1h)"
          value={`${currentErrorRate.toFixed(2)}%`}
          icon={<AlertTriangle className="h-4 w-4" />}
        />
        <MetricCard
          title="Cold Starts (Last 1h)"
          value={currentColdStarts.toLocaleString()}
          icon={<Snowflake className="h-4 w-4" />}
        />
        <MetricCard title="Est. Cost (Last 24h)" value={costLabel} icon={<DollarSign className="h-4 w-4" />} />
      </div>

      {/* Live charts */}
      <div className="grid gap-4 lg:grid-cols-2">
        <CpuMemoryChart
          data={chartHistory.map((p) => ({
            time: p.time,
            cpu: p.cpu,
            memory: p.memory
          }))}
        />
        <LatencyChart
          data={chartHistory.map((p) => ({
            time: p.time,
            p95: p.p95
          }))}
        />
        <ErrorCountChart
          data={chartHistory.map((p) => ({
            time: p.time,
            errors: p.errors
          }))}
        />
        <RealtimeActivityChart
          data={chartHistory.map((p) => ({
            time: p.time,
            uploadsPerMin: p.uploadsPerMin,
            reviewsPerMin: p.reviewsPerMin
          }))}
        />
      </div>

      {/* Alarms */}
      <AlarmTable alarms={snapshot?.activeAlarms ?? initialData?.activeAlarms ?? []} />
    </div>
  )
}
