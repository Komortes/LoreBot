import { renderHook, act, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useChat } from './useChat'
import * as api from '../api'
import type { ChatResponse } from '../types'

describe('useChat', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        type: 'character',
        answer: 'Дио — вампир',
        sources: [{ title: 'Dio', similarity: 0.9 }],
        confidence: 0.9,
        cards: [{ type: 'character', title: 'Dio' }],
        tokensUsed: 10,
        fromCache: false,
      }),
    }))
  })

  it('appends user then assistant message', async () => {
    const { result } = renderHook(() => useChat('jojo'))
    await act(async () => { await result.current.sendMessage('Кто такой Дио?') })
    await waitFor(() => expect(result.current.messages).toHaveLength(2))
    expect(result.current.messages[0].role).toBe('user')
    expect(result.current.messages[1].role).toBe('assistant')
    expect(result.current.messages[1].text).toContain('вампир')
    expect(result.current.messages[1].responseType).toBe('character')
    expect(result.current.messages[1].sources?.[0].title).toBe('Dio')
  })

  it('clears isLoading after completion', async () => {
    const { result } = renderHook(() => useChat('jojo'))
    await act(async () => { await result.current.sendMessage('test') })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })

  it('discards a stale response from the previous universe after switching', async () => {
    let resolveSlowRequest: (value: ChatResponse) => void = () => {}
    const postChatSpy = vi.spyOn(api, 'postChat').mockImplementation(
      () => new Promise<ChatResponse>((resolve) => { resolveSlowRequest = resolve }),
    )

    const { result, rerender } = renderHook(({ universe }) => useChat(universe), {
      initialProps: { universe: 'jojo' },
    })

    act(() => { void result.current.sendMessage('Кто такой Дио?') })
    await waitFor(() => expect(result.current.isLoading).toBe(true))

    // Switch universes before the slow request resolves.
    rerender({ universe: 'persona' })
    expect(result.current.messages).toHaveLength(0)
    expect(result.current.isLoading).toBe(false)

    // The stale response now arrives; it must not be appended to the new universe's messages.
    await act(async () => {
      resolveSlowRequest({
        type: 'character', answer: 'Дио — вампир', sources: [], cards: [], tokensUsed: 0, fromCache: false,
      })
    })

    expect(result.current.messages).toHaveLength(0)
    postChatSpy.mockRestore()
  })
})
