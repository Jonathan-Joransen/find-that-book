import { SiteFooter } from './components/SiteFooter/SiteFooter'
import { SiteHeader } from './components/SiteHeader/SiteHeader'
import { HomePage } from './pages/HomePage/HomePage'
import './App.css'

function App() {
  return (
    <div className="app">
      <SiteHeader />
      <HomePage />
      <SiteFooter />
    </div>
  )
}

export default App
