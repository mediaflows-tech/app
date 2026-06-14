import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { CommentDto } from '@/types/api'
import { toast } from '@/lib/toast'

export const commentKeys = {
  all: ['comments'] as const,
  list: (assetId: number) => [...commentKeys.all, assetId] as const
}

/** Shape the API actually returns (authorDisplayName instead of userName) */
interface CommentApiResponse {
  id: number
  assetId: number
  authorId: string
  authorDisplayName: string
  content: string
  parentCommentId: number | null
  replies: CommentApiResponse[]
  isOwner: boolean
  createdAt: string
  updatedAt: string
}

function mapComment(raw: CommentApiResponse): CommentDto {
  return {
    id: raw.id,
    assetId: raw.assetId,
    userId: raw.authorId,
    userName: raw.authorDisplayName,
    content: raw.content,
    parentCommentId: raw.parentCommentId,
    replies: (raw.replies ?? []).map(mapComment),
    isOwner: raw.isOwner,
    createdAt: raw.createdAt,
    updatedAt: raw.updatedAt
  }
}

export function useComments(assetId: number) {
  return useQuery<CommentDto[]>({
    queryKey: commentKeys.list(assetId),
    queryFn: async () => {
      const raw = await api.get<CommentApiResponse[]>(`/assets/${assetId}/comments`)
      return raw.map(mapComment)
    },
    staleTime: 15 * 1000
  })
}

interface AddCommentPayload {
  assetId: number
  content: string
  parentCommentId?: number | null
}

export function useAddComment() {
  const queryClient = useQueryClient()

  return useMutation<CommentDto, Error, AddCommentPayload, { previousComments: CommentDto[] | undefined }>({
    mutationFn: ({ assetId, content, parentCommentId }) =>
      api.post<CommentDto>(`/assets/${assetId}/comments`, {
        content,
        parentCommentId: parentCommentId ?? null
      }),

    onMutate: async ({ assetId, content, parentCommentId }) => {
      await queryClient.cancelQueries({
        queryKey: commentKeys.list(assetId)
      })

      const previousComments = queryClient.getQueryData<CommentDto[]>(commentKeys.list(assetId))

      // Optimistic comment with temporary id
      const optimisticComment: CommentDto = {
        id: -Date.now(),
        assetId,
        userId: '',
        userName: 'You',
        content,
        parentCommentId: parentCommentId ?? null,
        replies: [],
        isOwner: true,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }

      queryClient.setQueryData<CommentDto[]>(commentKeys.list(assetId), (old) => {
        if (!old) return [optimisticComment]

        if (parentCommentId) {
          // Insert as reply (shallow -- find the parent and append)
          return old.map((comment) =>
            comment.id === parentCommentId
              ? { ...comment, replies: [...(comment.replies ?? []), optimisticComment] }
              : comment
          )
        }

        return [...old, optimisticComment]
      })

      return { previousComments }
    },

    onError: (_err, { assetId }, context) => {
      if (context?.previousComments) {
        queryClient.setQueryData(commentKeys.list(assetId), context.previousComments)
      }
      toast.error('Failed to post comment')
    },

    onSettled: (_data, _err, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: commentKeys.list(assetId) })
    }
  })
}

interface EditCommentPayload {
  commentId: number
  assetId: number
  content: string
}

export function useEditComment() {
  const queryClient = useQueryClient()

  return useMutation<CommentDto, Error, EditCommentPayload>({
    mutationFn: ({ commentId, content }) => api.put<CommentDto>(`/comments/${commentId}`, { content }),

    onSuccess: (_data, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: commentKeys.list(assetId) })
      toast.success('Comment updated')
    },

    onError: () => {
      toast.error('Failed to update comment')
    }
  })
}

interface DeleteCommentPayload {
  commentId: number
  assetId: number
}

export function useDeleteComment() {
  const queryClient = useQueryClient()

  return useMutation<void, Error, DeleteCommentPayload, { previousComments: CommentDto[] | undefined }>({
    mutationFn: ({ commentId }) => api.delete(`/comments/${commentId}`),

    onMutate: async ({ commentId, assetId }) => {
      await queryClient.cancelQueries({
        queryKey: commentKeys.list(assetId)
      })

      const previousComments = queryClient.getQueryData<CommentDto[]>(commentKeys.list(assetId))

      // Optimistically remove the comment
      queryClient.setQueryData<CommentDto[]>(commentKeys.list(assetId), (old) => {
        if (!old) return []
        return removeCommentFromTree(old, commentId)
      })

      return { previousComments }
    },

    onError: (_err, { assetId }, context) => {
      if (context?.previousComments) {
        queryClient.setQueryData(commentKeys.list(assetId), context.previousComments)
      }
      toast.error('Failed to delete comment')
    },

    onSettled: (_data, _err, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: commentKeys.list(assetId) })
    },

    onSuccess: () => {
      toast.success('Comment deleted')
    }
  })
}

function removeCommentFromTree(comments: CommentDto[], commentId: number): CommentDto[] {
  return comments
    .filter((c) => c.id !== commentId)
    .map((c) => ({
      ...c,
      replies: removeCommentFromTree(c.replies ?? [], commentId)
    }))
}
