import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr'

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''

export function createNotificationConnection(accessToken: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/notifications`, {
      accessTokenFactory: () => accessToken
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()
}

export function createAnalyticsConnection(accessToken: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/analytics`, {
      accessTokenFactory: () => accessToken
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()
}
