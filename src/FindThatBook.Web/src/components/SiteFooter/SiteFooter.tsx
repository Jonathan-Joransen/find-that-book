import './SiteFooter.css'

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <a className="site-footer__brand" href="/" aria-label="Find That Book home">
        <img className="site-footer__logo" src="/logo.png" alt="" aria-hidden="true" />
        <span>find that book</span>
      </a>
      <span className="site-footer__copyright">
        © {new Date().getFullYear()} Find That Book
      </span>
    </footer>
  )
}
