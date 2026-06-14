'use client'

import { useSession } from 'next-auth/react'
import { PageHeader } from '@/components/shared/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { RoleBadge } from '@/components/ui/role-badge'
import { Skeleton } from '@/components/ui/skeleton'

function getInitials(name?: string | null, email?: string | null): string {
  if (name) {
    const parts = name.trim().split(/\s+/)
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    return name.slice(0, 2).toUpperCase()
  }
  if (email) return email.slice(0, 2).toUpperCase()
  return 'U'
}

export default function ProfilePage() {
  const { data: session, status } = useSession()
  const user = session?.user

  if (status === 'loading') {
    return (
      <div className="mx-auto max-w-lg space-y-6">
        <PageHeader title="Profile" description="Your account information" />
        <Card>
          <CardContent className="space-y-4 pt-6">
            <Skeleton className="mx-auto h-20 w-20 rounded-full" />
            <Skeleton className="mx-auto h-5 w-48" />
            <Skeleton className="mx-auto h-4 w-64" />
          </CardContent>
        </Card>
      </div>
    )
  }

  const initials = getInitials(user?.name, user?.email)

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <PageHeader title="Profile" description="Your account information" />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Account Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="flex items-center gap-4">
            <Avatar className="h-16 w-16">
              <AvatarImage src={user?.image ?? undefined} alt={user?.name ?? 'User'} />
              <AvatarFallback className="text-lg font-medium">{initials}</AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <h2 className="text-lg font-semibold truncate">{user?.name ?? 'Unknown User'}</h2>
              <p className="text-sm text-muted-foreground truncate">{user?.email ?? 'No email'}</p>
            </div>
          </div>

          <div className="space-y-3 border-t pt-4">
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Display Name</span>
              <span className="text-sm font-medium">{user?.name ?? '-'}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Email</span>
              <span className="text-sm font-medium">{user?.email ?? '-'}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Role</span>
              <RoleBadge role={user?.role ?? 'Viewer'} />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
