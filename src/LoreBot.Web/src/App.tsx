import { useState } from 'react'
import { ChatWindow } from './components/ChatWindow'
import { UniversePicker } from './components/UniversePicker'

export default function App() {
  const [universe, setUniverse] = useState('jojo')

  return (
    <div className="min-h-screen bg-gray-950 text-white flex flex-col">
      <header className="flex items-center gap-4 px-6 py-4 border-b border-gray-800">
        <span className="text-xl font-bold text-indigo-400">LoreBot</span>
        <UniversePicker selected={universe} onChange={setUniverse} />
      </header>
      <main className="flex-1 overflow-hidden">
        <ChatWindow universe={universe} />
      </main>
    </div>
  )
}
