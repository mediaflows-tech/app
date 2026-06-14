'use client'

import * as React from 'react'
import { DatePickerInput } from '@/components/ui/date-picker-input'
import { TimePickerInput } from '@/components/ui/time-picker-input'

interface DateTimePickerInputProps {
  value?: string // "YYYY-MM-DDThh:mm" or "YYYY-MM-DD"
  min?: string
  onChange: (value: string) => void
  className?: string
}

export function DateTimePickerInput({ value, min, onChange, className }: DateTimePickerInputProps) {
  const datePart = value ? value.slice(0, 10) : undefined
  const timePart = value && value.length > 10 ? value.slice(11, 16) : '09:00'

  return (
    <div className={`flex gap-2 ${className ?? ''}`}>
      <DatePickerInput
        value={datePart}
        onChange={(d) => onChange(d ? `${d}T${timePart}` : '')}
        placeholder="Select date"
        className="flex-1"
      />
      <TimePickerInput
        value={timePart}
        onChange={(t) => {
          if (datePart) onChange(`${datePart}T${t}`)
        }}
        className="w-36"
      />
    </div>
  )
}
