import { useEffect, useState } from 'react'
import { getUniverses } from '../api'
import type { Universe } from '../types'

interface Props {
  selected: string
  onChange: (slug: string) => void
}

export function UniversePicker({ selected, onChange }: Props) {
  const [universes, setUniverses] = useState<Universe[]>([
    { slug: 'jojo', name: "JoJo's Bizarre Adventure" },
  ])

  useEffect(() => {
    getUniverses().then(setUniverses).catch(() => {})
  }, [])

  return (
    <select
      value={selected}
      onChange={e => onChange(e.target.value)}
      aria-label="Universe"
      className="universe-select"
    >
      {universes.map(u => (
        <option key={u.slug} value={u.slug}>{u.name}</option>
      ))}
    </select>
  )
}
