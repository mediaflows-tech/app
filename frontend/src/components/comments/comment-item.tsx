'use client'

import { useState } from 'react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger
} from '@/components/ui/alert-dialog'
import { formatRelativeTime } from '@/lib/utils'
import { MessageSquare, Trash2, Pencil } from 'lucide-react'
import type { CommentDto } from '@/types/api'
import { CommentForm } from './comment-form'

interface CommentItemProps {
  comment: CommentDto
  assetId: number
  onReply: (parentCommentId: number, content: string) => void
  onEdit: (commentId: number, content: string) => void
  onDelete: (commentId: number) => void
  isSubmitting?: boolean
  depth?: number
}

const MAX_DEPTH = 3

export function CommentItem({
  comment,
  assetId,
  onReply,
  onEdit,
  onDelete,
  isSubmitting,
  depth = 0
}: CommentItemProps) {
  const [isReplying, setIsReplying] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const initials = (comment.userName ?? 'U')
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)

  const handleReply = (content: string) => {
    onReply(comment.id, content)
    setIsReplying(false)
  }

  const handleEdit = (content: string) => {
    onEdit(comment.id, content)
    setIsEditing(false)
  }

  return (
    <div className="group">
      <div className="flex gap-3">
        <Avatar className="h-7 w-7 shrink-0">
          <AvatarFallback className="text-[10px] font-medium">{initials}</AvatarFallback>
        </Avatar>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium">{comment.userName}</span>
            <span className="text-xs text-muted-foreground">{formatRelativeTime(comment.createdAt)}</span>
            {new Date(comment.updatedAt).getTime() - new Date(comment.createdAt).getTime() > 1000 && (
              <span className="text-xs text-muted-foreground">(edited)</span>
            )}
          </div>

          {isEditing ? (
            <div className="mt-2">
              <CommentForm
                onSubmit={handleEdit}
                isSubmitting={isSubmitting}
                initialContent={comment.content}
                submitLabel="Save"
                onCancel={() => setIsEditing(false)}
              />
            </div>
          ) : (
            <p className="mt-0.5 text-sm text-foreground/90 whitespace-pre-wrap">{comment.content}</p>
          )}

          {/* Actions */}
          {!isEditing && (
            <div className="mt-1 flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100">
              {depth < MAX_DEPTH && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 gap-1 px-2 text-xs text-muted-foreground"
                  onClick={() => setIsReplying(!isReplying)}
                >
                  <MessageSquare className="h-3 w-3" />
                  Reply
                </Button>
              )}
              {comment.isOwner && (
                <>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 gap-1 px-2 text-xs text-muted-foreground"
                    onClick={() => setIsEditing(true)}
                  >
                    <Pencil className="h-3 w-3" />
                    Edit
                  </Button>
                  <AlertDialog>
                    <AlertDialogTrigger className="inline-flex h-6 items-center gap-1 rounded-md px-2 text-xs text-destructive hover:bg-accent hover:text-destructive">
                      <Trash2 className="h-3 w-3" />
                      Delete
                    </AlertDialogTrigger>
                    <AlertDialogContent>
                      <AlertDialogHeader>
                        <AlertDialogTitle>Delete comment</AlertDialogTitle>
                        <AlertDialogDescription>
                          This action cannot be undone. This will permanently delete your comment.
                        </AlertDialogDescription>
                      </AlertDialogHeader>
                      <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                          onClick={() => onDelete(comment.id)}
                          className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                        >
                          Delete
                        </AlertDialogAction>
                      </AlertDialogFooter>
                    </AlertDialogContent>
                  </AlertDialog>
                </>
              )}
            </div>
          )}

          {/* Reply form */}
          {isReplying && (
            <div className="mt-3">
              <CommentForm
                onSubmit={handleReply}
                isSubmitting={isSubmitting}
                placeholder={`Reply to ${comment.userName}...`}
                submitLabel="Reply"
                onCancel={() => setIsReplying(false)}
              />
            </div>
          )}

          {/* Nested replies */}
          {(comment.replies ?? []).length > 0 && (
            <div className="mt-3 space-y-3 border-l pl-4">
              {(comment.replies ?? []).map((reply) => (
                <CommentItem
                  key={reply.id}
                  comment={reply}
                  assetId={assetId}
                  onReply={onReply}
                  onEdit={onEdit}
                  onDelete={onDelete}
                  isSubmitting={isSubmitting}
                  depth={depth + 1}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
