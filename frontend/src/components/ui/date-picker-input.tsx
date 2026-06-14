'use client'

import * as React from 'react'
import { CalendarIcon } from 'lucide-react'
import { parseDate, type CalendarDate, type DateValue } from '@internationalized/date'

import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar-rac'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

function toCalendarDate(dateStr: string | undefined): CalendarDate | undefined {
  if (!dateStr) return undefined
  try {
    return parseDate(dateStr)
  } catch {
    return undefined
  }
}

function formatDisplay(dateStr: string | undefined): string {
  if (!dateStr) return ''
  const d = new Date(dateStr + 'T00:00:00')
  if (isNaN(d.getTime())) return dateStr
  return d.toLocaleDateString('en-US', { day: '2-digit', month: 'long', year: 'numeric' })
}

function toISODate(date: DateValue): string {
  const y = String(date.year).padStart(4, '0')
  const m = String(date.month).padStart(2, '0')
  const d = String(date.day).padStart(2, '0')
  return `${y}-${m}-${d}`
}

interface DatePickerInputProps {
  value?: string // YYYY-MM-DD
  onChange: (value: string | undefined) => void
  placeholder?: string
  className?: string
}

export function DatePickerInput({ value, onChange, placeholder = 'Select date', className }: DatePickerInputProps) {
  const [open, setOpen] = React.useState(false)
  const [inputValue, setInputValue] = React.useState(formatDisplay(value))
  const calendarDate = toCalendarDate(value)
  const [month, setMonth] = React.useState<CalendarDate | undefined>(calendarDate)

  // Sync display when external value changes
  React.useEffect(() => {
    setInputValue(formatDisplay(value))
    const cd = toCalendarDate(value)
    if (cd) setMonth(cd)
  }, [value])

  return (
    <div className={className}>
      <div className="relative">
        <Input
          value={inputValue}
          placeholder={placeholder}
          className="bg-background pr-10"
          onChange={(e) => {
            setInputValue(e.target.value)
            const d = new Date(e.target.value)
            if (!isNaN(d.getTime())) {
              const iso = d.toISOString().slice(0, 10)
              onChange(iso)
            } else if (e.target.value === '') {
              onChange(undefined)
            }
          }}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') {
              e.preventDefault()
              setOpen(true)
            }
          }}
        />
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger
            render={
              <Button
                variant="ghost"
                className="absolute top-1/2 right-2 size-6 -translate-y-1/2 p-0 hover:bg-transparent focus-visible:ring-0 focus-visible:ring-offset-0"
              />
            }
          >
            <CalendarIcon className="size-3.5 text-muted-foreground" />
            <span className="sr-only">Select date</span>
          </PopoverTrigger>
          <PopoverContent align="end" sideOffset={10} className="w-auto p-3">
            <Calendar
              value={calendarDate ?? null}
              focusedValue={month}
              onFocusChange={setMonth}
              onChange={(date) => {
                if (date) {
                  const iso = toISODate(date)
                  onChange(iso)
                  setInputValue(formatDisplay(iso))
                }
                setOpen(false)
              }}
            />
          </PopoverContent>
        </Popover>
      </div>
    </div>
  )
}
