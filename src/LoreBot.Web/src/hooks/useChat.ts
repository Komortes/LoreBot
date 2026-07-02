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
    } catch {
      // Fall back to plain text when the model returns malformed JSON.
    }
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
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sessionId = useRef(getSessionId())
  const activeRequest = useRef<AbortController | null>(null)

  // Reset history when universe changes, and cancel any in-flight request for the
  // previous universe so its response can't land in the new universe's message list.
  const prevUniverse = useRef(universe)
  if (prevUniverse.current !== universe) {
    prevUniverse.current = universe
    activeRequest.current?.abort()
    activeRequest.current = null
    setMessages([])
    setIsLoading(false)
    setError(null)
  }

  async function sendMessage(text: string) {
    if (!text.trim()) return
    activeRequest.current?.abort()
    const controller = new AbortController()
    activeRequest.current = controller

    setError(null)
    setIsLoading(true)

    // Build history from current messages (last 6 turns = 3 exchanges)
    const history = messages.slice(-6).map(m => ({
      role: m.role === 'user' ? 'user' : 'assistant',
      content: m.text,
    }))

    setMessages(prev => [...prev, { role: 'user', text }])
    try {
      const res = await postChat(universe, text, sessionId.current, history, controller.signal)
      if (activeRequest.current !== controller) return // superseded by a newer request or universe switch
      setMessages(prev => [...prev, {
        role: 'assistant',
        text: extractAnswer(res.answer),
        sources: res.sources,
        responseType: res.type,
        confidence: res.confidence,
        cards: res.cards,
      }])
    } catch (e) {
      if (controller.signal.aborted) return
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      if (activeRequest.current === controller) setIsLoading(false)
    }
  }

  return { messages, isLoading, error, sendMessage }
}
