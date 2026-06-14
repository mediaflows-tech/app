'use client'

import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip, Legend } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useChartTheme } from './use-chart-theme'

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return `${(bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 2)} ${units[i]}`
}

// Pastel palette — distinct and readable in both light and dark
const SLICE_COLORS = ['#93c5fd', '#6ee7b7', '#fcd34d', '#c4b5fd']

interface StorageData {
  name: string
  value: number
}

interface StorageChartProps {
  data: StorageData[]
}

export function StorageChart({ data }: StorageChartProps) {
  const t = useChartTheme()
  const safeData = data ?? []

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Storage by Type</CardTitle>
      </CardHeader>
      <CardContent>
        {safeData.length === 0 ? (
          <div className="flex h-[250px] items-center justify-center">
            <p className="text-sm text-muted-foreground">No storage data available</p>
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={250}>
            <PieChart>
              <Pie
                data={safeData}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={90}
                paddingAngle={0}
                dataKey="value"
                stroke="none"
              >
                {safeData.map((_, index) => (
                  <Cell key={`cell-${index}`} fill={SLICE_COLORS[index % SLICE_COLORS.length]} stroke="none" />
                ))}
              </Pie>
              <Tooltip
                wrapperStyle={{ outline: 'none' }}
                contentStyle={{
                  fontSize: 12,
                  fontFamily: 'var(--font-geist-mono)',
                  borderRadius: 8,
                  backgroundColor: t.tooltip.backgroundColor,
                  border: t.tooltip.border,
                  color: t.tooltip.color,
                  backdropFilter: t.tooltip.backdropFilter,
                  WebkitBackdropFilter: t.tooltip.WebkitBackdropFilter
                }}
                formatter={(value) => formatBytes(Number(value))}
              />
              <Legend
                iconType="circle"
                iconSize={8}
                wrapperStyle={{
                  fontSize: 12,
                  fontFamily: 'var(--font-geist-mono)',
                  color: t.legend
                }}
              />
            </PieChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  )
}
