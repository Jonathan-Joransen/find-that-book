import { useState, type FormEvent } from 'react'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faHeart as faRegularHeart } from '@fortawesome/free-regular-svg-icons'
import {
  faArrowUp,
  faCircleNotch,
  faHeart as faSolidHeart,
} from '@fortawesome/free-solid-svg-icons'
import './App.css'

type Book = {
  title: string
  author: string
  publishedYear: number
  description: string
}

const examples = [
  'A classic adventure at sea',
  'A novel set during a revolution',
  'A story about growing up in Victorian London',
]

const footerGroups = [
  { title: 'Discover', links: ['Fiction', 'Mystery & thrillers', 'Romance', 'Sci-fi & fantasy'] },
  { title: 'Find That Book', links: ['How it works', 'Reading list', 'Contact'] },
  { title: 'Help', links: ['Book data', 'Privacy', 'Accessibility'] },
]

const coverThemes = ['coral', 'blue', 'ochre', 'sage', 'plum', 'clay']
const apiBaseUrl = import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? ''

function App() {
  const [query, setQuery] = useState('')
  const [books, setBooks] = useState<Book[]>([])
  const [hasSearched, setHasSearched] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [savedBooks, setSavedBooks] = useState<Set<string>>(() => new Set())

  async function searchBooks(searchQuery: string) {
    const trimmedQuery = searchQuery.trim()
    if (!trimmedQuery) return

    setQuery(trimmedQuery)
    setIsLoading(true)
    setError('')

    try {
      const response = await fetch(`${apiBaseUrl}/book/search`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: trimmedQuery }),
      })

      if (!response.ok) throw new Error('The search could not be completed.')

      const results = (await response.json()) as Book[]
      setBooks(results)
      setHasSearched(true)
    } catch {
      setError('We couldn’t connect to the book search. Please try again in a moment.')
    } finally {
      setIsLoading(false)
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void searchBooks(query)
  }

  function toggleSaved(bookKey: string) {
    setSavedBooks((current) => {
      const next = new Set(current)
      if (next.has(bookKey)) next.delete(bookKey)
      else next.add(bookKey)
      return next
    })
  }

  return (
    <div className={hasSearched ? 'app app--results' : 'app'}>
      <header className="site-header">
        <div className="header-main">
          <a className="brand" href="/" aria-label="Find That Book home">
            <img className="brand-logo" src="/logo.png" alt="" aria-hidden="true" />
            <span>find that book</span>
          </a>
        </div>
      </header>

      <main>
        <section className="search-section" aria-labelledby="page-title">
          <div className="search-copy">
            <h1 id="page-title">
              {hasSearched ? 'Find another good book' : 'That book you half remember? Let’s find it.'}
            </h1>
            {!hasSearched && (
              <p>Describe the plot, a character, or even just the mood. A few details are enough to start.</p>
            )}
          </div>

          <form className="search-box" onSubmit={handleSubmit}>
            <label className="sr-only" htmlFor="book-query">Describe the book you are looking for</label>
            <textarea
              id="book-query"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault()
                  event.currentTarget.form?.requestSubmit()
                }
              }}
              placeholder="Describe the book you’re trying to find…"
              rows={3}
              maxLength={500}
              autoFocus
            />
            <div className="search-actions">
              <button
                className="submit-button"
                type="submit"
                disabled={isLoading || !query.trim()}
                aria-label="Search for books"
              >
                <FontAwesomeIcon
                  className={isLoading ? 'spinner-icon' : undefined}
                  icon={isLoading ? faCircleNotch : faArrowUp}
                  aria-hidden="true"
                />
              </button>
            </div>
          </form>

          {!hasSearched && (
            <div className="examples" aria-label="Example searches">
              {examples.map((example) => (
                <button key={example} type="button" onClick={() => void searchBooks(example)}>{example}</button>
              ))}
            </div>
          )}

          {error && <p className="error-message" role="alert">{error}</p>}
        </section>

        {hasSearched && (
          <section className="results-section" aria-live="polite" aria-busy={isLoading}>
            <div className="results-heading">
              <div>
                <p className="results-context">Book matches</p>
                <h2>Results for “{query}”</h2>
              </div>
              <span>{books.length} {books.length === 1 ? 'result' : 'results'}</span>
            </div>

            {books.length > 0 ? (
              <div className="book-grid">
                {books.map((book, index) => {
                  const bookKey = `${book.title}-${book.author}`
                  const isSaved = savedBooks.has(bookKey)
                  return (
                  <article className="book-card" key={`${bookKey}-${index}`}>
                    <div className={`book-cover book-cover--${coverThemes[index % coverThemes.length]}`} aria-hidden="true">
                      <span className="cover-kicker">A book by</span>
                      <strong>{book.author}</strong>
                      <i />
                      <span className="cover-title">{book.title}</span>
                    </div>
                    <button
                      className={isSaved ? 'favorite-button favorite-button--saved' : 'favorite-button'}
                      type="button"
                      aria-label={`${isSaved ? 'Remove' : 'Save'} ${book.title}`}
                      aria-pressed={isSaved}
                      onClick={() => toggleSaved(bookKey)}
                    >
                      <FontAwesomeIcon icon={isSaved ? faSolidHeart : faRegularHeart} aria-hidden="true" />
                    </button>
                    <div className="book-details">
                      <h3>{book.title}</h3>
                      <p className="book-byline">by {book.author}</p>
                      <p className="book-description">{book.description}</p>
                      <p className="book-year">First published {book.publishedYear}</p>
                    </div>
                  </article>
                  )
                })}
              </div>
            ) : (
              <div className="empty-state">
                <h3>No close matches yet</h3>
                <p>Try adding a character, setting, time period, or a detail from the cover.</p>
              </div>
            )}
          </section>
        )}
      </main>

      <footer>
        <div className="footer-main">
          <a className="footer-brand" href="/">find that book</a>
          <div className="footer-links">
            {footerGroups.map((group) => (
              <div key={group.title}>
                <h3>{group.title}</h3>
                {group.links.map((link) => <a key={link} href="#page-title">{link}</a>)}
              </div>
            ))}
          </div>
        </div>
        <div className="footer-legal">
          <span>© 2026 Find That Book</span>
          <div><a href="#page-title">Terms</a><a href="#page-title">Privacy</a><a href="#page-title">Accessibility</a></div>
        </div>
      </footer>
    </div>
  )
}

export default App
