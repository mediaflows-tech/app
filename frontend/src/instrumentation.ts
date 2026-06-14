// frontend/src/instrumentation.ts
//
// Loaded once per Lambda cold start by Next.js. We use it to populate
// process.env with secrets fetched from SSM Parameter Store before any
// request handler runs (NextAuth/Auth.js reads process.env on demand).
//
// Why this exists:
// - The secrets (nextauth-secret, cognito-client-secret) are NOT set as
//   Amplify env vars, and amplify.yml only writes non-secret config to
//   .env.local — so they must be loaded from SSM at runtime here. Verified
//   via `aws amplify get-app` env keys + `aws ssm get-parameters-by-path`.
// - App-level Amplify environmentVariables are documented as available at
//   SSR runtime but in practice don't propagate, so secrets set there are
//   missing at request time and NextAuth throws "server configuration".
// - Hard-committing secrets to .env.production works but leaks them into
//   git. Loading from SSM at runtime keeps the secret surface in AWS.
//
// The amplify_ssr IAM role (set via Terraform as both iam_service_role_arn
// and compute_role_arn) is granted ssm:GetParametersByPath on the
// /mediaflows/<env>/ prefix, so this fetch only needs the path; no credentials.

// Required by NextAuth/Auth.js and the Cognito provider at SSR runtime.
const REQUIRED_RUNTIME_SERVER_SECRETS = [
  'AUTH_SECRET',
  'COGNITO_CLIENT_ID',
  'COGNITO_CLIENT_SECRET',
  'COGNITO_ISSUER'
] as const

export async function register() {
  // Skip on edge runtime; Next.js sets NEXT_RUNTIME='edge' there. Node runtime
  // sometimes leaves NEXT_RUNTIME undefined (observed empirically in Amplify
  // WEB_COMPUTE), so we only short-circuit on the explicit edge value.
  if (process.env.NEXT_RUNTIME === 'edge') return

  const path = process.env.SSM_SECRETS_PATH
  const region = process.env.AWS_REGION || 'ap-southeast-1'

  // Defensive try/catch: never let instrumentation break SSR. Failure here
  // means NextAuth will return its own "server configuration" 500 (clearer
  // signal) instead of an opaque container crash.
  try {
    if (!path) {
      console.warn('[instrumentation] SSM_SECRETS_PATH unset — skipping runtime secret load')
      reportMissingRequiredRuntimeSecrets()
      return
    }

    // SSM is the only source of the secrets (nextauth-secret, cognito-client-secret):
    // they are not Amplify env vars and amplify.yml writes only non-secret config to
    // .env.local, so this runtime load is what makes NextAuth work.
    const { SSMClient, GetParametersByPathCommand } = await import('@aws-sdk/client-ssm')
    const client = new SSMClient({ region })

    let loaded = 0
    let nextToken: string | undefined
    do {
      const response = await client.send(
        new GetParametersByPathCommand({
          Path: path,
          WithDecryption: true,
          Recursive: true,
          NextToken: nextToken
        })
      )

      for (const param of response.Parameters ?? []) {
        if (!param.Name || !param.Value) continue
        const key = ssmNameToEnvKey(param.Name, path)
        if (!key) continue

        // Don't clobber values that were already set explicitly (e.g. by
        // .env.production or the Amplify env), unless the runtime value is empty.
        if (process.env[key] && process.env[key] !== '') continue

        process.env[key] = param.Value
        loaded++

        // The NextAuth/Auth.js v5 fallback chain is AUTH_* ?? NEXTAUTH_*.
        // If only one of them is in SSM (e.g. nextauth-secret), mirror it
        // to the AUTH_ name too so both cousins resolve.
        if (key === 'NEXTAUTH_SECRET' && !process.env.AUTH_SECRET) {
          process.env.AUTH_SECRET = param.Value
        }
        if (key === 'NEXTAUTH_URL' && !process.env.AUTH_URL) {
          process.env.AUTH_URL = param.Value
        }
      }
      nextToken = response.NextToken
    } while (nextToken)

    console.log(`[instrumentation] loaded ${loaded} secrets from SSM at ${path}`)
    reportMissingRequiredRuntimeSecrets()
  } catch (err) {
    console.error(
      `[instrumentation] SSM load failed for path ${path || '(unset)'} in region ${region}; required secrets may now be missing:`,
      err
    )
    reportMissingRequiredRuntimeSecrets()
    // Swallow — let the route's natural error path report.
  }
}

function reportMissingRequiredRuntimeSecrets() {
  const missing = REQUIRED_RUNTIME_SERVER_SECRETS.filter((key) => !process.env[key] || process.env[key] === '')
  if (missing.length === 0) return

  console.error(
    `[instrumentation] REQUIRED RUNTIME SERVER SECRETS MISSING: ${missing.join(', ')}. NextAuth will otherwise return a "server configuration" 500.`
  )
}

// '/mediaflows/prod/cognito-client-secret' (with prefix '/mediaflows/prod/')
//   → 'COGNITO_CLIENT_SECRET'
function ssmNameToEnvKey(name: string, prefix: string): string | null {
  if (!name.startsWith(prefix)) return null
  const suffix = name.slice(prefix.length).replace(/^\/+/, '')
  if (!suffix) return null
  return suffix.toUpperCase().replace(/-/g, '_')
}
