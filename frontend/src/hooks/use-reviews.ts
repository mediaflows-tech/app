'use client'

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type {
  PagedResult,
  ReviewListItemDto,
  ReviewDetailsDto,
  ReviewDetailsApiResponse,
  ReviewDecision
} from '@/types/api'
import { toast } from '@/lib/toast'
import { toReviewDetails } from '@/lib/review-mappers'

export const reviewKeys = {
  all: ['reviews'] as const,
  list: (filters: ReviewFilters) => ['reviews', 'list', filters] as const,
  detail: (id: number) => ['reviews', 'detail', id] as const
}

export interface ReviewFilters {
  page?: number
  status?: string
  contentType?: string
  sortDir?: string
}

function buildReviewParams(filters: ReviewFilters): string {
  const params = new URLSearchParams()
  params.set('page', String(filters.page ?? 1))
  if (filters.status) params.set('status', filters.status)
  if (filters.contentType) params.set('contentType', filters.contentType)
  params.set('sortDir', filters.sortDir ?? 'desc')
  return params.toString()
}

export interface ReviewPagedResult extends PagedResult<ReviewListItemDto> {
  counts?: {
    pending: number
    approved: number
    rejected: number
  }
}

// The API returns a PagedResult<ReviewListItemDto> with a `counts` block
export function useReviews(filters: ReviewFilters = {}) {
  return useQuery({
    queryKey: reviewKeys.list(filters),
    queryFn: () => api.get<ReviewPagedResult>(`/reviews?${buildReviewParams(filters)}`)
  })
}

export function useReviewDetail(id: number) {
  return useQuery({
    queryKey: reviewKeys.detail(id),
    queryFn: async (): Promise<ReviewDetailsDto> => {
      const raw = await api.get<ReviewDetailsApiResponse>(`/reviews/${id}`)
      return toReviewDetails(raw, id)
    },
    enabled: id > 0
  })
}

export function useDecide() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      assetId,
      ...body
    }: {
      assetId: number
      decision: ReviewDecision
      comments?: string
      publishImmediately?: boolean
      scheduledPublishAt?: string
    }) => api.post(`/reviews/${assetId}/decide`, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      toast.success('Review decision submitted')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to submit review')
    }
  })
}

export function usePublishNow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (assetId: number) => api.post(`/reviews/${assetId}/publish`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      toast.success('Asset published immediately')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to publish')
    }
  })
}

export function useRejectApproved() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetId: number; comments: string }) =>
      api.post(`/reviews/${data.assetId}/reject`, { comments: data.comments }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      toast.success('Asset rejected')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Failed to reject')
    }
  })
}

export function useBatchDecide() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetIds: number[]; decision: ReviewDecision; comments?: string }) =>
      api.post('/reviews/batch/decide', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      toast.success('Batch review completed')
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Batch review failed')
    }
  })
}

export function useBatchPublish() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetIds: number[]; comments?: string }) =>
      api.post<{ count: number; skipped: number }>('/reviews/batch/publish', data),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      if (data.skipped > 0) {
        toast.warning(`Published ${data.count} asset(s). ${data.skipped} skipped (ineligible).`)
      } else {
        toast.success(`${data.count} asset(s) approved and published`)
      }
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Batch publish failed')
    }
  })
}

export function useBatchApproveAndSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { assetIds: number[]; scheduledPublishAt: string }) =>
      api.post<{ count: number; skipped: number }>('/reviews/batch/approve-schedule', data),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      if (data.skipped > 0) {
        toast.warning(`Scheduled ${data.count} asset(s). ${data.skipped} skipped (ineligible).`)
      } else {
        toast.success(`${data.count} asset(s) approved and scheduled`)
      }
    },
    onError: (error: Error) => {
      toast.error(error.message || 'Batch approve & schedule failed')
    }
  })
}
