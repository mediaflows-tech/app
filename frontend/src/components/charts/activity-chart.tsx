'use client'

import { useState } from 'react'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useActivityChart } from '@/hooks/use-admin-summary'
import { Skeleton } from '@/components/ui/skeleton'
import { useChartTheme } from './use-chart-theme'

const RANGES = [
  { label: '7D', days: 7 },
  { label: '30D', days: 30 },
  { label: '90D', days: 90 }
] as const

interface ActivityChartProps {
  fallbackLabels?: string[]
  fallbackData?: number[]
}

export function ActivityChart({ fallbackLabels, fallbackData }: ActivityChartProps = {}) {
  const [days, setDays] = useState<number>(7)
  const { data: remoteData, isLoading } = useActivityChart(days)
  const t = useChartTheme()

  const data =
    remoteData && remoteData.length > 0
      ? remoteData
      : (fallbackLabels ?? []).map((label, i) => ({
          date: label,
          uploads: fallbackData?.[i] ?? 0,
          reviews: 0
        }))

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium">Upload Activity</CardTitle>
        <Tabs value={String(days)} onValueChange={(v) => setDays(Number(v))}>
          <TabsList className="h-7">
            {RANGES.map((r) => (
              <TabsTrigger key={r.days} value={String(r.days)} className="px-2 text-xs">
                {r.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-[250px] w-full" />
        ) : (
          <ResponsiveContainer width="100%" height={250}>
            <LineChart data={data ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke={t.grid} vertical={false} />
              <XAxis
                dataKey="date"
                tick={{ fontSize: 11, fontFamily: 'var(--font-geist-mono)', fill: t.axis }}
                stroke={t.axis}
                tickLine={false}
                axisLine={false}
              />
              <YAxis
                tick={{ fontSize: 11, fontFamily: 'var(--font-geist-mono)', fill: t.axis }}
                stroke={t.axis}
                tickLine={false}
                axisLine={false}
              />
              <Tooltip
                wrapperStyle={{ outline: 'none' }}
                contentStyle={{
                  fontSize: 12,
                  fontFamily: 'var(--font-geist-mono)',
                  borderRadius: 8,
                  backgroundColor: t.tooltip.backgroundColor,
                  border: t.tooltip.border,
                  backdropFilter: t.tooltip.backdropFilter,
                  WebkitBackdropFilter: t.tooltip.WebkitBackdropFilter,
                  color: t.tooltip.color
                }}
              />
              <Line
                type="monotone"
                dataKey="uploads"
                stroke={t.primary}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 4, fill: t.primary }}
              />
              <Line
                type="monotone"
                dataKey="reviews"
                stroke={t.secondary}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 4, fill: t.secondary }}
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  )
}
