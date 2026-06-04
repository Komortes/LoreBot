import { useRef, useEffect, useState } from 'react'
import { useChat } from '../hooks/useChat'
import { MessageBubble } from './MessageBubble'

export function ChatWindow({ universe }: { universe: string }) {
  const { messages, isLoading, error, sendMessage } = useChat(universe)
  const [input, setInput] = useState('')
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!input.trim() || isLoading) return
    sendMessage(input)
    setInput('')
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex-1 overflow-y-auto px-4 py-4">
        {messages.length === 0 && (
          <div className="flex h-full items-center justify-center text-gray-500 text-sm">
            Задай вопрос о вселенной...
          </div>
        )}
        {messages.map((m, i) => <MessageBubble key={i} message={m} />)}
        {isLoading && (
          <div className="flex justify-start mb-4">
            <div className="bg-gray-800 rounded-2xl rounded-bl-sm px-4 py-3">
              <span className="text-gray-400 text-sm animate-pulse">Думаю...</span>
            </div>
          </div>
        )}
        {error && <p className="text-red-400 text-sm text-center py-2">{error}</p>}
        <div ref={bottomRef} />
      </div>
      <form onSubmit={handleSubmit} className="border-t border-gray-800 px-4 py-3 flex gap-2">
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="Задай вопрос..."
          disabled={isLoading}
          className="flex-1 bg-gray-900 text-gray-100 border border-gray-700 rounded-xl px-4 py-2 text-sm
                     focus:outline-none focus:border-indigo-500 disabled:opacity-50"
        />
        <button
          type="submit"
          disabled={isLoading || !input.trim()}
          className="bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white px-4 py-2
                     rounded-xl text-sm font-medium transition-colors"
        >
          Отправить
        </button>
      </form>
    </div>
  )
}
