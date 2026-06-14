'use client'

import { useState } from 'react'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Skeleton } from '@/components/ui/skeleton'
import { UserRow } from './user-row'
import { useUsers } from '@/hooks/use-admin-users'

const ROLE_TABS = ['All', 'SystemAdmin', 'ContentCreator', 'Editor', 'Viewer'] as const

export function UserTable() {
  const [role, setRole] = useState<string>('All')
  const { data: users, isLoading } = useUsers(role)

  return (
    <div className="space-y-4">
      <Tabs value={role} onValueChange={setRole}>
        <TabsList>
          {ROLE_TABS.map((tab) => (
            <TabsTrigger key={tab} value={tab} className="text-xs">
              {tab === 'All' ? 'All' : tab.replace(/([A-Z])/g, ' $1').trim()}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-14 w-full" />
          ))}
        </div>
      ) : !users?.length ? (
        <div className="py-12 text-center">
          <p className="text-sm text-muted-foreground">No users found for this role.</p>
        </div>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>User</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Last Modified</TableHead>
                <TableHead className="w-[50px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((user) => (
                <UserRow key={user.userId} user={user} />
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  )
}
