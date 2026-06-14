'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import toast from 'react-hot-toast'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { registerUser, mapAmplifyError } from '@/lib/auth-client'

const PASSWORD_RULES = [
  'At least 8 characters',
  'Uppercase and lowercase letters',
  'At least one number',
  'At least one symbol'
]

// Mirrors the Cognito user pool password policy in infrastructure/modules/auth.
// Client-side check only — server enforcement is still authoritative.
const PASSWORD_REGEX = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/

export default function RegisterPage() {
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const router = useRouter()

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()

    const normalizedEmail = email.trim().toLowerCase()
    const trimmedDisplayName = displayName.trim()

    if (!PASSWORD_REGEX.test(password)) {
      toast.error('Password must be at least 8 characters and include uppercase, lowercase, a number, and a symbol.', {
        id: 'register-error'
      })
      return
    }

    if (password !== confirmPassword) {
      toast.error('Passwords do not match.', { id: 'register-error' })
      return
    }

    setLoading(true)
    try {
      await registerUser(normalizedEmail, password, trimmedDisplayName)
      router.push(`/confirm?email=${encodeURIComponent(normalizedEmail)}`)
    } catch (err) {
      toast.error(mapAmplifyError(err, 'Registration failed. Please try again.'), { id: 'register-error' })
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-2xl font-bold tracking-tight">Create account</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">Start managing your media workflows</p>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="displayName"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Display Name
          </Label>
          <Input
            id="displayName"
            type="text"
            placeholder="Your name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            required
            maxLength={256}
            autoComplete="name"
            autoFocus
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="email"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Email
          </Label>
          <Input
            id="email"
            type="email"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoComplete="email"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="password"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Password
          </Label>
          <Input
            id="password"
            type="password"
            placeholder="Create a password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoComplete="new-password"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
          <ul className="mt-1 space-y-0.5">
            {PASSWORD_RULES.map((rule) => (
              <li key={rule} className="flex items-center gap-1.5 text-[0.6875rem] text-muted-foreground">
                <span className="size-1 rounded-full bg-muted-foreground/50" />
                {rule}
              </li>
            ))}
          </ul>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="confirmPassword"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Confirm Password
          </Label>
          <Input
            id="confirmPassword"
            type="password"
            placeholder="Re-enter password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            autoComplete="new-password"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <Button type="submit" disabled={loading} className="w-full rounded-full" size="lg">
          {loading ? 'Creating account...' : 'Create Account'}
        </Button>
      </form>

      <p className="mt-5 text-center text-[0.8125rem] text-muted-foreground">
        Already have an account?{' '}
        <Link href="/login" className="font-semibold text-foreground hover:underline">
          Sign in
        </Link>
      </p>
    </>
  )
}
