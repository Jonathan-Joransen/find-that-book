import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faHeart as faRegularHeart } from '@fortawesome/free-regular-svg-icons'
import { faHeart as faSolidHeart } from '@fortawesome/free-solid-svg-icons'
import type { BookCardProps } from './BookCard.models'
import './BookCard.css'

export function BookCard({ book, coverTheme, isSaved, onSaveToggle }: BookCardProps) {
  return (
    <article className="book-card">
      <div className={`book-card__cover book-card__cover--${coverTheme}`} aria-hidden="true">
        <span className="book-card__cover-kicker">A book by</span>
        <strong>{book.author}</strong>
        <i />
        <span className="book-card__cover-title">{book.title}</span>
      </div>

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
        <h3>{book.title}</h3>
        <p className="book-card__byline">by {book.author}</p>
        <p className="book-card__description">{book.description}</p>
        <p className="book-card__year">First published {book.publishedYear}</p>
      </div>
    </article>
  )
}
