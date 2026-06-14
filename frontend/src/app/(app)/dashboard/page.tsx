import { auth } from '@/lib/auth'
import { DashboardGreeting } from '@/components/dashboard/dashboard-greeting'
import { RecentCarousel } from '@/components/dashboard/recent-carousel'

export default async function DashboardPage() {
  const session = await auth()
  const name = session?.user.name ?? session?.user.email ?? 'there'

  return (
    <div className="grid h-[calc(100vh-var(--topbar-height)-3rem)] grid-rows-[auto_1fr] gap-6">
      <DashboardGreeting name={name.split(' ')[0]} />
      <RecentCarousel />
    </div>
  )
}
