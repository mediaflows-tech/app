'use client'

import { useEffect, useState } from 'react'
import { badgeStore } from '@/lib/badge-store'

/**
 * Subscribes to the live badge store and returns the current map of
 * { [elementId]: value }. Populated by the SignalR `UpdateBadge` event.
 */
export function useBadges(): Record<string, string> {
  const [badges, setBadges] = useState<Record<string, string>>(() => badgeStore.get())

  useEffect(() => {
    setBadges(badgeStore.get())
    return badgeStore.subscribe(setBadges)
  }, [])

  return badges
}
