import { NextResponse } from 'next/server'
import { cognitoCall, computeSecretHash, type CognitoError } from '@/lib/cognito-server'

interface RegisterBody {
  email?: string
  password?: string
  displayName?: string
}

export async function POST(req: Request) {
  let body: RegisterBody
  try {
    body = (await req.json()) as RegisterBody
  } catch {
    return NextResponse.json({ __type: 'InvalidParameterException' }, { status: 400 })
  }

  const email = body.email?.toLowerCase().trim() ?? ''
  const password = body.password ?? ''
  const displayName = body.displayName?.trim() ?? ''

  if (!email || !password || !displayName) {
    return NextResponse.json(
      { __type: 'InvalidParameterException', message: 'Missing required fields.' },
      { status: 400 }
    )
  }

  try {
    await cognitoCall('SignUp', {
      ClientId: process.env.COGNITO_CLIENT_ID!,
      SecretHash: computeSecretHash(email),
      Username: email,
      Password: password,
      UserAttributes: [
        { Name: 'email', Value: email },
        { Name: 'name', Value: displayName }
      ]
    })
    return NextResponse.json({ ok: true })
  } catch (err) {
    const cognitoErr = err as CognitoError
    return NextResponse.json(cognitoErr, { status: 400 })
  }
}
