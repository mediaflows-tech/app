'use client'

import { use, useCallback, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ArrowLeft } from 'lucide-react'
import Link from 'next/link'
import { toast } from '@/lib/toast'
import { useVersions, useRevert } from '@/hooks/use-versions'
import { VersionTable } from '@/components/versions/version-table'
import { VersionCompare } from '@/components/versions/version-compare'
import { VersionUpload } from '@/components/versions/version-upload'

interface VersionsPageProps {
  params: Promise<{ id: string }>
}

export default function VersionsPage({ params }: VersionsPageProps) {
  const { id } = use(params)
  const assetId = parseInt(id, 10)
  const { data: versions, isLoading } = useVersions(assetId)
  const revert = useRevert()

  const [compareSelection, setCompareSelection] = useState<[number | null, number | null]>([null, null])

  const handleToggleCompare = useCallback((versionId: number) => {
    setCompareSelection((prev) => {
      if (prev[0] === versionId) return [null, prev[1]]
      if (prev[1] === versionId) return [prev[0], null]
      if (prev[0] === null) return [versionId, prev[1]]
      if (prev[1] === null) return [prev[0], versionId]
      return [versionId, null]
    })
  }, [])

  const handleRevert = useCallback(
    (versionId: number) => {
      revert.mutate(
        { assetId, versionId },
        {
          onSuccess: () => toast.success('Reverted to selected version'),
          onError: () => toast.error('Failed to revert version')
        }
      )
    },
    [assetId, revert]
  )

  const canCompare =
    compareSelection[0] !== null && compareSelection[1] !== null && compareSelection[0] !== compareSelection[1]

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href={`/creator/assets/${assetId}`}>
            <Button variant="ghost" size="icon">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-xl font-semibold tracking-tight">Version History</h1>
            <p className="text-sm text-muted-foreground">Manage and compare asset versions</p>
          </div>
        </div>
        <VersionUpload assetId={assetId} />
      </div>

      {/* Comparison panel */}
      {canCompare && (
        <VersionCompare assetId={assetId} versionAId={compareSelection[0]!} versionBId={compareSelection[1]!} />
      )}

      {/* Version table */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">
            All Versions
            {compareSelection[0] !== null || compareSelection[1] !== null ? ' \u2014 Select 2 versions to compare' : ''}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-3">
              {Array.from({ length: 3 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : versions && versions.length > 0 ? (
            <VersionTable
              versions={versions}
              onRevert={handleRevert}
              isReverting={revert.isPending}
              selectedForCompare={compareSelection}
              onToggleCompare={handleToggleCompare}
            />
          ) : (
            <p className="py-8 text-center text-sm text-muted-foreground">No versions found</p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
