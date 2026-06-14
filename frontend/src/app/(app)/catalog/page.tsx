import { CatalogView } from './catalog-view'
import { PageHeader } from '@/components/shared/page-header'

export const metadata = {
  title: 'Catalog | MediaFlows'
}

export default async function CatalogPage({
  searchParams
}: {
  searchParams: Promise<{ type?: string; sort?: string }>
}) {
  const params = await searchParams

  return (
    <div className="space-y-6">
      <PageHeader title="Catalog" description="Browse published media assets" />
      <CatalogView initialType={params.type} initialSort={params.sort} />
    </div>
  )
}
