// Shim: redirects sonner-style toast calls to react-hot-toast via notify
// Accepts optional second arg for backward compat but ignores options
import { notify } from '@/components/ui/toast-config'

/* eslint-disable @typescript-eslint/no-explicit-any */
export const toast = {
  success: (message: string, _opts?: any) => notify.success(message),
  error: (message: string, _opts?: any) => notify.error(message),
  warning: (message: string, _opts?: any) => notify.warning(message),
  info: (message: string, _opts?: any) => notify.info(message),
  loading: (message: string, _opts?: any) => notify.loading(message),
  dismiss: (id?: string) => notify.dismiss(id)
}
