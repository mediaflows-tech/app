import type { Metadata } from 'next'
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { auth } from '@/lib/auth'
import { ReviewDetailClient } from './review-detail-client'
import { Button } from '@/components/ui/button'
import { ArrowLeft } from 'lucide-react'
import { toReviewDetails } from '@/lib/review-mappers'
import type { ReviewDetailsApiResponse } from '@/types/api'

interface Props {
  params: Promise<{ id: string }>
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params
  return { title: `Review #${id}` }
}

export default async function ApprovalDetailPage({ params }: Props) {
  const { id } = await params
  const assetId = parseInt(id, 10)
  if (isNaN(assetId)) notFound()

  const session = await auth()
  if (!session?.user?.accessToken) notFound()

  const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''
  const res = await fetch(`${apiBase}/api/v1/reviews/${assetId}`, {
    headers: { Authorization: `Bearer ${session.user.accessToken}` },
    cache: 'no-store'
  })
  if (!res.ok) notFound()
  const raw = (await res.json()) as ReviewDetailsApiResponse
  const reviewDetail = toReviewDetails(raw, assetId)

  return (
    <div className="space-y-6">
      <div>
        <Button variant="link" className="px-0" render={<Link href="/review" />}>
          <ArrowLeft className="mr-1 h-4 w-4" />
          Review Queue
        </Button>
      </div>
      <ReviewDetailClient initialData={reviewDetail} assetId={assetId} />
    </div>
  )
}
