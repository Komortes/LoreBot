import type { Source } from '../types'

export function SourceCard({ source, index }: { source: Source; index: number }) {
  return (
    <a
      href={source.url ?? '#'}
      target="_blank"
      rel="noopener noreferrer"
      className="block rounded-md border border-gray-700 bg-gray-800/60 px-3 py-2 text-sm hover:border-purple-500 transition-colors"
    >
      <span className="font-mono text-purple-400">[{index + 1}]</span>{' '}
      <span className="font-medium">{source.title}</span>
      {source.category && <span className="ml-2 text-xs text-gray-400">{source.category}</span>}
    </a>
  )
}
