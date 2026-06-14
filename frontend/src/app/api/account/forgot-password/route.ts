import { NextResponse } from 'next/server'
import { cognitoCall, computeSecretHash, type CognitoError } from '@/lib/cognito-server'

interface ForgotBody {
  email?: string
}

export async function POST(req: Request) {
  let body: ForgotBody
  try {
    body = (await req.json()) as ForgotBody
  } catch {
    return NextResponse.json({ __type: 'InvalidParameterException' }, { status: 400 })
  }

  const email = body.email?.toLowerCase().trim() ?? ''
  if (!email) {
    return NextResponse.json({ __type: 'InvalidParameterException', message: 'Missing email.' }, { status: 400 })
  }

  try {
    await cognitoCall('ForgotPassword', {
      ClientId: process.env.COGNITO_CLIENT_ID!,
      SecretHash: computeSecretHash(email),
      Username: email
    })
    return NextResponse.json({ ok: true })
  } catch (err) {
    return NextResponse.json(err as CognitoError, { status: 400 })
  }
}
