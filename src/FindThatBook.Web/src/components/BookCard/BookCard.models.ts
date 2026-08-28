import type { Book } from '../../models/Book'

export type BookCardProps = {
  book: Book
  coverTheme: string
  isSaved: boolean
  onSaveToggle: () => void
}
