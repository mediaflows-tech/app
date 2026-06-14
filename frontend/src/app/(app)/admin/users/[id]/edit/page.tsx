'use client'

import { use } from 'react'
import { UserForm } from '@/components/admin/user-form'
import { useUser } from '@/hooks/use-admin-users'
import { Skeleton } from '@/components/ui/skeleton'

interface EditUserPageProps {
  params: Promise<{ id: string }>
}

export default function EditUserPage({ params }: EditUserPageProps) {
  const { id } = use(params)
  const { data: user, isLoading } = useUser(id)

  if (isLoading) {
    return (
      <div className="mx-auto max-w-lg space-y-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-[400px]" />
      </div>
    )
  }

  if (!user) {
    return (
      <div className="mx-auto max-w-lg py-12 text-center">
        <p className="text-sm text-muted-foreground">User not found.</p>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Edit User</h1>
        <p className="text-sm text-muted-foreground">Update user details and role</p>
      </div>
      <UserForm user={user} mode="edit" />
    </div>
  )
}
