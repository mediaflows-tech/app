'use client'

import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import interactionPlugin from '@fullcalendar/interaction'
import type { DateClickArg } from '@fullcalendar/interaction'
import type { DatesSetArg, EventClickArg, EventDropArg, EventInput } from '@fullcalendar/core'

interface CalendarInnerProps {
  events: EventInput[]
  onDatesSet: (arg: DatesSetArg) => void
  onDateClick: (arg: DateClickArg) => void
  onEventDrop: (arg: EventDropArg) => void
  onEventClick: (arg: EventClickArg) => void
  eventAllow: (dropInfo: { start: Date }) => boolean
  dayCellClassNames: (arg: { date: Date }) => string[]
}

export default function CalendarInner({
  events,
  onDatesSet,
  onDateClick,
  onEventDrop,
  onEventClick,
  eventAllow,
  dayCellClassNames
}: CalendarInnerProps) {
  return (
    <FullCalendar
      plugins={[dayGridPlugin, timeGridPlugin, interactionPlugin]}
      initialView="dayGridMonth"
      headerToolbar={{
        left: 'prev,next today',
        center: 'title',
        right: 'dayGridMonth,timeGridWeek'
      }}
      events={events}
      editable
      selectable
      datesSet={onDatesSet}
      dateClick={onDateClick}
      eventDrop={onEventDrop}
      eventClick={onEventClick}
      eventAllow={eventAllow}
      dayCellClassNames={dayCellClassNames}
      timeZone="local"
      height="auto"
    />
  )
}
