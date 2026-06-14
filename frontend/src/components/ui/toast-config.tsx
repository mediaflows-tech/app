'use client'

import toast from 'react-hot-toast'
import { Circle, CircleCheck, CircleX, CircleAlert, Loader2 } from 'lucide-react'
import { notificationStore } from '@/lib/notification-store'

const iconClass = 'h-4 w-4 shrink-0'

export const notify = {
  success(message: string) {
    notificationStore.push('Success', message, 'success')
    return toast.success(message, {
      icon: <CircleCheck className={`${iconClass} text-green-500`} />,
      duration: 2500
    })
  },

  error(message: string) {
    notificationStore.push('Error', message, 'error')
    return toast.error(message, {
      icon: <CircleX className={`${iconClass} text-red-500`} />,
      duration: 4000
    })
  },

  warning(message: string) {
    notificationStore.push('Warning', message, 'warning')
    return toast(message, {
      icon: <CircleAlert className={`${iconClass} text-amber-500`} />,
      duration: 2500
    })
  },

  info(message: string) {
    return toast(message, {
      icon: <Circle className={`${iconClass} text-blue-500`} />,
      duration: 2500
    })
  },

  loading(message: string) {
    return toast.loading(message, {
      icon: <Loader2 className={`${iconClass} text-muted-foreground animate-spin`} />
    })
  },

  /** Push to notification store only — no visible toast */
  silent(title: string, description: string, level: 'success' | 'error' | 'warning' | 'info' = 'info') {
    notificationStore.push(title, description, level)
  },

  dismiss(id?: string) {
    toast.dismiss(id)
  }
}
