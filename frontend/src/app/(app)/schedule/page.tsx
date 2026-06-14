import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/page-header'
import { PublishingCalendar } from '@/components/schedule/publishing-calendar'

export const metadata: Metadata = {
  title: 'Publishing Schedule'
}

export default function SchedulePage() {
  return (
    <div className="space-y-8">
      <PageHeader title="Publishing Schedule" description="Schedule approved assets for publication" />
      <PublishingCalendar />
    </div>
  )
}
