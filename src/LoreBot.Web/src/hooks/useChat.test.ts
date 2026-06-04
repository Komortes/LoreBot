import { renderHook, act, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useChat } from './useChat'

describe('useChat', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ answer: 'Дио — вампир', sources: [{ title: 'Dio', similarity: 0.9 }], tokensUsed: 10, fromCache: false }),
    }))
  })

  it('appends user then assistant message', async () => {
    const { result } = renderHook(() => useChat('jojo'))
    await act(async () => { await result.current.sendMessage('Кто такой Дио?') })
    await waitFor(() => expect(result.current.messages).toHaveLength(2))
    expect(result.current.messages[0].role).toBe('user')
    expect(result.current.messages[1].role).toBe('assistant')
    expect(result.current.messages[1].text).toContain('вампир')
    expect(result.current.messages[1].sources?.[0].title).toBe('Dio')
  })

  it('clears isLoading after completion', async () => {
    const { result } = renderHook(() => useChat('jojo'))
    await act(async () => { await result.current.sendMessage('test') })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
