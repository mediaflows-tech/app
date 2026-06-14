'use client'

import { Suspense, useState, useRef, useCallback } from 'react'
import { useRouter, useSearchParams, redirect } from 'next/navigation'
import { TriangleAlert, CheckCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { confirmRegistration, resendConfirmationCode, mapAmplifyError } from '@/lib/auth-client'

function ConfirmForm() {
  const [code, setCode] = useState(['', '', '', '', '', ''])
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [loading, setLoading] = useState(false)
  const [resending, setResending] = useState(false)
  const codeRef = useRef(code)
  codeRef.current = code
  const inputRefs = useRef<(HTMLInputElement | null)[]>([])
  const searchParams = useSearchParams()
  const router = useRouter()
  const email = searchParams.get('email') ?? ''

  // Guard: redirect if no email param
  if (!email) {
    redirect('/register')
  }

  const handleChange = useCallback((index: number, value: string) => {
    if (value && !/^\d$/.test(value)) return
    setCode((prev) => {
      const next = [...prev]
      next[index] = value
      return next
    })
    if (value && index < 5) {
      inputRefs.current[index + 1]?.focus()
    }
  }, [])

  const handleKeyDown = useCallback((index: number, e: React.KeyboardEvent) => {
    if (e.key === 'Backspace' && !codeRef.current[index] && index > 0) {
      inputRefs.current[index - 1]?.focus()
    }
  }, [])

  const handlePaste = useCallback((e: React.ClipboardEvent) => {
    e.preventDefault()
    const paste = e.clipboardData.getData('text').trim()
    const digits = paste.replace(/\D/g, '').slice(0, 6)
    if (!digits) return
    setCode((prev) => {
      const next = [...prev]
      for (let i = 0; i < digits.length; i++) {
        next[i] = digits[i]
      }
      return next
    })
    inputRefs.current[Math.min(digits.length, 5)]?.focus()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const fullCode = code.join('')
    if (fullCode.length !== 6) {
      setError('Please enter the 6-digit code.')
      return
    }
    setError('')
    setSuccess('')
    setLoading(true)

    try {
      await confirmRegistration(email, fullCode)
      router.push('/login?confirmed=true')
    } catch (err) {
      setError(mapAmplifyError(err, 'Verification failed. Please try again.'))
      setLoading(false)
    }
  }

  async function handleResend() {
    setResending(true)
    setError('')
    setSuccess('')

    try {
      await resendConfirmationCode(email)
      setSuccess('A new code has been sent. Please check your inbox.')
    } catch (err) {
      setError(mapAmplifyError(err, 'Failed to resend code. Please try again.'))
    } finally {
      setResending(false)
    }
  }

  return (
    <>
      <h1 className="text-2xl font-bold tracking-tight">Verify your email</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">
        Enter the 6-digit code sent to <span className="font-medium text-foreground">{email}</span>
      </p>

      {success && (
        <div className="mb-4 flex items-start gap-2 rounded-lg bg-green-500/10 px-3 py-2 text-sm text-green-700 dark:text-green-400">
          <CheckCircle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>{success}</p>
        </div>
      )}

      {error && (
        <div className="mb-4 flex items-start gap-2 rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>{error}</p>
        </div>
      )}

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label
            id="code-label"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Verification Code
          </Label>
          <div className="flex justify-center gap-2" role="group" aria-labelledby="code-label">
            {code.map((digit, i) => (
              <input
                key={i}
                ref={(el) => {
                  inputRefs.current[i] = el
                }}
                type="text"
                inputMode="numeric"
                maxLength={1}
                value={digit}
                onChange={(e) => handleChange(i, e.target.value)}
                onKeyDown={(e) => handleKeyDown(i, e)}
                onPaste={handlePaste}
                autoFocus={i === 0}
                aria-label={`Digit ${i + 1} of 6`}
                disabled={loading}
                className="size-12 rounded-lg border border-transparent bg-muted text-center text-xl font-semibold outline-none transition-colors focus:border-ring focus:bg-background focus:ring-3 focus:ring-ring/50 disabled:opacity-50"
              />
            ))}
          </div>
        </div>

        <Button type="submit" disabled={loading} className="w-full rounded-full" size="lg">
          {loading ? 'Verifying...' : 'Verify Email'}
        </Button>
      </form>

      <p className="mt-5 text-center text-[0.8125rem] text-muted-foreground">
        Didn&apos;t receive a code?{' '}
        <button
          type="button"
          onClick={handleResend}
          disabled={resending}
          className="font-semibold text-foreground hover:underline disabled:opacity-50"
        >
          {resending ? 'Sending...' : 'Resend'}
        </button>
      </p>
    </>
  )
}

function ConfirmSkeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-7 w-48 rounded bg-muted" />
      <div className="h-4 w-64 rounded bg-muted" />
      <div className="flex justify-center gap-2 py-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="size-12 rounded-lg bg-muted" />
        ))}
      </div>
      <div className="h-10 rounded-full bg-muted" />
    </div>
  )
}

export default function ConfirmPage() {
  return (
    <Suspense fallback={<ConfirmSkeleton />}>
      <ConfirmForm />
    </Suspense>
  )
}
