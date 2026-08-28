import { useCallback, useEffect, useRef, useState } from 'react'
import { searchBooks } from '../services/bookSearchApi'
import type { Book } from '../models/Book'

export function useBookSearch() {
  const [query, setQuery] = useState('')
  const [resultQuery, setResultQuery] = useState('')
  const [books, setBooks] = useState<Book[]>([])
  const [hasSearched, setHasSearched] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const activeRequest = useRef<AbortController | null>(null)

  useEffect(() => () => activeRequest.current?.abort(), [])

  const submitSearch = useCallback(async (searchQuery: string) => {
    const trimmedQuery = searchQuery.trim()
    if (!trimmedQuery) return

    activeRequest.current?.abort()
    const request = new AbortController()
    activeRequest.current = request

    setQuery(trimmedQuery)
    setIsLoading(true)
    setError('')

    try {
      const results = await searchBooks(trimmedQuery, request.signal)
      setBooks(results)
      setResultQuery(trimmedQuery)
      setHasSearched(true)
    } catch (requestError) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return
      setError('We couldn’t connect to the book search. Please try again in a moment.')
    } finally {
      if (activeRequest.current === request) {
        activeRequest.current = null
        setIsLoading(false)
      }
    }
  }, [])

  return {
    query,
    setQuery,
    resultQuery,
    books,
    hasSearched,
    isLoading,
    error,
    submitSearch,
  }
}
