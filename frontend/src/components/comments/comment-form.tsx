'use client'

import { useState, useRef, useEffect } from 'react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Loader2 } from 'lucide-react'

interface CommentFormProps {
  onSubmit: (content: string) => void
  isSubmitting?: boolean
  placeholder?: string
  submitLabel?: string
  initialContent?: string
  onCancel?: () => void
}

const MAX_CHARACTERS = 2000

export function CommentForm({
  onSubmit,
  isSubmitting,
  placeholder = 'Write a comment...',
  submitLabel = 'Comment',
  initialContent = '',
  onCancel
}: CommentFormProps) {
  const [content, setContent] = useState(initialContent)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const remaining = MAX_CHARACTERS - content.length
  const isOverLimit = remaining < 0
  const isEmpty = content.trim().length === 0

  useEffect(() => {
    if (initialContent || onCancel) {
      textareaRef.current?.focus()
    }
  }, [initialContent, onCancel])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (isEmpty || isOverLimit || isSubmitting) return
    onSubmit(content.trim())
    if (!initialContent) setContent('')
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      e.preventDefault()
      if (!isEmpty && !isOverLimit && !isSubmitting) {
        onSubmit(content.trim())
        if (!initialContent) setContent('')
      }
    }
    if (e.key === 'Escape' && onCancel) {
      onCancel()
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-2">
      <Textarea
        ref={textareaRef}
        value={content}
        onChange={(e) => setContent(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        rows={3}
        className="resize-none text-sm"
        disabled={isSubmitting}
      />
      <div className="flex items-center justify-between">
        <span
          className={`text-xs font-mono ${
            isOverLimit
              ? 'text-destructive'
              : remaining < 100
                ? 'text-yellow-600 dark:text-yellow-500'
                : 'text-muted-foreground'
          }`}
        >
          {remaining}
        </span>
        <div className="flex gap-2">
          {onCancel && (
            <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
              Cancel
            </Button>
          )}
          <Button type="submit" size="sm" disabled={isEmpty || isOverLimit || isSubmitting}>
            {isSubmitting && <Loader2 className="mr-1.5 h-3 w-3 animate-spin" />}
            {submitLabel}
          </Button>
        </div>
      </div>
    </form>
  )
}
