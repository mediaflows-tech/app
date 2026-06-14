'use client'

import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react'
import { useSession } from 'next-auth/react'
import { createPortal } from 'react-dom'
import { Bell, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { notificationStore, type AppNotification } from '@/lib/notification-store'
import { formatAlarmName } from '@/lib/format-alarm-name'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'

type AlarmDto = { alarmName: string; stateValue: string; metricName: string; updated: string }

// Stable empty reference for the server snapshot (and pre-hydration) so
// useSyncExternalStore doesn't see a new array identity on every render.
const EMPTY_NOTIFICATIONS: AppNotification[] = []

function formatRelative(date: Date): string {
  const now = Date.now()
  const diff = now - new Date(date).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'Just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  return `${days}d ago`
}

function levelDot(level?: string) {
  if (level === 'error') return 'bg-red-500'
  if (level === 'warning') return 'bg-amber-500'
  if (level === 'success') return 'bg-green-500'
  return 'bg-blue-500'
}

export function NotificationPanel() {
  const [visible, setVisible] = useState(false)
  const [mounted, setMounted] = useState(false)
  const notifications = useSyncExternalStore(
    notificationStore.subscribe,
    notificationStore.get,
    () => EMPTY_NOTIFICATIONS
  )
  const [pos, setPos] = useState({ top: 0, right: 0 })
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popoverRef = useRef<HTMLDivElement>(null)
  const { data: session } = useSession()
  const isAdmin = session?.user.role === 'SystemAdmin'

  const open = () => {
    if (triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect()
      setPos({ top: rect.bottom + 8, right: window.innerWidth - rect.right })
    }
    setMounted(true)
    requestAnimationFrame(() => setVisible(true))
  }

  const close = useCallback(() => {
    setVisible(false)
    timerRef.current = setTimeout(() => setMounted(false), 200)
  }, [])

  useEffect(() => () => clearTimeout(timerRef.current), [])

  // Click-outside: close when clicking outside both trigger and popover
  useEffect(() => {
    if (!mounted) return
    const handler = (e: MouseEvent) => {
      const target = e.target as Node
      if (
        triggerRef.current &&
        !triggerRef.current.contains(target) &&
        popoverRef.current &&
        !popoverRef.current.contains(target)
      ) {
        close()
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [mounted, close])

  // Alarm history is admin-only — the endpoint is AdminOnly and 403s everyone
  // else. Gate the fetch on role so non-admins don't fire a doomed request, and
  // so alarm notifications only ever enter an admin's (per-user) store.
  useEffect(() => {
    if (!isAdmin) return
    // Use the typed API client (attaches the NextAuth bearer token) instead of
    // raw fetch — the bare fetch() goes through Next.js's rewrite proxy which
    // doesn't add Authorization, so the backend 401s.
    api
      .get<AlarmDto[]>('/admin/monitoring/alarms')
      .then((alarms) => {
        const existing = notificationStore.get()
        alarms.forEach((a) => {
          if (!existing.some((n) => n.description.includes(a.alarmName))) {
            const friendly = formatAlarmName(a.alarmName)
            notificationStore.push(`Alarm: ${friendly}`, `${a.metricName} — ${a.stateValue} (${a.updated})`, 'warning')
          }
        })
      })
      .catch(() => {
        // Silently ignore — a transient error shouldn't break the panel.
      })
  }, [isAdmin])

  const unreadCount = notifications.filter((n) => !n.read).length

  return (
    <>
      <Button
        ref={triggerRef}
        variant="ghost"
        size="icon"
        className="relative h-9 w-9"
        onClick={() => (mounted ? close() : open())}
        aria-label={`Notifications${unreadCount > 0 ? `, ${unreadCount} unread` : ''}`}
      >
        <Bell className="h-4 w-4" />
        {unreadCount > 0 && (
          <Badge
            variant="destructive"
            className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full p-0 text-[10px] leading-none"
          >
            {unreadCount > 99 ? '99+' : unreadCount}
          </Badge>
        )}
      </Button>

      {mounted &&
        createPortal(
          <div
            ref={popoverRef}
            className={cn(
              'fixed z-50 w-80 overflow-hidden rounded-xl border border-[var(--glass-border)] bg-[var(--glass-bg)] shadow-xl backdrop-blur-2xl transition-all duration-200',
              visible ? 'scale-100 opacity-100' : 'scale-95 opacity-0'
            )}
            style={{ top: pos.top, right: pos.right }}
          >
            <div className="flex items-center justify-between border-b border-border/30 p-4">
              <span className="text-sm font-semibold">Notifications</span>
              {notifications.length > 0 && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 text-xs text-muted-foreground"
                  onClick={() => notificationStore.clearAll()}
                >
                  Clear all
                </Button>
              )}
            </div>

            <ScrollArea className="max-h-[400px]">
              {notifications.length === 0 ? (
                <div className="flex flex-col items-center gap-2 p-6 text-center">
                  <Bell className="h-8 w-8 text-muted-foreground/40" />
                  <p className="text-sm text-muted-foreground">No notifications</p>
                </div>
              ) : (
                <div className="divide-y divide-border/30">
                  {notifications.map((n) => (
                    <div
                      key={n.id}
                      className={cn(
                        'group relative flex items-start gap-2 p-4 pr-8 transition-colors hover:bg-accent/50 cursor-pointer',
                        !n.read && 'bg-accent/20'
                      )}
                      onClick={() => notificationStore.markAsRead(n.id)}
                    >
                      {!n.read && (
                        <span className={cn('mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full', levelDot(n.level))} />
                      )}
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-foreground">{n.title}</p>
                        <p className="mt-0.5 text-xs text-muted-foreground truncate">{n.description}</p>
                        <p className="mt-1 text-[10px] text-muted-foreground/60">{formatRelative(n.timestamp)}</p>
                      </div>
                      <button
                        className="absolute right-3 top-3 rounded-md p-0.5 text-muted-foreground/50 opacity-0 transition-opacity hover:text-foreground group-hover:opacity-100"
                        onClick={(e) => {
                          e.stopPropagation()
                          notificationStore.dismiss(n.id)
                        }}
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </ScrollArea>
          </div>,
          document.body
        )}
    </>
  )
}
