export class ApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public body?: { error?: string; message?: string; details?: string }
  ) {
    super(body?.message ?? body?.error ?? statusText)
    this.name = 'ApiError'
  }
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''

async function getAccessToken(): Promise<string | null> {
  if (typeof window === 'undefined') {
    // Server-side: dynamically import to avoid bundling server-only module into client chunks
    const { auth } = await import('@/lib/auth')
    const session = await auth()
    return session?.user.accessToken ?? null
  }
  const { getSession } = await import('next-auth/react')
  const session = await getSession()
  return session?.user.accessToken ?? null
}

// Ensures a rejected access token signs the user out only once, even if several
// in-flight requests 401 at the same time.
let signingOut = false

async function forceSignOut(): Promise<void> {
  if (signingOut) return
  signingOut = true
  try {
    const { signOut } = await import('next-auth/react')
    await signOut({ callbackUrl: '/login?error=SessionRequired' })
  } catch {
    // Best-effort — the thrown ApiError still surfaces to the caller.
  }
}

async function request<T>(path: string, options: RequestInit = {}, _retry = true): Promise<T> {
  const token = await getAccessToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>)
  }
  if (token) headers['Authorization'] = `Bearer ${token}`

  const url = `${API_BASE}/api/v1${path}`
  const res = await fetch(url, { ...options, headers })

  if (!res.ok) {
    // Client-side 401 on first attempt — session may not be hydrated yet; retry once
    if (res.status === 401 && _retry && typeof window !== 'undefined') {
      await new Promise((r) => setTimeout(r, 500))
      return request<T>(path, options, false)
    }
    // Persistent client-side 401 — the access token is rejected (expired/revoked),
    // so the session is dead. Sign out so the user isn't left firing failing calls
    // with a stale token (complements SessionGuard's window-focus/mount check).
    if (res.status === 401 && typeof window !== 'undefined') {
      void forceSignOut()
    }
    const body = await res.json().catch(() => undefined)
    throw new ApiError(res.status, res.statusText, body)
  }
  if (res.status === 204) return undefined as T
  return res.json()
}

export const api = {
  get: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'GET' }),
  post: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'POST', body: JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'PUT', body: JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'PATCH', body: JSON.stringify(body) }),
  delete: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'DELETE' })
}
