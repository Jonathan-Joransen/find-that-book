export type SearchPanelProps = {
  query: string
  hasSearched: boolean
  isLoading: boolean
  error: string
  onQueryChange: (query: string) => void
  onSearch: (query: string) => void | Promise<void>
}
