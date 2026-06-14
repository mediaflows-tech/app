'use client'

import type { MediaAssetSummaryDto } from '@/types/api'
import { AssetCard } from './asset-card'

interface AssetGridProps {
  assets: MediaAssetSummaryDto[]
  onSubmit?: (id: number) => void
  onDelete?: (id: number) => void
  selectedIds?: Set<number>
  onToggleSelect?: (id: number, checked: boolean) => void
}

export function AssetGrid({ assets, onSubmit, onDelete, selectedIds, onToggleSelect }: AssetGridProps) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {assets.map((asset) => (
        <AssetCard
          key={asset.id}
          asset={asset}
          onSubmit={onSubmit}
          onDelete={onDelete}
          selected={selectedIds?.has(asset.id)}
          onSelect={onToggleSelect}
          anySelected={(selectedIds?.size ?? 0) > 0}
        />
      ))}
    </div>
  )
}
