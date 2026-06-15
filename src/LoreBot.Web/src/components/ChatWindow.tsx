import { useRef, useEffect, useState } from 'react'
import { useChat } from '../hooks/useChat'
import { MessageBubble } from './MessageBubble'

const SUGGESTIONS = [
  'Кто такой Дио Брандо?',
  'Что такое Стенд?',
  'Расскажи про Джотаро Куджо',
]

function TypingDots() {
  return (
    <div className="msg-row bot">
      <div className="bot-avatar">L</div>
      <div className="bubble bot">
        <div className="typing-dots">
          <span /><span /><span />
        </div>
      </div>
    </div>
  )
}

export function ChatWindow({ universe }: { universe: string }) {
  const { messages, isLoading, error, sendMessage } = useChat(universe)
  const [input, setInput] = useState('')
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!input.trim() || isLoading) return
    sendMessage(input)
    setInput('')
  }

  return (
    <>
      <div className="messages">
        {messages.length === 0 && (
          <div className="empty-state">
            <div>
              <div className="empty-icon">L</div>
              <p className="empty-title">LoreBot готов отвечать</p>
              <p className="empty-sub">Задай вопрос о вселенной или выбери подсказку</p>
            </div>
            <div className="suggestions">
              {SUGGESTIONS.map(s => (
                <button
                  key={s}
                  className="suggestion-btn"
                  onClick={() => !isLoading && sendMessage(s)}
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}
        {messages.map((m, i) => <MessageBubble key={i} message={m} />)}
        {isLoading && <TypingDots />}
        {error && <div className="error-bar">{error}</div>}
        <div ref={bottomRef} />
      </div>

      <form className="input-area" onSubmit={handleSubmit}>
        <input
          className="chat-input"
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="Задай вопрос о вселенной..."
          disabled={isLoading}
        />
        <button type="submit" className="send-btn" disabled={isLoading || !input.trim()}>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" width="18" height="18">
            <path d="M3.105 2.288a.75.75 0 0 0-.826.95l1.414 4.926A1.5 1.5 0 0 0 5.135 9.25h6.115a.75.75 0 0 1 0 1.5H5.135a1.5 1.5 0 0 0-1.442 1.086l-1.414 4.926a.75.75 0 0 0 .826.95 28.897 28.897 0 0 0 15.293-7.154.75.75 0 0 0 0-1.115A28.897 28.897 0 0 0 3.105 2.288Z" />
          </svg>
        </button>
      </form>
    </>
  )
}
