import Link from 'next/link'
import { Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { UserTable } from '@/components/admin/user-table'

export const metadata = {
  title: 'Users | MediaFlows Admin'
}

export default function UsersPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
          <p className="text-sm text-muted-foreground">Manage platform users and their roles</p>
        </div>
        <Button render={<Link href="/admin/users/new" />}>
          <Plus className="mr-2 h-4 w-4" />
          New User
        </Button>
      </div>
      <UserTable />
    </div>
  )
}
