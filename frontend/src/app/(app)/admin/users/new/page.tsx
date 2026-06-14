import { UserForm } from '@/components/admin/user-form'

export const metadata = {
  title: 'Create User | MediaFlows Admin'
}

export default function NewUserPage() {
  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Create User</h1>
        <p className="text-sm text-muted-foreground">Add a new user to the platform</p>
      </div>
      <UserForm mode="create" />
    </div>
  )
}
