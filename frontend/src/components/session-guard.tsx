'use client'

import { useEffect } from 'react'
import { useSession, signOut } from 'next-auth/react'

/**
 * Client-side counterpart to the server-side check in (app)/layout.tsx.
 * The server layout only re-runs on full navigations, so after a failed token
 * refresh (session.error === 'RefreshTokenError') the stale token would keep
 * being sent to the API on client-side route changes until the next full
 * render. This watches the session on the client and signs out immediately.
 */
export function SessionGuard() {
  const { data: session } = useSession()

  useEffect(() => {
    if (session?.error === 'RefreshTokenError') {
      void signOut({ callbackUrl: '/login?error=SessionRequired' })
    }
  }, [session?.error])

  return null
}
