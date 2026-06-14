'use client'

import { use } from 'react'
import { AssetDetailView } from './asset-detail-view'

interface AssetDetailPageProps {
  params: Promise<{ id: string }>
}

export default function AssetDetailPage({ params }: AssetDetailPageProps) {
  const { id } = use(params)
  const assetId = Number(id)

  if (isNaN(assetId) || assetId <= 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <p className="text-sm text-muted-foreground">Invalid asset ID.</p>
      </div>
    )
  }

  return <AssetDetailView assetId={assetId} />
}
