import { useState } from 'react'
import { ChatWindow } from './components/ChatWindow'
import { ThemeToggle } from './components/ThemeToggle'
import { UniversePicker } from './components/UniversePicker'
import { useTheme } from './hooks/useTheme'

export default function App() {
  const [universe, setUniverse] = useState('jojo')
  const { theme, toggleTheme } = useTheme()

  return (
    <div className="flex min-h-screen flex-col bg-gray-50 text-gray-950 transition-colors dark:bg-gray-950 dark:text-white">
      <header className="flex items-center justify-between gap-4 border-b border-gray-200 px-6 py-4 dark:border-gray-800">
        <div className="flex min-w-0 items-center gap-4">
          <span className="text-xl font-bold text-indigo-600 dark:text-indigo-400">LoreBot</span>
          <UniversePicker selected={universe} onChange={setUniverse} />
        </div>
        <ThemeToggle theme={theme} onToggle={toggleTheme} />
      </header>
      <main className="flex-1 overflow-hidden">
        <ChatWindow universe={universe} />
      </main>
    </div>
  )
}
