import type { Book } from '../../models/Book'

export type SearchResultsProps = {
  books: Book[]
  query: string
  isLoading: boolean
}
