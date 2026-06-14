'use client'

import { createContext, useContext, useEffect, useRef, useState } from 'react'
import { useSession } from 'next-auth/react'
import type { HubConnection } from '@microsoft/signalr'
import { createNotificationConnection } from '@/lib/signalr'
import { notify } from '@/components/ui/toast-config'
import { notificationStore } from '@/lib/notification-store'
import { badgeStore } from '@/lib/badge-store'

interface SignalRContextValue {
  connection: HubConnection | null
  connectionState: 'disconnected' | 'connecting' | 'connected' | 'reconnecting'
}

const SignalRContext = createContext<SignalRContextValue>({
  connection: null,
  connectionState: 'disconnected'
})

export function useSignalRContext() {
  return useContext(SignalRContext)
}

export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const { data: session } = useSession()
  const connectionRef = useRef<HubConnection | null>(null)
  const [connection, setConnection] = useState<HubConnection | null>(null)
  const [connectionState, setConnectionState] = useState<SignalRContextValue['connectionState']>('disconnected')

  // Scope persisted notifications to the signed-in user. Keyed on the stable
  // user id (not the access token) so a token refresh doesn't reset the store.
  useEffect(() => {
    notificationStore.setUser(session?.user?.id ?? null)
  }, [session?.user?.id])

  useEffect(() => {
    if (!session?.user.accessToken) return
    const conn = createNotificationConnection(session.user.accessToken)
    connectionRef.current = conn

    conn.onreconnecting(() => setConnectionState('reconnecting'))
    conn.onreconnected(() => setConnectionState('connected'))
    conn.onclose(() => setConnectionState('disconnected'))

    conn.on('ReceiveToast', (title: string, message: string, type: string) => {
      if (type === 'error') notify.error(message)
      else if (type === 'warning') notify.warning(message)
      else notify.success(message)
    })

    conn.on('ReceiveNotification', () => {
      notificationStore.push('System', 'New notification received', 'info')
    })

    // Live sidebar badges — e.g. `pending-review-count` for Editor / SystemAdmin.
    // The backend pushes the initial value in NotificationHub.OnConnectedAsync
    // and re-broadcasts on every review decision.
    conn.on('UpdateBadge', (elementId: string, value: string) => {
      badgeStore.set(elementId, value)
    })

    void Promise.resolve().then(async () => {
      setConnectionState('connecting')
      try {
        await conn.start()
        setConnectionState('connected')
        setConnection(conn)
      } catch (err: unknown) {
        console.error('SignalR connection failed:', err)
        setConnectionState('disconnected')
      }
    })

    return () => {
      conn.stop()
      connectionRef.current = null
      setConnection(null)
      badgeStore.clear()
    }
  }, [session?.user.accessToken])

  return <SignalRContext.Provider value={{ connection, connectionState }}>{children}</SignalRContext.Provider>
}
