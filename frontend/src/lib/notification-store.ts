export interface AppNotification {
  id: string
  title: string
  description: string
  timestamp: Date
  read: boolean
  level?: 'success' | 'error' | 'warning' | 'info'
}

// Notifications are per-user: the key is namespaced by the signed-in user id so
// one account's notifications can't bleed into another's on a shared browser.
// While logged out (no user set) there is no key — reads return empty and writes
// are dropped.
const STORAGE_PREFIX = 'mf-notifications'
const MAX_ITEMS = 50

type Listener = (items: AppNotification[]) => void

let listeners: Listener[] = []
let cache: AppNotification[] | null = null
let currentUserId: string | null = null

function storageKey(): string | null {
  return currentUserId ? `${STORAGE_PREFIX}:${currentUserId}` : null
}

function load(): AppNotification[] {
  if (cache) return cache
  const key = storageKey()
  // Cache every outcome (including empties) so get() keeps a stable reference —
  // useSyncExternalStore re-renders whenever the snapshot identity changes, so it
  // must not get a fresh [] on each call.
  if (typeof window === 'undefined' || !key) {
    cache = []
    return cache!
  }
  try {
    const raw = localStorage.getItem(key)
    cache = raw ? JSON.parse(raw, (k, v) => (k === 'timestamp' ? new Date(v) : v)) : []
    return cache!
  } catch {
    cache = []
    return cache!
  }
}

function save(items: AppNotification[]) {
  cache = items
  const key = storageKey()
  if (typeof window !== 'undefined' && key) {
    localStorage.setItem(key, JSON.stringify(items))
  }
  listeners.forEach((fn) => fn(items))
}

export const notificationStore = {
  get: load,

  // Switch the active user. Resets the in-memory cache and reloads from that
  // user's namespaced key, then notifies subscribers so the panel reflects the
  // new session instead of the previous user's notifications.
  setUser(userId: string | null) {
    if (userId === currentUserId) return
    currentUserId = userId
    cache = null
    const items = load()
    listeners.forEach((fn) => fn(items))
  },

  push(title: string, description: string, level: AppNotification['level'] = 'info') {
    const items = load()
    items.unshift({
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
      title,
      description,
      timestamp: new Date(),
      read: false,
      level
    })
    save(items.slice(0, MAX_ITEMS))
  },

  markAsRead(id: string) {
    const items = load().map((n) => (n.id === id ? { ...n, read: true } : n))
    save(items)
  },

  dismiss(id: string) {
    save(load().filter((n) => n.id !== id))
  },

  clearAll() {
    save([])
  },

  unreadCount(): number {
    return load().filter((n) => !n.read).length
  },

  subscribe(listener: Listener): () => void {
    listeners.push(listener)
    return () => {
      listeners = listeners.filter((fn) => fn !== listener)
    }
  }
}
