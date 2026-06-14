'use client'

import { useEffect, useState, useCallback } from 'react'
import { notificationStore, type AppNotification } from '@/lib/notification-store'

export function useNotifications() {
  const [notifications, setNotifications] = useState<AppNotification[]>([])

  useEffect(() => {
    setNotifications(notificationStore.get())
    return notificationStore.subscribe(setNotifications)
  }, [])

  const unreadCount = notifications.filter((n) => !n.read).length

  const markAllRead = useCallback(() => {
    notifications.filter((n) => !n.read).forEach((n) => notificationStore.markAsRead(n.id))
  }, [notifications])

  return { notifications, unreadCount, markAllRead }
}
