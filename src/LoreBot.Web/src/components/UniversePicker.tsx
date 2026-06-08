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
      className="max-w-64 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-800 focus:border-indigo-500 focus:outline-none dark:border-gray-700 dark:bg-gray-900 dark:text-gray-200"
    >
      {universes.map(u => (
        <option key={u.slug} value={u.slug}>{u.name}</option>
      ))}
    </select>
  )
}
