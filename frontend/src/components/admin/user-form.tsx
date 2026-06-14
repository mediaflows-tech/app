'use client'

import { useForm } from 'react-hook-form'
import { useRouter } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { useCreateUser, useUpdateUser } from '@/hooks/use-admin-users'
import type { CognitoUserDto } from '@/types/api'

const ROLES = [
  { value: 'SystemAdmin', label: 'System Admin' },
  { value: 'ContentCreator', label: 'Content Creator' },
  { value: 'Editor', label: 'Editor' },
  { value: 'Viewer', label: 'Viewer' }
] as const

interface UserFormData {
  email: string
  displayName: string
  role: string
  temporaryPassword: string
}

interface UserFormProps {
  user?: CognitoUserDto
  mode: 'create' | 'edit'
}

export function UserForm({ user, mode }: UserFormProps) {
  const router = useRouter()
  const createUser = useCreateUser()
  const updateUser = useUpdateUser()

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors }
  } = useForm<UserFormData>({
    defaultValues: {
      email: user?.email ?? '',
      displayName: user?.displayName ?? '',
      role: user?.role ?? 'Viewer',
      temporaryPassword: ''
    }
  })

  const selectedRole = watch('role')
  const isPending = createUser.isPending || updateUser.isPending

  async function onSubmit(data: UserFormData) {
    if (mode === 'create') {
      await createUser.mutateAsync(data)
    } else if (user) {
      await updateUser.mutateAsync({
        id: user.userId,
        data: {
          displayName: data.displayName,
          role: data.role
        }
      })
    }
    router.push('/admin/users')
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">{mode === 'create' ? 'Create User' : 'Edit User'}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              placeholder="user@example.com"
              disabled={mode === 'edit'}
              {...register('email', { required: 'Email is required' })}
            />
            {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="displayName">Display Name</Label>
            <Input
              id="displayName"
              placeholder="John Doe"
              {...register('displayName', {
                required: 'Display name is required'
              })}
            />
            {errors.displayName && <p className="text-xs text-destructive">{errors.displayName.message}</p>}
          </div>

          <div className="space-y-2">
            <Label>Role</Label>
            <Select value={selectedRole} onValueChange={(v) => setValue('role', v ?? 'Viewer')}>
              <SelectTrigger>
                <SelectValue placeholder="Select a role" />
              </SelectTrigger>
              <SelectContent>
                {ROLES.map((role) => (
                  <SelectItem key={role.value} value={role.value}>
                    {role.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {mode === 'edit' && (
              <p className="text-xs text-muted-foreground">Changing roles takes effect immediately upon save.</p>
            )}
          </div>

          {mode === 'create' && (
            <div className="space-y-2">
              <Label htmlFor="temporaryPassword">Temporary Password</Label>
              <Input
                id="temporaryPassword"
                type="password"
                placeholder="Min 8 characters"
                {...register('temporaryPassword', {
                  required: mode === 'create' ? 'Temporary password is required' : false,
                  minLength: {
                    value: 8,
                    message: 'Password must be at least 8 characters'
                  }
                })}
              />
              {errors.temporaryPassword && (
                <p className="text-xs text-destructive">{errors.temporaryPassword.message}</p>
              )}
              <p className="text-xs text-muted-foreground">The user will be required to change this on first login.</p>
            </div>
          )}
        </CardContent>
        <CardFooter className="flex justify-between border-t pt-6">
          <Button type="button" variant="ghost" onClick={() => router.push('/admin/users')}>
            Cancel
          </Button>
          <Button type="submit" disabled={isPending}>
            {isPending
              ? mode === 'create'
                ? 'Creating...'
                : 'Saving...'
              : mode === 'create'
                ? 'Create User'
                : 'Save Changes'}
          </Button>
        </CardFooter>
      </Card>
    </form>
  )
}
