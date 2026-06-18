import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MessageBubble } from './MessageBubble'
import type { Message } from '../types'

describe('MessageBubble', () => {
  it('renders assistant sources below the answer', () => {
    const message: Message = {
      role: 'assistant',
      text: 'Дио — вампир [1].',
      sources: [
        {
          title: 'Dio Brando',
          url: 'https://example.test/dio',
          category: 'character',
          similarity: 0.92,
        },
      ],
    }

    render(<MessageBubble message={message} />)

    expect(screen.getByText('Источники')).toBeTruthy()
    expect(screen.getByText('Dio Brando')).toBeTruthy()
    const links = screen.getAllByRole('link')

    expect(links.some(link => link.getAttribute('href') === 'https://example.test/dio')).toBe(true)
    expect(screen.getByTitle('Dio Brando').getAttribute('href')).toBe(
      'https://example.test/dio',
    )
  })
})
