'use client'

import { useComments, useAddComment, useEditComment, useDeleteComment } from '@/hooks/use-comments'
import { CommentItem } from './comment-item'
import { CommentForm } from './comment-form'
import { Skeleton } from '@/components/ui/skeleton'
import { MessageSquare } from 'lucide-react'

interface CommentThreadProps {
  assetId: number
}

export function CommentThread({ assetId }: CommentThreadProps) {
  const { data: comments, isLoading, error } = useComments(assetId)
  const addComment = useAddComment()
  const editComment = useEditComment()
  const deleteComment = useDeleteComment()

  const handleAddComment = (content: string) => {
    addComment.mutate({ assetId, content })
  }

  const handleReply = (parentCommentId: number, content: string) => {
    addComment.mutate({ assetId, content, parentCommentId })
  }

  const handleEdit = (commentId: number, content: string) => {
    editComment.mutate({ commentId, assetId, content })
  }

  const handleDelete = (commentId: number) => {
    deleteComment.mutate({ commentId, assetId })
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex gap-3">
            <Skeleton className="h-7 w-7 rounded-full" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-4 w-full" />
            </div>
          </div>
        ))}
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <MessageSquare className="h-8 w-8 text-muted-foreground/40" />
        <p className="mt-2 text-sm text-destructive">Failed to load comments.</p>
      </div>
    )
  }

  const topLevelComments = comments?.filter((c) => !c.parentCommentId) ?? []

  return (
    <div className="space-y-6">
      {/* New comment form */}
      <CommentForm onSubmit={handleAddComment} isSubmitting={addComment.isPending} />

      {/* Comment list */}
      {topLevelComments.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <MessageSquare className="h-8 w-8 text-muted-foreground/40" />
          <p className="mt-2 text-sm text-muted-foreground">No comments yet. Be the first to comment.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {topLevelComments.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              assetId={assetId}
              onReply={handleReply}
              onEdit={handleEdit}
              onDelete={handleDelete}
              isSubmitting={addComment.isPending}
            />
          ))}
        </div>
      )}
    </div>
  )
}
