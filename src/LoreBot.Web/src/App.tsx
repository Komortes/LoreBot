import { useState } from 'react'
import { ChatWindow } from './components/ChatWindow'
import { ThemeToggle } from './components/ThemeToggle'
import { UniversePicker } from './components/UniversePicker'
import { useTheme } from './hooks/useTheme'

export default function App() {
  const [universe, setUniverse] = useState('jojo')
  const { theme, toggleTheme } = useTheme()

  return (
    <div className={`app ${theme === 'dark' ? 'dark' : ''}`}>
      <header className="header">
        <div className="header-left">
          <div className="logo-badge">L</div>
          <span className="app-title">LoreBot</span>
          <span className="divider">|</span>
          <UniversePicker selected={universe} onChange={setUniverse} />
        </div>
        <ThemeToggle theme={theme} onToggle={toggleTheme} />
      </header>
      <main className="chat-main">
        <ChatWindow universe={universe} />
      </main>
    </div>
  )
}
