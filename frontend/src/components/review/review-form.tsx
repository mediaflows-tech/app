'use client'

import { useState } from 'react'
import { useDecide, usePublishNow, useRejectApproved } from '@/hooks/use-reviews'
import { useSchedule, useUnschedule } from '@/hooks/use-schedule'
import type { AssetStatus } from '@/types/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { StatusBadge } from '@/components/ui/status-badge'
import { Label } from '@/components/ui/label'
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
import { RejectConfirmation } from './reject-confirmation'
import { PublishNowConfirmDialog } from './publish-now-confirm-dialog'
import { ApprovePublishConfirmDialog } from './approve-publish-confirm-dialog'
import { useRouter } from 'next/navigation'
import { Check, CalendarCheck, Pencil, X, Clock, XCircle } from 'lucide-react'
import { formatDate, getMinDateTime } from '@/lib/utils'

interface ReviewFormProps {
  assetId: number
  status: AssetStatus
  scheduledPublishAt: string | null
  canReview: boolean
  canSchedule: boolean
  isScheduled: boolean
}

export function ReviewForm({
  assetId,
  status,
  scheduledPublishAt,
  canReview,
  canSchedule,
  isScheduled
}: ReviewFormProps) {
  const router = useRouter()
  const decide = useDecide()
  const publishNow = usePublishNow()
  const rejectApproved = useRejectApproved()
  const schedule = useSchedule()
  const unschedule = useUnschedule()

  const [comments, setComments] = useState('')
  const [showSchedulePanel, setShowSchedulePanel] = useState(false)
  const [scheduleDate, setScheduleDate] = useState('')
  const [showRejectForm, setShowRejectForm] = useState(false)
  const [rejectComments, setRejectComments] = useState('')
  const [commentError, setCommentError] = useState(false)
  const [publishConfirmOpen, setPublishConfirmOpen] = useState(false)
  const [unscheduleConfirmOpen, setUnscheduleConfirmOpen] = useState(false)
  const [approvePublishConfirmOpen, setApprovePublishConfirmOpen] = useState(false)

  const minDateTime = getMinDateTime()

  const navigateToQueue = () => router.push('/review')

  // ── Path 1: Can review (PendingReview / Submitted) ──
  if (canReview) {
    const handleApprovePublish = () => {
      decide.mutate(
        {
          assetId,
          decision: 'Approved',
          comments: comments || undefined,
          publishImmediately: true
        },
        { onSuccess: navigateToQueue }
      )
    }

    const handleScheduleNow = () => {
      if (!scheduleDate) return
      decide.mutate(
        {
          assetId,
          decision: 'Approved',
          comments: comments || undefined,
          scheduledPublishAt: new Date(scheduleDate).toISOString()
        },
        { onSuccess: navigateToQueue }
      )
    }

    const handleScheduleLater = () => {
      decide.mutate(
        {
          assetId,
          decision: 'Approved',
          comments: comments || undefined,
          publishImmediately: false
        },
        { onSuccess: navigateToQueue }
      )
    }

    const handleDecisionRequiringComment = (decision: 'ChangesRequested' | 'Rejected') => {
      if (!comments.trim()) {
        setCommentError(true)
        return
      }
      decide.mutate({ assetId, decision, comments }, { onSuccess: navigateToQueue })
    }

    return (
      <Card>
        <CardHeader>
          <CardTitle>Your Review</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="review-comments">Comments</Label>
            <Textarea
              id="review-comments"
              placeholder="Add review comments..."
              maxLength={2000}
              rows={4}
              value={comments}
              onChange={(e) => {
                setComments(e.target.value)
                setCommentError(false)
              }}
              className={commentError ? 'border-destructive' : ''}
            />
            <p className="text-xs text-muted-foreground">{comments.length}/2000</p>
            {commentError && <p className="text-xs text-destructive">A comment is required for this action.</p>}
          </div>

          <div className="flex flex-wrap gap-2">
            <Button onClick={() => setApprovePublishConfirmOpen(true)} disabled={decide.isPending || showSchedulePanel}>
              <Check className="mr-1 h-4 w-4" />
              Approve & Publish
            </Button>
            <Button variant="outline" onClick={() => setShowSchedulePanel(true)} disabled={showSchedulePanel}>
              <CalendarCheck className="mr-1 h-4 w-4" />
              Approve & Schedule
            </Button>
            <Button
              variant="secondary"
              onClick={() => handleDecisionRequiringComment('ChangesRequested')}
              disabled={decide.isPending}
            >
              <Pencil className="mr-1 h-4 w-4" />
              Request Changes
            </Button>
            <Button
              variant="outline"
              onClick={() => handleDecisionRequiringComment('Rejected')}
              disabled={decide.isPending}
            >
              <X className="mr-1 h-4 w-4" />
              Reject
            </Button>
          </div>

          {showSchedulePanel && (
            <Card className="bg-muted/50">
              <CardContent className="space-y-3">
                <p className="text-sm font-medium">How would you like to publish this asset?</p>
                <div className="space-y-2">
                  <Label htmlFor="schedule-dt">Publish Date & Time</Label>
                  <Input
                    id="schedule-dt"
                    type="datetime-local"
                    min={minDateTime}
                    value={scheduleDate}
                    onChange={(e) => setScheduleDate(e.target.value)}
                  />
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" onClick={handleScheduleNow} disabled={!scheduleDate || decide.isPending}>
                    <CalendarCheck className="mr-1 h-4 w-4" />
                    Schedule Now
                  </Button>
                  <Button size="sm" variant="outline" onClick={handleScheduleLater} disabled={decide.isPending}>
                    <Clock className="mr-1 h-4 w-4" />
                    Schedule Later
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      setShowSchedulePanel(false)
                      setScheduleDate('')
                    }}
                  >
                    Cancel
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">
                  <strong>Schedule Now</strong> -- approve and set the publish date now.
                  <br />
                  <strong>Schedule Later</strong> -- approve only; schedule from the calendar page later.
                </p>
              </CardContent>
            </Card>
          )}
        </CardContent>

        <ApprovePublishConfirmDialog
          open={approvePublishConfirmOpen}
          onOpenChange={setApprovePublishConfirmOpen}
          isSubmitting={decide.isPending}
          onConfirm={handleApprovePublish}
        />
      </Card>
    )
  }

  // ── Path 2: Can schedule (Approved, no date) ──
  if (canSchedule) {
    const handlePublishNow = () => {
      publishNow.mutate(assetId, { onSuccess: navigateToQueue })
    }

    const handleReject = () => {
      if (!rejectComments.trim()) return
      rejectApproved.mutate({ assetId, comments: rejectComments }, { onSuccess: navigateToQueue })
    }

    const handleSchedule = () => {
      if (!scheduleDate) return
      schedule.mutate(
        {
          assetId,
          scheduledPublishAt: new Date(scheduleDate).toISOString()
        },
        { onSuccess: navigateToQueue }
      )
    }

    return (
      <Card>
        <CardHeader>
          <CardTitle>Your Review</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <StatusBadge status={status} />
            <span className="text-sm text-muted-foreground">Ready to schedule or publish</span>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button size="sm" onClick={() => setPublishConfirmOpen(true)} disabled={publishNow.isPending}>
              <Check className="mr-1 h-4 w-4" />
              Publish Now
            </Button>
            <Button size="sm" variant="outline" onClick={() => setShowRejectForm(true)}>
              <X className="mr-1 h-4 w-4" />
              Reject
            </Button>
          </div>

          <RejectConfirmation
            open={showRejectForm}
            comments={rejectComments}
            isSubmitting={rejectApproved.isPending}
            onCommentsChange={setRejectComments}
            onCancel={() => {
              setShowRejectForm(false)
              setRejectComments('')
            }}
            onConfirm={handleReject}
          />

          <hr />

          <p className="text-sm font-medium">Or schedule for later:</p>
          <div className="space-y-3">
            <div className="space-y-2">
              <Label>Publish Date & Time</Label>
              <Input
                type="datetime-local"
                min={minDateTime}
                value={scheduleDate}
                onChange={(e) => setScheduleDate(e.target.value)}
              />
            </div>
            <Button size="sm" variant="outline" onClick={handleSchedule} disabled={!scheduleDate || schedule.isPending}>
              <CalendarCheck className="mr-1 h-4 w-4" />
              {schedule.isPending ? 'Scheduling...' : 'Schedule'}
            </Button>
          </div>
        </CardContent>

        <PublishNowConfirmDialog
          open={publishConfirmOpen}
          onOpenChange={setPublishConfirmOpen}
          isPublishing={publishNow.isPending}
          onConfirm={handlePublishNow}
        />
      </Card>
    )
  }

  // ── Path 3: Already scheduled (Approved + has date) ──
  if (isScheduled) {
    const handlePublishNow = () => {
      publishNow.mutate(assetId, { onSuccess: navigateToQueue })
    }

    const handleUnschedule = () => {
      unschedule.mutate(assetId)
    }

    const handleReject = () => {
      if (!rejectComments.trim()) return
      rejectApproved.mutate({ assetId, comments: rejectComments }, { onSuccess: navigateToQueue })
    }

    return (
      <Card>
        <CardHeader>
          <CardTitle>Your Review</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <Badge>Approved</Badge>
            <Badge variant="outline">
              <CalendarCheck className="mr-1 h-3 w-3" />
              Scheduled
            </Badge>
          </div>

          <p className="text-sm">
            Scheduled for <strong>{formatDate(scheduledPublishAt!, 'long')}</strong>
          </p>

          <div className="flex flex-wrap gap-2">
            <Button size="sm" onClick={() => setPublishConfirmOpen(true)} disabled={publishNow.isPending}>
              <Check className="mr-1 h-4 w-4" />
              Publish Now
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="text-destructive"
              onClick={() => setUnscheduleConfirmOpen(true)}
              disabled={unschedule.isPending}
            >
              <XCircle className="mr-1 h-4 w-4" />
              Cancel Schedule
            </Button>
            <Button size="sm" variant="outline" onClick={() => setShowRejectForm(true)}>
              <X className="mr-1 h-4 w-4" />
              Reject
            </Button>
          </div>

          <RejectConfirmation
            open={showRejectForm}
            comments={rejectComments}
            isSubmitting={rejectApproved.isPending}
            onCommentsChange={setRejectComments}
            onCancel={() => {
              setShowRejectForm(false)
              setRejectComments('')
            }}
            onConfirm={handleReject}
          />
        </CardContent>

        <PublishNowConfirmDialog
          open={publishConfirmOpen}
          onOpenChange={setPublishConfirmOpen}
          isPublishing={publishNow.isPending}
          onConfirm={handlePublishNow}
        />

        <AlertDialog open={unscheduleConfirmOpen} onOpenChange={setUnscheduleConfirmOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Cancel scheduled publish?</AlertDialogTitle>
              <AlertDialogDescription>
                This will remove the scheduled publish date. The asset will remain approved and can be rescheduled or
                published manually later.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Keep Schedule</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleUnschedule}
                disabled={unschedule.isPending}
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              >
                {unschedule.isPending ? 'Cancelling...' : 'Cancel Schedule'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </Card>
    )
  }

  // ── Path 4: Already reviewed / other status ──
  return (
    <Card>
      <CardHeader>
        <CardTitle>Your Review</CardTitle>
      </CardHeader>
      <CardContent className="py-6 text-center">
        <StatusBadge status={status} />
        <p className="mt-2 text-sm text-muted-foreground">This asset has already been reviewed.</p>
      </CardContent>
    </Card>
  )
}
