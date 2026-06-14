import { redirect } from 'next/navigation'
import { auth, signOut } from '@/lib/auth'
import { SignalRProvider } from '@/providers/signalr-provider'
import { AppShell } from '@/components/layout/app-shell'
import { SessionGuard } from '@/components/session-guard'

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const session = await auth()

  if (!session) {
    redirect('/login')
  }

  if (session.error === 'RefreshTokenError') {
    await signOut({ redirect: false })
    redirect('/login?error=SessionRequired')
  }

  return (
    <SignalRProvider>
      <SessionGuard />
      <AppShell>{children}</AppShell>
    </SignalRProvider>
  )
}
