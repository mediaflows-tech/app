import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

export function formatDate(date: string | Date, style: 'short' | 'long' = 'short'): string {
  const d = new Date(date)
  if (style === 'short') {
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
  }
  return d.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export function formatStatus(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export function getFileExtLabel(contentType: string): string {
  const sub = contentType.split('/').pop() ?? ''
  const map: Record<string, string> = {
    jpeg: 'JPG',
    quicktime: 'MOV',
    mpeg: 'MPG',
    'x-wav': 'WAV',
    'svg+xml': 'SVG',
    'x-matroska': 'MKV'
  }
  return map[sub] ?? sub.toUpperCase()
}

export function formatRelativeTime(date: string | Date): string {
  const now = new Date()
  const d = new Date(date)
  const diffMs = now.getTime() - d.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  if (diffMins < 1) return 'just now'
  if (diffMins < 60) return `${diffMins}m ago`
  const diffHours = Math.floor(diffMins / 60)
  if (diffHours < 24) return `${diffHours}h ago`
  const diffDays = Math.floor(diffHours / 24)
  if (diffDays < 7) return `${diffDays}d ago`
  return formatDate(d, 'short')
}

/** ISO-8601 string (minute precision) representing "now" in the browser's local time,
 *  suitable for use as the `min` attribute on `<input type="datetime-local">`. */
export function getMinDateTime(): string {
  const now = new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
}
