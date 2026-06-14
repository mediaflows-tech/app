'use client'

import { useState, useCallback, type KeyboardEvent } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { X, Plus, Loader2, Check } from 'lucide-react'
import { toast } from '@/lib/toast'
import { useAsset } from '@/hooks/use-assets'
import { useTags, useAddTag, useRemoveTag, useUpdateAsset } from '@/hooks/use-tags'
import { AutoTagDisplay } from './auto-tag-display'

interface TagEditorProps {
  assetId: number
}

export function TagEditor({ assetId }: TagEditorProps) {
  const { data: asset } = useAsset(assetId)
  const { data: tagData } = useTags(assetId)
  const addTag = useAddTag()
  const removeTag = useRemoveTag()
  const updateAsset = useUpdateAsset()

  const [newTag, setNewTag] = useState('')
  const [isEditingTitle, setIsEditingTitle] = useState(false)
  const [isEditingDescription, setIsEditingDescription] = useState(false)
  const [editTitle, setEditTitle] = useState('')
  const [editDescription, setEditDescription] = useState('')

  const handleAddTag = useCallback(() => {
    const tag = newTag.trim().toLowerCase()
    if (!tag) return
    if (tagData?.tags.includes(tag)) {
      toast.error('Tag already exists')
      return
    }
    addTag.mutate(
      { assetId, tag },
      {
        onSuccess: () => setNewTag('')
      }
    )
  }, [assetId, newTag, tagData, addTag])

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter') {
        e.preventDefault()
        handleAddTag()
      }
    },
    [handleAddTag]
  )

  const handleRemoveTag = useCallback(
    (tag: string) => {
      removeTag.mutate({ assetId, tag })
    },
    [assetId, removeTag]
  )

  const handleSaveTitle = useCallback(() => {
    updateAsset.mutate(
      { assetId, title: editTitle },
      {
        onSuccess: () => {
          setIsEditingTitle(false)
          toast.success('Title updated')
        },
        onError: () => toast.error('Failed to update title')
      }
    )
  }, [assetId, editTitle, updateAsset])

  const handleSaveDescription = useCallback(() => {
    updateAsset.mutate(
      { assetId, description: editDescription },
      {
        onSuccess: () => {
          setIsEditingDescription(false)
          toast.success('Description updated')
        },
        onError: () => toast.error('Failed to update description')
      }
    )
  }, [assetId, editDescription, updateAsset])

  return (
    <div className="space-y-6">
      {/* Title & Description */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">Asset Information</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Title */}
          <div className="space-y-2">
            <Label className="text-xs text-muted-foreground">Title</Label>
            {isEditingTitle ? (
              <div className="flex items-center gap-2">
                <Input
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  className="h-8 text-sm"
                  autoFocus
                />
                <Button size="icon" className="h-8 w-8" onClick={handleSaveTitle} disabled={updateAsset.isPending}>
                  {updateAsset.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
                </Button>
                <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => setIsEditingTitle(false)}>
                  <X className="h-3 w-3" />
                </Button>
              </div>
            ) : (
              <p
                className="cursor-pointer rounded-md px-3 py-1.5 text-sm transition-colors hover:bg-accent"
                onClick={() => {
                  setEditTitle(asset?.title ?? '')
                  setIsEditingTitle(true)
                }}
              >
                {asset?.title ?? 'Untitled'}
              </p>
            )}
          </div>

          <Separator />

          {/* Description */}
          <div className="space-y-2">
            <Label className="text-xs text-muted-foreground">Description</Label>
            {isEditingDescription ? (
              <div className="space-y-2">
                <Textarea
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  className="text-sm"
                  rows={3}
                  autoFocus
                />
                <div className="flex items-center gap-2">
                  <Button size="sm" onClick={handleSaveDescription} disabled={updateAsset.isPending}>
                    {updateAsset.isPending ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : null}
                    Save
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => setIsEditingDescription(false)}>
                    Cancel
                  </Button>
                </div>
              </div>
            ) : (
              <p
                className="cursor-pointer rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-accent"
                onClick={() => {
                  setEditDescription(asset?.description ?? '')
                  setIsEditingDescription(true)
                }}
              >
                {asset?.description || 'Click to add a description...'}
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Manual Tags */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">Tags</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Add tag input */}
          <div className="flex items-center gap-2">
            <Input
              placeholder="Add a tag..."
              value={newTag}
              onChange={(e) => setNewTag(e.target.value)}
              onKeyDown={handleKeyDown}
              className="h-8 text-sm"
            />
            <Button size="sm" onClick={handleAddTag} disabled={!newTag.trim() || addTag.isPending}>
              {addTag.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <Plus className="h-3 w-3" />}
            </Button>
          </div>

          {/* Existing tags */}
          {tagData && tagData.tags.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {tagData.tags.map((tag) => (
                <Badge key={tag} variant="secondary" className="gap-1 text-xs">
                  {tag}
                  <button
                    onClick={() => handleRemoveTag(tag)}
                    className="ml-0.5 rounded-full p-0.5 transition-colors hover:bg-foreground/10"
                  >
                    <X className="h-2.5 w-2.5" />
                  </button>
                </Badge>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No tags added yet. Type above and press Enter.</p>
          )}

          {/* AI auto-tags */}
          {tagData && tagData.autoTags.length > 0 && (
            <>
              <Separator />
              <AutoTagDisplay tags={tagData.autoTags} />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
