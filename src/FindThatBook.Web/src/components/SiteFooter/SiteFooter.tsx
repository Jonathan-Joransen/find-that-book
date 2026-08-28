import type { FooterLink, FooterLinkGroup } from './SiteFooter.models'
import './SiteFooter.css'

const pageLink = (label: string): FooterLink => ({ label, href: '#page-title' })

const footerGroups: FooterLinkGroup[] = [
  { title: 'Discover', links: ['Fiction', 'Mystery & thrillers', 'Romance', 'Sci-fi & fantasy'].map(pageLink) },
  { title: 'Find That Book', links: ['How it works', 'Reading list', 'Contact'].map(pageLink) },
  { title: 'Help', links: ['Book data', 'Privacy', 'Accessibility'].map(pageLink) },
]

const legalLinks = ['Terms', 'Privacy', 'Accessibility'].map(pageLink)

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="site-footer__main">
        <a className="site-footer__brand" href="/" aria-label="Find That Book home">
          <img className="site-footer__logo" src="/logo.png" alt="" aria-hidden="true" />
          <span>find that book</span>
        </a>

        <nav className="site-footer__links" aria-label="Footer navigation">
          {footerGroups.map((group) => (
            <div key={group.title}>
              <h2>{group.title}</h2>
              {group.links.map((link) => (
                <a key={link.label} href={link.href}>{link.label}</a>
              ))}
            </div>
          ))}
        </nav>
      </div>

      <div className="site-footer__legal">
        <span>© 2026 Find That Book</span>
        <div>
          {legalLinks.map((link) => (
            <a key={link.label} href={link.href}>{link.label}</a>
          ))}
        </div>
      </div>
    </footer>
  )
}
