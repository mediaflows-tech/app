'use client'

import { Badge } from '@/components/ui/badge'
import { Sparkles } from 'lucide-react'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'

interface AutoTag {
  name: string
  confidence: number
}

interface AutoTagDisplayProps {
  tags: AutoTag[]
}

export function AutoTagDisplay({ tags }: AutoTagDisplayProps) {
  if (tags.length === 0) return null

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <Sparkles className="h-3 w-3" />
        AI-detected tags
      </div>
      <div className="flex flex-wrap gap-1.5">
        <TooltipProvider>
          {tags.map((tag) => (
            <Tooltip key={tag.name}>
              <TooltipTrigger>
                <Badge variant="outline" className="cursor-default text-xs">
                  {tag.name}
                  <span className="ml-1 text-muted-foreground">{tag.confidence.toFixed(1)}%</span>
                </Badge>
              </TooltipTrigger>
              <TooltipContent>
                <p>Detected with {tag.confidence.toFixed(1)}% confidence</p>
              </TooltipContent>
            </Tooltip>
          ))}
        </TooltipProvider>
      </div>
    </div>
  )
}
