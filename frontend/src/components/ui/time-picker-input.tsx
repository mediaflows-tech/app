'use client'

import * as React from 'react'
import { Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'

const HOURS = Array.from({ length: 12 }, (_, i) => i + 1)
const MINUTES = Array.from({ length: 12 }, (_, i) => i * 5)

function to12Hour(time24: string): { hour: number; minute: number; period: 'AM' | 'PM' } {
  const [h, m] = time24.split(':').map(Number)
  const period: 'AM' | 'PM' = h >= 12 ? 'PM' : 'AM'
  const hour = h === 0 ? 12 : h > 12 ? h - 12 : h
  return { hour, minute: m, period }
}

function to24Hour(hour: number, minute: number, period: 'AM' | 'PM'): string {
  let h = hour
  if (period === 'AM' && h === 12) h = 0
  else if (period === 'PM' && h !== 12) h += 12
  return `${String(h).padStart(2, '0')}:${String(minute).padStart(2, '0')}`
}

function formatDisplay(time24: string): string {
  const { hour, minute, period } = to12Hour(time24)
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')} ${period}`
}

interface TimePickerInputProps {
  value: string // "HH:mm" 24-hour
  onChange: (value: string) => void
  className?: string
  min?: string
}

export function TimePickerInput({ value, onChange, className, min }: TimePickerInputProps) {
  const [open, setOpen] = React.useState(false)
  const { hour, minute, period } = to12Hour(value)

  const selectTime = (h: number, m: number, p: 'AM' | 'PM') => {
    onChange(to24Hour(h, m, p))
  }

  return (
    <div className={className}>
      <div className="relative">
        <Input
          value={formatDisplay(value)}
          readOnly
          className="bg-background pr-10 cursor-pointer"
          onClick={() => setOpen(true)}
          min={min}
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
            <Clock className="size-3.5 text-muted-foreground" />
            <span className="sr-only">Select time</span>
          </PopoverTrigger>
          <PopoverContent align="end" sideOffset={10} className="w-auto p-0">
            <div className="flex divide-x divide-border/30">
              {/* Hours */}
              <div className="h-56 w-16 overflow-y-auto scrollbar-none">
                <div className="p-1">
                  {HOURS.map((h) => (
                    <button
                      key={h}
                      className={cn(
                        'flex w-full items-center justify-center rounded-md py-1.5 text-sm transition-colors hover:bg-accent',
                        h === hour && 'bg-primary text-primary-foreground hover:bg-primary/90'
                      )}
                      onClick={() => selectTime(h, minute, period)}
                    >
                      {String(h).padStart(2, '0')}
                    </button>
                  ))}
                </div>
              </div>

              {/* Minutes */}
              <div className="h-56 w-16 overflow-y-auto scrollbar-none">
                <div className="p-1">
                  {MINUTES.map((m) => (
                    <button
                      key={m}
                      className={cn(
                        'flex w-full items-center justify-center rounded-md py-1.5 text-sm transition-colors hover:bg-accent',
                        m === minute && 'bg-primary text-primary-foreground hover:bg-primary/90'
                      )}
                      onClick={() => selectTime(hour, m, period)}
                    >
                      {String(m).padStart(2, '0')}
                    </button>
                  ))}
                </div>
              </div>

              {/* AM/PM */}
              <div className="flex flex-col gap-1 p-1">
                {(['AM', 'PM'] as const).map((p) => (
                  <button
                    key={p}
                    className={cn(
                      'flex w-14 flex-1 items-center justify-center rounded-md text-sm font-medium transition-colors hover:bg-accent',
                      p === period && 'bg-primary text-primary-foreground hover:bg-primary/90'
                    )}
                    onClick={() => selectTime(hour, minute, p)}
                  >
                    {p}
                  </button>
                ))}
              </div>
            </div>
          </PopoverContent>
        </Popover>
      </div>
    </div>
  )
}
