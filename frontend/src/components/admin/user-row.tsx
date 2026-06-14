'use client'

import { useState } from 'react'
import Link from 'next/link'
import { MoreHorizontal, Pencil, ShieldOff, Shield, Trash2 } from 'lucide-react'
import { TableCell, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { RoleBadge } from '@/components/ui/role-badge'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog'
import { useDisableUser, useEnableUser, useDeleteUser } from '@/hooks/use-admin-users'
import { formatRelativeTime } from '@/lib/utils'
import type { CognitoUserDto } from '@/types/api'

interface UserRowProps {
  user: CognitoUserDto
}

function initials(name: string): string {
  return name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
}

export function UserRow({ user }: UserRowProps) {
  const [showDeleteDialog, setShowDeleteDialog] = useState(false)
  const disableUser = useDisableUser()
  const enableUser = useEnableUser()
  const deleteUser = useDeleteUser()

  const isDisabled = !user.enabled

  return (
    <>
      <TableRow className={isDisabled ? 'opacity-60' : undefined}>
        <TableCell>
          <div className="flex items-center gap-3">
            <Avatar className="h-8 w-8">
              <AvatarFallback className="text-xs">{initials(user.displayName || user.email)}</AvatarFallback>
            </Avatar>
            <div>
              <p className="text-sm font-medium leading-none">{user.displayName}</p>
              <p className="text-xs text-muted-foreground">{user.email}</p>
            </div>
          </div>
        </TableCell>
        <TableCell>
          <RoleBadge role={user.role} />
        </TableCell>
        <TableCell>
          <Badge variant={isDisabled ? 'destructive' : 'secondary'}>{user.status}</Badge>
        </TableCell>
        <TableCell className="text-sm text-muted-foreground">
          {user.lastModifiedAt ? formatRelativeTime(user.lastModifiedAt) : 'Never'}
        </TableCell>
        <TableCell className="text-right">
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon" className="h-8 w-8" />}>
              <MoreHorizontal className="h-4 w-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem render={<Link href={`/admin/users/${user.userId}/edit`} />}>
                <Pencil className="mr-2 h-3.5 w-3.5" />
                Edit
              </DropdownMenuItem>
              {isDisabled ? (
                <DropdownMenuItem onClick={() => enableUser.mutate(user.userId)} disabled={enableUser.isPending}>
                  <Shield className="mr-2 h-3.5 w-3.5" />
                  Enable
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem onClick={() => disableUser.mutate(user.userId)} disabled={disableUser.isPending}>
                  <ShieldOff className="mr-2 h-3.5 w-3.5" />
                  Disable
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onClick={() => setShowDeleteDialog(true)}>
                <Trash2 className="mr-2 h-3.5 w-3.5" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </TableCell>
      </TableRow>

      <AlertDialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete user</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete{' '}
              <span className="font-medium text-foreground">{user.displayName || user.email}</span>? This action cannot
              be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                deleteUser.mutate(user.userId)
                setShowDeleteDialog(false)
              }}
              variant="destructive"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
