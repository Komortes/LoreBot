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
          <div className="flex h-full items-center justify-center text-sm text-gray-500 dark:text-gray-400">
            Задай вопрос о вселенной...
          </div>
        )}
        {messages.map((m, i) => <MessageBubble key={i} message={m} />)}
        {isLoading && (
          <div className="flex justify-start mb-4">
            <div className="rounded-md border border-gray-200 bg-white px-4 py-3 dark:border-gray-700 dark:bg-gray-800">
              <span className="animate-pulse text-sm text-gray-500 dark:text-gray-400">Думаю...</span>
            </div>
          </div>
        )}
        {error && <p className="text-red-400 text-sm text-center py-2">{error}</p>}
        <div ref={bottomRef} />
      </div>
      <form onSubmit={handleSubmit} className="flex gap-2 border-t border-gray-200 px-4 py-3 dark:border-gray-800">
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="Задай вопрос..."
          disabled={isLoading}
          className="min-w-0 flex-1 rounded-md border border-gray-300 bg-white px-4 py-2 text-sm text-gray-900
                     focus:border-indigo-500 focus:outline-none disabled:opacity-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
        />
        <button
          type="submit"
          disabled={isLoading || !input.trim()}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition-colors
                     hover:bg-indigo-700 disabled:opacity-50"
        >
          Отправить
        </button>
      </form>
    </div>
  )
}
