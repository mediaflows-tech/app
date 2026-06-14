'use client'

import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useChartTheme } from './use-chart-theme'

interface DataPoint {
  time: string
  errors: number
}

interface ErrorCountChartProps {
  data: DataPoint[]
}

export function ErrorCountChart({ data }: ErrorCountChartProps) {
  const t = useChartTheme()

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Error Count</CardTitle>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={data}>
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
            <Bar dataKey="errors" fill={t.primary} radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  )
}
