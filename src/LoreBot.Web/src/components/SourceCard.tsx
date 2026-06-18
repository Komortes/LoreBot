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
  const inner = (
    <>
      <span className="source-num">[{index + 1}]</span>{' '}
      <span className="source-title">{source.title}</span>
      {source.category && <span className="source-cat">{source.category}</span>}
    </>
  )
  return href ? (
    <a href={href} target="_blank" rel="noopener noreferrer" className="source-link">
      {inner}
    </a>
  ) : (
    <span className="source-link">{inner}</span>
  )
}
