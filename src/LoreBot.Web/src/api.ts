import type { ChatResponse, Universe } from './types'

const BASE = (import.meta.env as Record<string, string>)['VITE_API_BASE'] ?? '/api'

export async function postChat(
  universe: string,
  message: string,
  sessionId: string,
  history: { role: string; content: string }[] = [],
): Promise<ChatResponse> {
  const res = await fetch(`${BASE}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ universe, message, sessionId, history }),
  })
  if (!res.ok) throw new Error(`Chat request failed: ${res.status}`)
  return res.json()
}

export async function getUniverses(): Promise<Universe[]> {
  const res = await fetch(`${BASE}/universes`)
  if (!res.ok) throw new Error(`Universes request failed: ${res.status}`)
  return res.json()
}
