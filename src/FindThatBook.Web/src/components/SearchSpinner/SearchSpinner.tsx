import './SearchSpinner.css'

type SearchSpinnerProps = {
  label?: string
}

export function SearchSpinner({ label = 'Searching the shelves…' }: SearchSpinnerProps) {
  return (
    <div className="search-spinner" role="status" aria-live="polite">
      <div className="search-spinner__art" aria-hidden="true">
        <img
          className="search-spinner__image"
          src="/animations/search-spinner.png"
          alt=""
        />
      </div>
      <span>{label}</span>
    </div>
  )
}
