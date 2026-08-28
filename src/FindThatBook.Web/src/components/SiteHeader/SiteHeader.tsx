import './SiteHeader.css'

export function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header__content">
        <a className="site-header__brand" href="/" aria-label="Find That Book home">
          <img className="site-header__logo" src="/logo.png" alt="" aria-hidden="true" />
          <span>find that book</span>
        </a>
      </div>
    </header>
  )
}
