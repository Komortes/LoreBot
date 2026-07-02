import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { UniversePicker } from './UniversePicker'
import * as api from '../api'

describe('UniversePicker', () => {
  it('surfaces an error when the universe list fails to load', async () => {
    vi.spyOn(api, 'getUniverses').mockRejectedValue(new Error('network down'))

    render(<UniversePicker selected="jojo" onChange={() => {}} />)

    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
    // The hardcoded fallback option must still be usable while the real list failed to load.
    expect(screen.getByRole('option', { name: "JoJo's Bizarre Adventure" })).toBeTruthy()
  })

  it('replaces the fallback list once the real universes load', async () => {
    vi.spyOn(api, 'getUniverses').mockResolvedValue([
      { slug: 'jojo', name: "JoJo's Bizarre Adventure" },
      { slug: 'persona', name: 'Persona' },
    ])

    render(<UniversePicker selected="jojo" onChange={() => {}} />)

    await waitFor(() => expect(screen.getByRole('option', { name: 'Persona' })).toBeTruthy())
    expect(screen.queryByRole('alert')).toBeNull()
  })
})
