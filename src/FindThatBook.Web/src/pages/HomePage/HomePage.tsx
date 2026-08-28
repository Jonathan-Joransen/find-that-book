import { SearchPanel } from '../../components/SearchPanel/SearchPanel'
import { SearchResults } from '../../components/SearchResults/SearchResults'
import { useBookSearch } from '../../hooks/useBookSearch'
import './HomePage.css'

export function HomePage() {
  const search = useBookSearch()

  return (
    <main className={search.hasSearched ? 'home-page home-page--with-results' : 'home-page'}>
      <SearchPanel
        query={search.query}
        hasSearched={search.hasSearched}
        isLoading={search.isLoading}
        error={search.error}
        onQueryChange={search.setQuery}
        onSearch={search.submitSearch}
      />

      {search.hasSearched && (
        <SearchResults
          key={search.resultQuery}
          books={search.books}
          query={search.resultQuery}
          isLoading={search.isLoading}
        />
      )}
    </main>
  )
}
