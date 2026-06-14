'use client'

import { useState, useCallback, useEffect } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { useSearch, useAutocomplete } from '@/hooks/use-search'
import { InfiniteScrollTrigger } from '@/components/shared/infinite-scroll'
import { PageHeader } from '@/components/shared/page-header'
import { MediaCard } from '@/components/media/media-card'
import { MasonryGrid } from '@/components/media/masonry-grid'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Command, CommandEmpty, CommandGroup, CommandItem, CommandList } from '@/components/ui/command'
import { Skeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { Search as SearchIcon, X } from 'lucide-react'
import { useDebounce } from '@/hooks/use-debounce'

const FILE_TYPES = [
  { value: 'all', label: 'All Types' },
  { value: 'image', label: 'Images' },
  { value: 'video', label: 'Videos' },
  { value: 'audio', label: 'Audio' },
  { value: 'document', label: 'Documents' }
] as const

export default function SearchPage() {
  const router = useRouter()
  const searchParams = useSearchParams()

  const [inputValue, setInputValue] = useState(searchParams.get('q') ?? '')
  const [showAutocomplete, setShowAutocomplete] = useState(false)
  const [fileType, setFileType] = useState(searchParams.get('fileType') ?? 'all')

  const debouncedQuery = useDebounce(inputValue, 300)
  const autocompleteQuery = useDebounce(inputValue, 150)

  const { data: autocompleteResults } = useAutocomplete(autocompleteQuery)

  const searchQuery = searchParams.get('q') ?? ''

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, error, refetch } = useSearch({
    q: searchQuery,
    fileType: fileType === 'all' ? undefined : fileType
  })

  // Sync URL when debounced query changes
  useEffect(() => {
    if (debouncedQuery.length >= 2) {
      const params = new URLSearchParams()
      params.set('q', debouncedQuery)
      if (fileType !== 'all') params.set('fileType', fileType)
      router.push(`/search?${params.toString()}`)
    }
  }, [debouncedQuery, fileType, router])

  const handleSelectSuggestion = useCallback(
    (suggestion: string) => {
      setInputValue(suggestion)
      setShowAutocomplete(false)
      const params = new URLSearchParams()
      params.set('q', suggestion)
      if (fileType !== 'all') params.set('fileType', fileType)
      router.push(`/search?${params.toString()}`)
    },
    [router, fileType]
  )

  const handleClear = useCallback(() => {
    setInputValue('')
    router.push('/search')
  }, [router])

  const allResults = data?.pages?.flatMap((page) => page?.items ?? []) ?? []
  const totalCount = data?.pages?.[0]?.totalCount ?? 0

  return (
    <div className="space-y-6">
      <PageHeader title="Search" description="Find media assets" />

      {/* Search input with autocomplete */}
      <div className="relative max-w-2xl">
        <div className="relative">
          <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={inputValue}
            onChange={(e) => {
              setInputValue(e.target.value)
              setShowAutocomplete(e.target.value.length >= 2)
            }}
            onFocus={() => {
              if (inputValue.length >= 2) setShowAutocomplete(true)
            }}
            onBlur={() => {
              // Delay to allow clicking suggestions
              setTimeout(() => setShowAutocomplete(false), 200)
            }}
            placeholder="Search assets by title, description, or tags..."
            className="pl-9 pr-9"
          />
          {inputValue && (
            <button
              onClick={handleClear}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Autocomplete dropdown */}
        {showAutocomplete && autocompleteResults && autocompleteResults.length > 0 && (
          <div className="absolute z-50 mt-1 w-full">
            <Command className="rounded-md border shadow-md">
              <CommandList>
                <CommandEmpty>No suggestions</CommandEmpty>
                <CommandGroup>
                  {autocompleteResults.map((suggestion) => (
                    <CommandItem
                      key={suggestion}
                      value={suggestion}
                      onSelect={() => handleSelectSuggestion(suggestion)}
                      className="cursor-pointer"
                    >
                      <SearchIcon className="mr-2 h-3 w-3 text-muted-foreground" />
                      {suggestion}
                    </CommandItem>
                  ))}
                </CommandGroup>
              </CommandList>
            </Command>
          </div>
        )}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <Select value={fileType} onValueChange={(v) => setFileType(v ?? 'all')}>
          <SelectTrigger className="w-36">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {FILE_TYPES.map((ft) => (
              <SelectItem key={ft.value} value={ft.value}>
                {ft.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Results */}
      {searchQuery && (
        <p className="text-sm text-muted-foreground">
          {totalCount.toLocaleString()} result{totalCount !== 1 ? 's' : ''} for{' '}
          <span className="font-medium text-foreground">&quot;{searchQuery}&quot;</span>
        </p>
      )}

      {isLoading && (
        <MasonryGrid>
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="mb-4">
              <Skeleton className="aspect-[4/3] w-full rounded-md" />
              <div className="mt-2 space-y-1">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-1/2" />
              </div>
            </div>
          ))}
        </MasonryGrid>
      )}

      {!isLoading && error && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <SearchIcon className="h-10 w-10 text-muted-foreground/40" />
          <p className="mt-3 text-sm text-muted-foreground">Search failed. Please try again.</p>
          <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}

      {!isLoading && !error && !searchQuery && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <SearchIcon className="h-10 w-10 text-muted-foreground/40" />
          <p className="mt-3 text-sm text-muted-foreground">Enter a search term to find assets.</p>
        </div>
      )}

      {!isLoading && !error && searchQuery && allResults.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <SearchIcon className="h-10 w-10 text-muted-foreground/40" />
          <p className="mt-3 text-sm text-muted-foreground">No results found. Try different keywords.</p>
        </div>
      )}

      {allResults.length > 0 && (
        <>
          <MasonryGrid>
            {allResults.map((result) => (
              <div key={result.id} className="mb-4">
                <MediaCard
                  id={result.id}
                  title={result.title}
                  thumbnailUrl={result.thumbnailUrl}
                  contentType={result.contentType}
                  creatorName={result.creatorName}
                  createdAt={result.createdAt}
                />
              </div>
            ))}
          </MasonryGrid>

          <InfiniteScrollTrigger onLoadMore={fetchNextPage} hasMore={!!hasNextPage} isLoading={isFetchingNextPage} />
        </>
      )}
    </div>
  )
}
