import { Suspense } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { AssetDetailContent } from './asset-detail-content'

interface AssetDetailPageProps {
  params: Promise<{ id: string }>
}

export default async function AssetDetailPage({ params }: AssetDetailPageProps) {
  const { id } = await params
  const assetId = parseInt(id, 10)

  return (
    <Suspense
      fallback={
        <div className="space-y-6">
          <Skeleton className="h-8 w-48" />
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
            <Skeleton className="aspect-video w-full rounded-lg lg:col-span-2" />
            <div className="space-y-4">
              <Skeleton className="h-40 w-full rounded-lg" />
              <Skeleton className="h-32 w-full rounded-lg" />
            </div>
          </div>
        </div>
      }
    >
      <AssetDetailContent assetId={assetId} />
    </Suspense>
  )
}
