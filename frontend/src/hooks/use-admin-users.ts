'use client'

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { CognitoUserDto } from '@/types/api'
import { toast } from '@/lib/toast'

const USERS_KEY = ['admin-users'] as const

export function useUsers(role?: string) {
  const params = role && role !== 'All' ? `?role=${role}` : ''
  return useQuery({
    queryKey: [...USERS_KEY, role ?? 'All'],
    queryFn: () => api.get<CognitoUserDto[]>(`/admin/users${params}`)
  })
}

export function useUser(id: string) {
  return useQuery({
    queryKey: [...USERS_KEY, id],
    queryFn: () => api.get<CognitoUserDto>(`/admin/users/${id}`),
    enabled: !!id
  })
}

export function useCreateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { email: string; displayName: string; role: string; temporaryPassword: string }) =>
      api.post<CognitoUserDto>('/admin/users', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_KEY })
      toast.success('User created successfully')
    },
    onError: (error: Error) => {
      toast.error(`Failed to create user: ${error.message}`)
    }
  })
}

export function useUpdateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: { displayName?: string; role?: string } }) =>
      api.put<CognitoUserDto>(`/admin/users/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_KEY })
      toast.success('User updated successfully')
    },
    onError: (error: Error) => {
      toast.error(`Failed to update user: ${error.message}`)
    }
  })
}

export function useDisableUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<void>(`/admin/users/${id}/disable`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_KEY })
      toast.success('User disabled')
    },
    onError: (error: Error) => {
      toast.error(`Failed to disable user: ${error.message}`)
    }
  })
}

export function useEnableUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<void>(`/admin/users/${id}/enable`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_KEY })
      toast.success('User enabled')
    },
    onError: (error: Error) => {
      toast.error(`Failed to enable user: ${error.message}`)
    }
  })
}

export function useDeleteUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/users/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_KEY })
      toast.success('User deleted')
    },
    onError: (error: Error) => {
      toast.error(`Failed to delete user: ${error.message}`)
    }
  })
}
