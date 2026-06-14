'use client'

import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useChartTheme } from './use-chart-theme'

interface DataPoint {
  time: string
  p95: number
}

interface LatencyChartProps {
  data: DataPoint[]
}

export function LatencyChart({ data }: LatencyChartProps) {
  const t = useChartTheme()

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Latency (P95)</CardTitle>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke={t.grid} vertical={false} />
            <XAxis
              dataKey="time"
              tick={{ fontSize: 10, fontFamily: 'var(--font-geist-mono)', fill: t.axis }}
              stroke={t.axis}
              tickLine={false}
              axisLine={false}
            />
            <YAxis
              tick={{ fontSize: 10, fontFamily: 'var(--font-geist-mono)', fill: t.axis }}
              stroke={t.axis}
              tickLine={false}
              axisLine={false}
              tickFormatter={(v) => `${v}ms`}
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
              formatter={(value) => `${Number(value).toFixed(0)}ms`}
            />
            <Line
              type="monotone"
              dataKey="p95"
              stroke={t.primary}
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4, fill: t.primary }}
            />
          </LineChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  )
}
