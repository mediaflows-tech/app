'use client'

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog'
import { formatBytes, formatDate } from '@/lib/utils'
import { RotateCcw, GitCompare } from 'lucide-react'
import { useState } from 'react'
import type { AssetVersionDto } from '@/hooks/use-versions'

interface VersionTableProps {
  versions: AssetVersionDto[]
  onRevert: (versionId: number) => void
  isReverting: boolean
  selectedForCompare: [number | null, number | null]
  onToggleCompare: (versionId: number) => void
}

export function VersionTable({
  versions,
  onRevert,
  isReverting,
  selectedForCompare,
  onToggleCompare
}: VersionTableProps) {
  const [revertId, setRevertId] = useState<number | null>(null)

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[80px]">Version</TableHead>
            <TableHead>Notes</TableHead>
            <TableHead>Size</TableHead>
            <TableHead>Date</TableHead>
            <TableHead className="w-[140px] text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {versions.map((version) => {
            const isSelectedForCompare = selectedForCompare[0] === version.id || selectedForCompare[1] === version.id

            return (
              <TableRow key={version.id}>
                <TableCell>
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-sm">v{version.versionNumber}</span>
                    {version.isCurrent && (
                      <Badge variant="secondary" className="text-xs">
                        Current
                      </Badge>
                    )}
                  </div>
                </TableCell>
                <TableCell className="max-w-[200px] truncate text-sm text-muted-foreground">
                  {version.notes || '\u2014'}
                </TableCell>
                <TableCell className="text-sm">{formatBytes(version.fileSize)}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{formatDate(version.createdAt, 'long')}</TableCell>
                <TableCell className="text-right">
                  <div className="flex items-center justify-end gap-1">
                    {versions.length > 1 && (
                      <Button
                        variant={isSelectedForCompare ? 'secondary' : 'ghost'}
                        size="icon"
                        className="h-7 w-7"
                        onClick={() => onToggleCompare(version.id)}
                        title="Select for comparison"
                      >
                        <GitCompare className="h-3.5 w-3.5" />
                      </Button>
                    )}
                    {!version.isCurrent && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7"
                        onClick={() => setRevertId(version.id)}
                        disabled={isReverting}
                        title="Revert to this version"
                      >
                        <RotateCcw className="h-3.5 w-3.5" />
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>

      {/* Revert confirmation */}
      <AlertDialog
        open={revertId !== null}
        onOpenChange={(open) => {
          if (!open) setRevertId(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Revert to this version?</AlertDialogTitle>
            <AlertDialogDescription>
              This will create a new version based on the selected version. The current version will be preserved in
              history.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (revertId !== null) {
                  onRevert(revertId)
                  setRevertId(null)
                }
              }}
            >
              Revert
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
