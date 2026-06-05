import { useState, useRef } from 'react'
import type { Message } from '../types'
import { postChat } from '../api'

function getSessionId(): string {
  const key = 'lorebot-session'
  let id = localStorage.getItem(key)
  if (!id) { id = crypto.randomUUID(); localStorage.setItem(key, id) }
  return id
}

export function useChat(universe: string) {
  const [messages, setMessages] = useState<Message[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sessionId = useRef(getSessionId())

  async function sendMessage(text: string) {
    if (!text.trim()) return
    setError(null)
    setMessages(prev => [...prev, { role: 'user', text }])
    setIsLoading(true)
    try {
      const res = await postChat(universe, text, sessionId.current)
      setMessages(prev => [...prev, {
        role: 'assistant',
        text: res.answer,
        sources: res.sources,
        responseType: res.type,
        confidence: res.confidence,
        cards: res.cards,
      }])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      setIsLoading(false)
    }
  }

  return { messages, isLoading, error, sendMessage }
}
