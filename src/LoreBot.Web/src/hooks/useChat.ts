import { useState, useRef } from 'react'
import type { Message } from '../types'
import { postChat } from '../api'

function extractAnswer(raw: string): string {
  const t = raw.trim()
  const unwrapped = t.replace(/^```(?:json)?\s*/i, '').replace(/\s*```$/i, '').trim()
  if (unwrapped.startsWith('{')) {
    try {
      const parsed = JSON.parse(unwrapped)
      if (typeof parsed.answer === 'string' && parsed.answer.trim()) return parsed.answer.trim()
    } catch {}
  }
  return t
}

function getSessionId(): string {
  const key = 'lorebot-session'
  let id = localStorage.getItem(key)
  if (!id) { id = crypto.randomUUID(); localStorage.setItem(key, id) }
  return id
}

export function useChat(universe: string) {
  const [messages, setMessages] = useState<Message[]>([])
  // Reset history when universe changes
  const prevUniverse = useRef(universe)
  if (prevUniverse.current !== universe) {
    prevUniverse.current = universe
    setMessages([])
  }
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sessionId = useRef(getSessionId())

  async function sendMessage(text: string) {
    if (!text.trim()) return
    setError(null)
    setIsLoading(true)

    // Build history from current messages (last 6 turns = 3 exchanges)
    const history = messages.slice(-6).map(m => ({
      role: m.role === 'user' ? 'user' : 'assistant',
      content: m.text,
    }))

    setMessages(prev => [...prev, { role: 'user', text }])
    try {
      const res = await postChat(universe, text, sessionId.current, history)
      setMessages(prev => [...prev, {
        role: 'assistant',
        text: extractAnswer(res.answer),
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
