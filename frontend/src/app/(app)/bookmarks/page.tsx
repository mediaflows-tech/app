import { BookmarksView } from './bookmarks-view'
import { PageHeader } from '@/components/shared/page-header'

export const metadata = {
  title: 'Bookmarks | MediaFlows'
}

export default function BookmarksPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Bookmarks" description="Your saved assets" />
      <BookmarksView />
    </div>
  )
}
