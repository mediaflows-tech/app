'use client'

import { useTheme } from 'next-themes'

export function useChartTheme() {
  const { resolvedTheme } = useTheme()
  const dark = resolvedTheme === 'dark'

  // --glass-opacity and --glass-blur are defined in globals.css
  const glassOpacity = 0.6
  const glassBlur = 'blur(var(--glass-blur, 16px))'

  return {
    primary: dark ? '#f5f5f7' : '#1d1d1f',
    secondary: dark ? '#86868b' : '#86868b',
    grid: dark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)',
    axis: dark ? '#a1a1a6' : '#86868b',
    tooltip: {
      backgroundColor: dark ? `rgba(28,28,30,${glassOpacity})` : `rgba(255,255,255,${glassOpacity})`,
      border: dark ? '1px solid rgba(255,255,255,0.1)' : '1px solid rgba(0,0,0,0.08)',
      color: dark ? '#f5f5f7' : '#1d1d1f',
      backdropFilter: glassBlur,
      WebkitBackdropFilter: glassBlur
    },
    legend: dark ? '#d2d2d7' : '#48484a'
  }
}
