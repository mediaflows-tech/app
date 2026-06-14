'use client'

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatRelativeTime } from '@/lib/utils'
import type { CloudWatchAlarmDto } from '@/types/api'

interface AlarmTableProps {
  alarms: CloudWatchAlarmDto[]
}

function alarmStateBadge(state: string) {
  switch (state) {
    case 'ALARM':
      return <Badge variant="destructive">ALARM</Badge>
    case 'OK':
      return <Badge variant="secondary">OK</Badge>
    case 'INSUFFICIENT_DATA':
      return <Badge variant="outline">NO DATA</Badge>
    default:
      return <Badge variant="outline">{state}</Badge>
  }
}

import { formatAlarmName } from '@/lib/format-alarm-name'

export function AlarmTable({ alarms }: AlarmTableProps) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">CloudWatch Alarms</CardTitle>
      </CardHeader>
      <CardContent>
        {alarms.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">No active alarms</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>State</TableHead>
                <TableHead>Metric</TableHead>
                <TableHead className="text-right">Updated</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {alarms.map((alarm) => (
                <TableRow key={alarm.alarmName}>
                  <TableCell className="text-sm font-medium" title={alarm.alarmName}>
                    {formatAlarmName(alarm.alarmName)}
                  </TableCell>
                  <TableCell>{alarmStateBadge(alarm.stateValue)}</TableCell>
                  <TableCell className="max-w-[300px] truncate text-sm text-muted-foreground">
                    {alarm.metricName}
                  </TableCell>
                  <TableCell className="text-right text-sm text-muted-foreground">
                    {formatRelativeTime(alarm.stateUpdatedTimestamp)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}
