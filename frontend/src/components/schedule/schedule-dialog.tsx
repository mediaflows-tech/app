'use client'

import { useEffect, useMemo, useState } from 'react'
import { useAvailableAssets, useSchedule } from '@/hooks/use-schedule'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { DateTimePickerInput } from '@/components/ui/datetime-picker-input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

interface ScheduleDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  prefilledDate: string
  onSuccess: () => void
}

export function ScheduleDialog({ open, onOpenChange, prefilledDate, onSuccess }: ScheduleDialogProps) {
  const [assetId, setAssetId] = useState('')
  const [scheduleDate, setScheduleDate] = useState('')
  const { data: availableAssets, isLoading: assetsLoading } = useAvailableAssets()
  const scheduleMutation = useSchedule()

  const minDateTime = useMemo(() => {
    const now = new Date()
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
    return now.toISOString().slice(0, 16)
  }, [])

  useEffect(() => {
    if (open && prefilledDate) {
      const today = new Date().toISOString().slice(0, 10)
      if (prefilledDate.length === 10 && prefilledDate >= today) {
        setScheduleDate(`${prefilledDate}T09:00`)
      } else if (prefilledDate.length > 10) {
        setScheduleDate(prefilledDate.slice(0, 16))
      }
    }
    if (!open) {
      setAssetId('')
      setScheduleDate('')
    }
  }, [open, prefilledDate])

  const handleSubmit = () => {
    if (!assetId || !scheduleDate) return
    scheduleMutation.mutate(
      {
        assetId: parseInt(assetId),
        scheduledPublishAt: new Date(scheduleDate).toISOString()
      },
      {
        onSuccess: () => {
          setAssetId('')
          setScheduleDate('')
          onSuccess()
        }
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Schedule Publication</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 px-4">
          <div className="space-y-2">
            <Label htmlFor="schedule-asset">Asset</Label>
            <Select value={assetId} onValueChange={(v: string | null) => setAssetId(v ?? '')}>
              <SelectTrigger id="schedule-asset">
                <SelectValue placeholder={assetsLoading ? 'Loading assets...' : 'Select an asset...'} />
              </SelectTrigger>
              <SelectContent>
                {availableAssets && availableAssets.length === 0 && (
                  <SelectItem value="_empty" disabled>
                    No approved assets available
                  </SelectItem>
                )}
                {availableAssets?.map((asset) => (
                  <SelectItem key={asset.id} value={String(asset.id)}>
                    {asset.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Publish Date & Time</Label>
            <DateTimePickerInput value={scheduleDate} min={minDateTime} onChange={setScheduleDate} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={!assetId || !scheduleDate || scheduleMutation.isPending}>
            {scheduleMutation.isPending ? 'Scheduling...' : 'Schedule'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
