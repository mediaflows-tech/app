'use client'

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { ScheduleEventDto, AvailableAssetDto } from '@/types/api'
import { toast } from '@/lib/toast'

export const scheduleKeys = {
  all: ['schedule'] as const,
  events: (start: string, end: string) => ['schedule', 'events', start, end] as const,
  available: ['schedule', 'available'] as const
}

export function useScheduleEvents(start: string, end: string) {
  return useQuery({
    queryKey: scheduleKeys.events(start, end),
    queryFn: () => {
      const params = new URLSearchParams({ start, end })
      return api.get<ScheduleEventDto[]>(`/schedule/events?${params.toString()}`)
    },
    enabled: !!start && !!end
  })
}

export function useAvailableAssets() {
  return useQuery({
    queryKey: scheduleKeys.available,
    queryFn: () => api.get<AvailableAssetDto[]>('/schedule/available')
  })
}

export function useSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetId: number; scheduledPublishAt: string }) => api.post('/schedule', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduleKeys.all })
      toast.success('Asset scheduled for publishing')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to schedule')
    }
  })
}

export function useReschedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetId: number; scheduledPublishAt: string }) =>
      api.put(`/schedule/${data.assetId}`, { scheduledPublishAt: data.scheduledPublishAt }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduleKeys.all })
      toast.success('Event rescheduled')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to reschedule')
    }
  })
}

export function useUnschedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (assetId: number) => api.delete(`/schedule/${assetId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduleKeys.all })
      toast.success('Asset unscheduled')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to unschedule')
    }
  })
}
