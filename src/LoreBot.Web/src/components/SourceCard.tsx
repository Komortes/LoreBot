import type { Source } from '../types'

function safeUrl(url: string | undefined): string | undefined {
  if (!url) return undefined
  try {
    const { protocol } = new URL(url)
    return protocol === 'http:' || protocol === 'https:' ? url : undefined
  } catch {
    return undefined
  }
}

export function SourceCard({ source, index }: { source: Source; index: number }) {
  const href = safeUrl(source.url)
  const className =
    'block rounded-md border border-gray-700 bg-gray-800/60 px-3 py-2 text-sm hover:border-purple-500 transition-colors'
  const inner = (
    <>
      <span className="font-mono text-purple-400">[{index + 1}]</span>{' '}
      <span className="font-medium">{source.title}</span>
      {source.category && <span className="ml-2 text-xs text-gray-400">{source.category}</span>}
    </>
  )
  return href ? (
    <a href={href} target="_blank" rel="noopener noreferrer" className={className}>
      {inner}
    </a>
  ) : (
    <span className={className}>{inner}</span>
  )
}
