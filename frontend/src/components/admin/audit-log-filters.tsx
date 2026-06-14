'use client'

import { useEffect, useRef, useState } from 'react'
import { Search } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Label } from '@/components/ui/label'
import { DatePickerInput } from '@/components/ui/date-picker-input'

interface AuditLogFiltersProps {
  filters: { query?: string; actionType?: string; from?: string; to?: string }
  onFilterChange: (filters: { query?: string; actionType?: string; from?: string; to?: string }) => void
  actionTypes?: string[]
}

export function AuditLogFilters({ filters, onFilterChange, actionTypes }: AuditLogFiltersProps) {
  const availableTypes = actionTypes && actionTypes.length > 0 ? actionTypes : []

  // Local search state for debouncing — all other filters fire immediately
  const [search, setSearch] = useState(filters.query ?? '')
  const cbRef = useRef(onFilterChange)
  cbRef.current = onFilterChange
  const filtersRef = useRef(filters)
  filtersRef.current = filters

  useEffect(() => {
    const timer = setTimeout(() => {
      if (search !== (filtersRef.current.query ?? '')) {
        cbRef.current({ ...filtersRef.current, query: search || undefined })
      }
    }, 300)
    return () => clearTimeout(timer)
  }, [search])

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
      <div className="relative flex-1">
        <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search logs..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>

      <Select
        value={filters.actionType ?? 'all'}
        onValueChange={(v) =>
          onFilterChange({ ...filters, actionType: (v ?? 'all') === 'all' ? undefined : (v ?? undefined) })
        }
      >
        <SelectTrigger className="w-full truncate sm:w-44">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All Actions</SelectItem>
          {availableTypes.map((type) => (
            <SelectItem key={type} value={type}>
              {type.replace(/\./g, ' ')}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <div className="flex gap-2">
        <div className="space-y-1">
          <Label className="text-xs text-muted-foreground">From</Label>
          <DatePickerInput
            value={filters.from}
            onChange={(v) => onFilterChange({ ...filters, from: v })}
            placeholder="Start date"
            className="w-44"
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs text-muted-foreground">To</Label>
          <DatePickerInput
            value={filters.to}
            onChange={(v) => onFilterChange({ ...filters, to: v })}
            placeholder="End date"
            className="w-44"
          />
        </div>
      </div>
    </div>
  )
}
