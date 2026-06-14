'use client'

import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useChartTheme } from './use-chart-theme'

interface DataPoint {
  time: string
  cpu: number
  memory: number
}

interface CpuMemoryChartProps {
  data: DataPoint[]
}

export function CpuMemoryChart({ data }: CpuMemoryChartProps) {
  const t = useChartTheme()

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">CPU & Memory</CardTitle>
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
              domain={[0, 100]}
              tick={{ fontSize: 10, fontFamily: 'var(--font-geist-mono)', fill: t.axis }}
              stroke={t.axis}
              tickLine={false}
              axisLine={false}
              tickFormatter={(v) => `${v}%`}
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
              formatter={(value) => `${Number(value).toFixed(1)}%`}
            />
            <Legend iconType="line" iconSize={12} wrapperStyle={{ fontSize: 11, color: t.legend }} />
            <Line type="monotone" dataKey="cpu" name="CPU" stroke={t.primary} strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="memory" name="Memory" stroke={t.secondary} strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  )
}
