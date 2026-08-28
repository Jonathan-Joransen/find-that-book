import { useState } from 'react'
import { BookCard } from '../BookCard/BookCard'
import type { SearchResultsProps } from './SearchResults.models'
import './SearchResults.css'

const coverThemes = ['coral', 'blue', 'ochre', 'sage', 'plum', 'clay']

function getBookKey(openLibraryKey: string | null, title: string, author: string) {
  return openLibraryKey ?? `${title}-${author}`
}

export function SearchResults({ books, query, isLoading }: SearchResultsProps) {
  const [savedBooks, setSavedBooks] = useState<Set<string>>(() => new Set())

  function toggleSaved(bookKey: string) {
    setSavedBooks((current) => {
      const next = new Set(current)
      if (next.has(bookKey)) next.delete(bookKey)
      else next.add(bookKey)
      return next
    })
  }

  return (
    <section className="search-results" aria-live="polite" aria-busy={isLoading}>
      <div className="search-results__heading">
        <div>
          <p>Book matches</p>
          <h2>Results for “{query}”</h2>
        </div>
        <span>{books.length} {books.length === 1 ? 'result' : 'results'}</span>
      </div>

      {books.length > 0 ? (
        <div className="search-results__grid">
          {books.map((book, index) => {
            const bookKey = getBookKey(book.openLibraryKey, book.title, book.author)
            return (
              <BookCard
                key={`${bookKey}-${index}`}
                book={book}
                coverTheme={coverThemes[index % coverThemes.length]}
                isSaved={savedBooks.has(bookKey)}
                onSaveToggle={() => toggleSaved(bookKey)}
              />
            )
          })}
        </div>
      ) : (
        <div className="search-results__empty">
          <h3>No close matches yet</h3>
          <p>Try adding a character, setting, time period, or a detail from the cover.</p>
        </div>
      )}
    </section>
  )
}
