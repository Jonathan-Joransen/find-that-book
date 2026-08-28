import { type FormEvent, type KeyboardEvent, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faArrowUp, faCircleNotch } from '@fortawesome/free-solid-svg-icons'
import type { SearchPanelProps } from './SearchPanel.models'
import './SearchPanel.css'

const suggestedSearches = [
  'That novel about a boy stranded on a boat with a tiger',
  'A mystery set on a snowbound train',
  'The one where children find a secret world through a wardrobe',
  'A woman who keeps reliving the same day',
  'That classic about a scientist who creates a monster',
  'A family moves into a haunted house by the sea',
  'The book with a magical competition held at night',
  'A detective investigating a murder in a quiet English village',
  'That story about a girl growing up during wartime',
  'A romance told through letters found in an old desk',
  'The one with a library where every book is a different life',
  'A group of friends searching for buried treasure',
  'That dystopian novel where books are forbidden',
  'A chef who can taste people’s emotions in food',
  'The book about a man who ages backward',
  'A fantasy with a school for young dragons',
  'That novel narrated by Death during World War II',
  'A journalist uncovers a secret in a small coastal town',
  'The one about an astronaut stranded alone on Mars',
  'A coming-of-age story set during one unforgettable summer',
  'That book where everyone loses the ability to sleep',
  'A young woman inherits a crumbling mansion',
  'The novel about a whale and an obsessed sea captain',
  'A time traveler keeps meeting the same person at different ages',
  'That story with a hidden society beneath London',
  'A painter whose portraits reveal the future',
  'The one about sisters running a magical tea shop',
  'A thriller where the narrator cannot remember the night before',
  'That Charles Dickens novel about an orphan',
  'A book I read as a kid with a mysterious clock in the walls',
]

const TYPE_DELAY_MS = 52
const DELETE_DELAY_MS = 26
const READ_DELAY_MS = 1_800
const NEXT_SUGGESTION_DELAY_MS = 420

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
  const [placeholder, setPlaceholder] = useState('')

  useEffect(() => {
    if (query) return

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

    if (prefersReducedMotion) {
      const timeoutId = window.setTimeout(() => setPlaceholder(suggestedSearches[0]))
      return () => window.clearTimeout(timeoutId)
    }

    let suggestionIndex = 0
    let characterIndex = 0
    let timeoutId: number

    function typeSuggestion() {
      const suggestion = suggestedSearches[suggestionIndex]

      if (characterIndex < suggestion.length) {
        characterIndex += 1
        setPlaceholder(suggestion.slice(0, characterIndex))
        timeoutId = window.setTimeout(typeSuggestion, TYPE_DELAY_MS)
        return
      }

      timeoutId = window.setTimeout(deleteSuggestion, READ_DELAY_MS)
    }

    function deleteSuggestion() {
      const suggestion = suggestedSearches[suggestionIndex]

      if (characterIndex > 0) {
        characterIndex -= 1
        setPlaceholder(suggestion.slice(0, characterIndex))
        timeoutId = window.setTimeout(deleteSuggestion, DELETE_DELAY_MS)
        return
      }

      suggestionIndex = (suggestionIndex + 1) % suggestedSearches.length
      timeoutId = window.setTimeout(typeSuggestion, NEXT_SUGGESTION_DELAY_MS)
    }

    timeoutId = window.setTimeout(typeSuggestion, NEXT_SUGGESTION_DELAY_MS)

    return () => window.clearTimeout(timeoutId)
  }, [query])

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
          onChange={(event) => {
            setPlaceholder('')
            onQueryChange(event.target.value)
          }}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
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

      {error && <p className="search-panel__error" role="alert">{error}</p>}
    </section>
  )
}
