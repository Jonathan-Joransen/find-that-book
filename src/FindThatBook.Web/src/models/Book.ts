export type Book = {
  title: string
  author: string
  firstPublishYear: number | null
  description: string
  bookKey: string | null
  bookUrl: string | null
  coverId: number | null
  coverImageUrl: string | null
  explanation: string
  score: number
  authors: BookAuthor[]
}

export type BookAuthor = {
  authorKey: string | null
  name: string
  role: string | null
  isPrimary: boolean
  evidence: 'canonicalWork' | 'searchResult'
}
