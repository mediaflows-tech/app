'use client'

import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type {
  AdminDashboardViewModelDto,
  AdminSummaryDto,
  ActivityDataPoint,
  StorageByType,
  CloudWatchAlarmDto
} from '@/types/api'

// The API returns AdminDashboardViewModelDto; we normalize it into the shape components expect
export function useSummary() {
  return useQuery({
    queryKey: ['admin-summary'],
    queryFn: async (): Promise<AdminSummaryDto> => {
      const raw = await api.get<AdminDashboardViewModelDto>('/admin/summary')

      // Map storageTypeLabels + storageTypeData into StorageByType[]
      const storageByType: StorageByType[] = (raw.storageTypeLabels ?? []).map((label, i) => ({
        name: label,
        value: raw.storageTypeData?.[i] ?? 0
      }))

      // Map activeAlarms from the response
      const alarms: CloudWatchAlarmDto[] = raw.activeAlarms ?? []

      return {
        totalUsers: raw.totalUsers ?? 0,
        totalAssets: raw.totalAssets ?? 0,
        pendingReviews: raw.pendingReviews ?? 0,
        // The API returns storageUsedFormatted (string) — store it for display;
        // set bytes to 0 since the API doesn't provide raw bytes
        storageUsedBytes: 0,
        storageUsedFormatted: raw.storageUsedFormatted ?? '0 B',
        storageLimitBytes: 0,
        storageByType,
        alarms,
        // Carry through activity data for the chart
        activityLabels: raw.activityLabels ?? [],
        activityData: raw.activityData ?? [],
        systemHealth: raw.systemHealth ?? 'Unknown'
      }
    },
    refetchInterval: 60_000
  })
}

export function useActivityChart(days: number = 7) {
  return useQuery({
    queryKey: ['admin-summary', 'activity', days],
    queryFn: async (): Promise<ActivityDataPoint[]> => {
      try {
        const raw = await api.get<{ labels: string[]; data: number[] }>(`/admin/summary/activity?days=${days}`)
        const labels = raw?.labels ?? []
        const dataPoints = raw?.data ?? []
        return labels.map((label, i) => ({
          date: label,
          uploads: dataPoints[i] ?? 0,
          reviews: 0
        }))
      } catch {
        // If the dedicated endpoint doesn't exist, return empty so fallback data is used
        return []
      }
    },
    placeholderData: []
  })
}
