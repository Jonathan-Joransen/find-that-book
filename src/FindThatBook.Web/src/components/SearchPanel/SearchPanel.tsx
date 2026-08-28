import { type FormEvent, type KeyboardEvent, useLayoutEffect, useRef } from 'react'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faArrowUp, faCircleNotch } from '@fortawesome/free-solid-svg-icons'
import type { SearchPanelProps } from './SearchPanel.models'
import './SearchPanel.css'

const exampleSearches = [
  'A classic adventure at sea',
  'A novel set during a revolution',
  'A story about growing up in Victorian London',
]

export function SearchPanel({
  query,
  hasSearched,
  isLoading,
  error,
  onQueryChange,
  onSearch,
}: SearchPanelProps) {
  const formRef = useRef<HTMLFormElement>(null)
  const previousFormTop = useRef<number | null>(null)

  useLayoutEffect(() => {
    const form = formRef.current
    if (!form) return

    const currentTop = form.getBoundingClientRect().top
    const previousTop = previousFormTop.current

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

    if (hasSearched && previousTop !== null && !prefersReducedMotion) {
      const offset = previousTop - currentTop

      if (Math.abs(offset) > 1) {
        form.animate(
          [
            { transform: `translateY(${offset}px)` },
            { transform: 'translateY(0)' },
          ],
          {
            duration: 780,
            easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
          },
        )
      }
    }

    previousFormTop.current = currentTop
  }, [hasSearched])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void onSearch(query)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      event.currentTarget.form?.requestSubmit()
    }
  }

  return (
    <section
      className={hasSearched ? 'search-panel search-panel--compact' : 'search-panel'}
      aria-labelledby="page-title"
    >
      <div className="search-panel__copy">
        <h1
          id="page-title"
          aria-label={hasSearched ? 'Find another book' : 'What was that book?'}
          aria-live="polite"
        >
          <span className="search-panel__title-initial" aria-hidden="true">
            What was that book?
          </span>
          <span className="search-panel__title-results" aria-hidden="true">
            Find another book
          </span>
        </h1>
      </div>

      <form ref={formRef} className="search-panel__form" onSubmit={handleSubmit}>
        <label className="sr-only" htmlFor="book-query">
          Describe the book you are looking for
        </label>
        <textarea
          id="book-query"
          value={query}
          onChange={(event) => onQueryChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Describe the book you’re trying to find…"
          rows={3}
          maxLength={500}
          autoFocus
        />
        <div className="search-panel__actions">
          <button
            className="search-panel__submit"
            type="submit"
            disabled={isLoading || !query.trim()}
            aria-label="Search for books"
          >
            <FontAwesomeIcon
              className={isLoading ? 'search-panel__spinner' : undefined}
              icon={isLoading ? faCircleNotch : faArrowUp}
              aria-hidden="true"
            />
          </button>
        </div>
      </form>

      {!hasSearched && (
        <div className="search-panel__examples" aria-label="Example searches">
          {exampleSearches.map((example) => (
            <button
              key={example}
              type="button"
              disabled={isLoading}
              onClick={() => void onSearch(example)}
            >
              {example}
            </button>
          ))}
        </div>
      )}

      {error && <p className="search-panel__error" role="alert">{error}</p>}
    </section>
  )
}
