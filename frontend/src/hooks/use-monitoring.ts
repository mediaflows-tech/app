'use client'

import { useQuery } from '@tanstack/react-query'
import { useState, useEffect, useRef } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { useSession } from 'next-auth/react'
import { api } from '@/lib/api'
import { createAnalyticsConnection } from '@/lib/signalr'
import type { AnalyticsSnapshotDto } from '@/types/api'

export function useMonitoring() {
  return useQuery({
    queryKey: ['admin-monitoring'],
    queryFn: () => api.get<AnalyticsSnapshotDto>('/admin/monitoring'),
    retry: 2
  })
}

type ConnectionStatus = 'connected' | 'reconnecting' | 'disconnected'

export function useAnalyticsStream() {
  const { data: session } = useSession()
  const [snapshot, setSnapshot] = useState<AnalyticsSnapshotDto | null>(null)
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected')
  const connectionRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    const accessToken = session?.user.accessToken
    if (!accessToken) return
    if (connectionRef.current) return

    const connection = createAnalyticsConnection(accessToken)

    connection.on('ReceiveAnalyticsUpdate', (data: AnalyticsSnapshotDto) => {
      setSnapshot(data)
    })

    connection.onreconnecting(() => {
      setConnectionStatus('reconnecting')
    })

    connection.onreconnected(() => {
      setConnectionStatus('connected')
    })

    connection.onclose(() => {
      setConnectionStatus('disconnected')
    })

    connection
      .start()
      .then(() => {
        setConnectionStatus('connected')
        connectionRef.current = connection
      })
      .catch((err: unknown) => {
        console.warn('Analytics stream connection failed:', err)
        setConnectionStatus('disconnected')
        // Don't throw — the page should still work with REST data
      })

    return () => {
      const conn = connectionRef.current
      if (conn && conn.state === HubConnectionState.Connected) {
        conn.stop()
      }
      connectionRef.current = null
    }
  }, [session?.user.accessToken])

  return { snapshot, connectionStatus }
}
