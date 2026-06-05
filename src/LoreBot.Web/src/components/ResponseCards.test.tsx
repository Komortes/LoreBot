import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ResponseCards } from './ResponseCards'

describe('ResponseCards', () => {
  it('shows response type and confidence without cards', () => {
    render(<ResponseCards responseType="character" confidence={0.87} />)

    expect(screen.getByText('Character')).toBeTruthy()
    expect(screen.getByText('87%')).toBeTruthy()
  })

  it('renders structured cards', () => {
    render(
      <ResponseCards
        responseType="character"
        cards={[{ type: 'character', title: 'Dio Brando', body: 'Vampire antagonist' }]}
      />,
    )

    expect(screen.getByText('Dio Brando')).toBeTruthy()
    expect(screen.getByText('Vampire antagonist')).toBeTruthy()
  })

  it('renders nothing for plain answers without metadata', () => {
    const { container } = render(<ResponseCards responseType="answer" />)

    expect(container.firstChild).toBeNull()
  })
})
