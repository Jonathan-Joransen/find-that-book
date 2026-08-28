import type { Book } from '../models/Book'

const apiBaseUrl = import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? ''

export async function searchBooks(query: string, signal?: AbortSignal): Promise<Book[]> {
  const response = await fetch(`${apiBaseUrl}/book/search`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
    signal,
  })

  if (!response.ok) throw new Error('The search could not be completed.')

  return response.json() as Promise<Book[]>
}
