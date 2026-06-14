import { Suspense } from 'react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Upload } from 'lucide-react'
import Link from 'next/link'
import { AssetLibrary } from '@/components/assets/asset-library'

export default function AssetsPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Asset Library</h1>
          <p className="text-sm text-muted-foreground">Manage your uploaded media files</p>
        </div>
        <Button render={<Link href="/creator/upload" />}>
          <Upload className="mr-2 h-4 w-4" />
          Upload
        </Button>
      </div>

      <Suspense
        fallback={
          <div className="space-y-4">
            <Skeleton className="h-10 w-full" />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {Array.from({ length: 8 }).map((_, i) => (
                <Skeleton key={i} className="aspect-video w-full rounded-lg" />
              ))}
            </div>
          </div>
        }
      >
        <AssetLibrary />
      </Suspense>
    </div>
  )
}
