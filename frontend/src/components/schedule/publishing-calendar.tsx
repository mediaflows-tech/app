'use client'

import { useCallback, useRef, useState } from 'react'
import dynamic from 'next/dynamic'
import type { DateClickArg } from '@fullcalendar/interaction'
import type { DatesSetArg, EventClickArg, EventDropArg } from '@fullcalendar/core'
import { useScheduleEvents, useReschedule, useUnschedule } from '@/hooks/use-schedule'
import { ScheduleDialog } from './schedule-dialog'
import { Card } from '@/components/ui/card'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog'
import { useRouter } from 'next/navigation'

// Dynamic import to prevent SSR issues with FullCalendar
const FullCalendar = dynamic(() => import('./calendar-inner'), { ssr: false })

interface SelectedEvent {
  id: string
  title: string
  start: string
  assetId: number
  thumbnailUrl: string
  status: string
}

export function PublishingCalendar() {
  const router = useRouter()
  const [dateRange, setDateRange] = useState({ start: '', end: '' })
  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false)
  const [scheduleDialogDate, setScheduleDialogDate] = useState('')
  const [eventDetailOpen, setEventDetailOpen] = useState(false)
  const [selectedEvent, setSelectedEvent] = useState<SelectedEvent | null>(null)

  const { data: events } = useScheduleEvents(dateRange.start, dateRange.end)
  const reschedule = useReschedule()
  const unscheduleAsset = useUnschedule()

  const handleDatesSet = useCallback((arg: DatesSetArg) => {
    setDateRange({ start: arg.startStr, end: arg.endStr })
  }, [])

  const handleDateClick = useCallback((info: DateClickArg) => {
    const clicked = new Date(info.dateStr)
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    if (clicked < today) return
    setScheduleDialogDate(info.dateStr)
    setScheduleDialogOpen(true)
  }, [])

  const handleEventDrop = useCallback(
    (info: EventDropArg) => {
      const assetId = parseInt(info.event.extendedProps.assetId)
      const newStart = info.event.start?.toISOString()
      if (!newStart) return
      reschedule.mutate({ assetId, scheduledPublishAt: newStart }, { onError: () => info.revert() })
    },
    [reschedule]
  )

  const handleEventClick = useCallback((info: EventClickArg) => {
    const props = info.event.extendedProps
    setSelectedEvent({
      id: info.event.id,
      title: info.event.title,
      start: info.event.start?.toLocaleString() ?? 'Not set',
      assetId: parseInt(props.assetId),
      thumbnailUrl: props.thumbnailUrl,
      status: props.status
    })
    setEventDetailOpen(true)
  }, [])

  const eventAllow = useCallback((dropInfo: { start: Date }) => {
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    return dropInfo.start >= today
  }, [])

  const dayCellClassNames = useCallback((arg: { date: Date }) => {
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    if (arg.date < today) return ['fc-day-past']
    return []
  }, [])

  const handleUnschedule = () => {
    if (!selectedEvent) return
    unscheduleAsset.mutate(selectedEvent.assetId, {
      onSuccess: () => {
        setEventDetailOpen(false)
        setSelectedEvent(null)
      }
    })
  }

  return (
    <>
      <Card className="p-4">
        <FullCalendar
          events={events ?? []}
          onDatesSet={handleDatesSet}
          onDateClick={handleDateClick}
          onEventDrop={handleEventDrop}
          onEventClick={handleEventClick}
          eventAllow={eventAllow}
          dayCellClassNames={dayCellClassNames}
        />
      </Card>

      <ScheduleDialog
        open={scheduleDialogOpen}
        onOpenChange={setScheduleDialogOpen}
        prefilledDate={scheduleDialogDate}
        onSuccess={() => setScheduleDialogOpen(false)}
      />

      <AlertDialog open={eventDetailOpen} onOpenChange={setEventDetailOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{selectedEvent?.title}</AlertDialogTitle>
            <AlertDialogDescription>
              {selectedEvent?.thumbnailUrl && (
                <span className="mb-3 block text-center">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={selectedEvent.thumbnailUrl} alt="Thumbnail" className="mx-auto max-h-[200px] rounded-md" />
                </span>
              )}
              <span className="block">
                <strong>Status:</strong> {selectedEvent?.status}
              </span>
              <span className="block">
                <strong>Scheduled:</strong> {selectedEvent?.start}
              </span>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter className="flex gap-2 sm:justify-between">
            <AlertDialogCancel>Close</AlertDialogCancel>
            <div className="flex gap-2">
              <AlertDialogAction onClick={() => router.push(`/review/${selectedEvent?.assetId}`)}>
                View Details
              </AlertDialogAction>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={handleUnschedule}
                disabled={unscheduleAsset.isPending}
              >
                {unscheduleAsset.isPending ? 'Unscheduling...' : 'Unschedule'}
              </AlertDialogAction>
            </div>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
