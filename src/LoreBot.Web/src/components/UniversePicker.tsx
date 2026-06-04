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
      className="bg-gray-800 text-gray-200 border border-gray-700 rounded-lg px-3 py-1 text-sm focus:outline-none focus:border-indigo-500"
    >
      {universes.map(u => (
        <option key={u.slug} value={u.slug}>{u.name}</option>
      ))}
    </select>
  )
}
