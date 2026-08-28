import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faHeart as faRegularHeart } from '@fortawesome/free-regular-svg-icons'
import { faHeart as faSolidHeart } from '@fortawesome/free-solid-svg-icons'
import type { BookCardProps } from './BookCard.models'
import './BookCard.css'

export function BookCard({ book, coverTheme, isSaved, onSaveToggle }: BookCardProps) {
  const cover = book.coverImageUrl ? (
    <img
      className="book-card__cover-image"
      src={book.coverImageUrl}
      alt={`Cover of ${book.title}`}
      loading="lazy"
    />
  ) : (
    <div className={`book-card__cover book-card__cover--${coverTheme}`} aria-hidden="true">
      <span className="book-card__cover-kicker">A book by</span>
      <strong>{book.author}</strong>
      <i />
      <span className="book-card__cover-title">{book.title}</span>
    </div>
  )

  return (
    <article className="book-card">
      {book.openLibraryUrl ? (
        <a className="book-card__cover-link" href={book.openLibraryUrl} target="_blank" rel="noreferrer">
          {cover}
        </a>
      ) : cover}

      <button
        className={isSaved ? 'book-card__favorite book-card__favorite--saved' : 'book-card__favorite'}
        type="button"
        aria-label={`${isSaved ? 'Remove' : 'Save'} ${book.title}`}
        aria-pressed={isSaved}
        onClick={onSaveToggle}
      >
        <FontAwesomeIcon icon={isSaved ? faSolidHeart : faRegularHeart} aria-hidden="true" />
      </button>

      <div className="book-card__details">
        <h3>
          {book.openLibraryUrl ? (
            <a href={book.openLibraryUrl} target="_blank" rel="noreferrer">{book.title}</a>
          ) : book.title}
        </h3>
        <p className="book-card__byline">by {book.author}</p>
        <p className="book-card__description">{book.description}</p>
        {book.firstPublishYear && (
          <p className="book-card__year">First published {book.firstPublishYear}</p>
        )}
        {book.openLibraryKey && (
          <p className="book-card__identifier">Open Library {book.openLibraryKey}</p>
        )}
        <p className="book-card__explanation">
          <strong>Why it matched:</strong> {book.explanation}
        </p>
      </div>
    </article>
  )
}
