import type { BookCardProps } from './BookCard.models'
import './BookCard.css'

export function BookCard({ book }: BookCardProps) {
  const cover = book.coverImageUrl ? (
    <img
      className="book-card__cover-image"
      src={book.coverImageUrl}
      alt={`Cover of ${book.title}`}
      loading="lazy"
    />
  ) : (
    <div className="book-card__cover book-card__cover--fallback" aria-hidden="true">
      <span className="book-card__cover-kicker">A book by</span>
      <strong>{book.author}</strong>
      <i />
      <span className="book-card__cover-title">{book.title}</span>
    </div>
  )

  return (
    <article className="book-card">
      {book.bookUrl ? (
        <a className="book-card__cover-link" href={book.bookUrl} target="_blank" rel="noreferrer">
          {cover}
        </a>
      ) : cover}

      <div className="book-card__details">
        <h3>
          {book.bookUrl ? (
            <a href={book.bookUrl} target="_blank" rel="noreferrer">{book.title}</a>
          ) : book.title}
        </h3>
        <p className="book-card__byline">by {book.author}</p>
        {book.description && <p className="book-card__description">{book.description}</p>}
        {book.firstPublishYear && (
          <p className="book-card__year">First published {book.firstPublishYear}</p>
        )}
        <p className="book-card__score">Match score: {book.score}/100</p>
        <p className="book-card__explanation">
          <strong>Why it matched:</strong> {book.explanation}
        </p>
      </div>
    </article>
  )
}
