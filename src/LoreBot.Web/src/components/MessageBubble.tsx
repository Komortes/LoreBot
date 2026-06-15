import type { Message, Source } from '../types'
import { ResponseCards } from './ResponseCards'

function safeUrl(url: string | undefined) {
  if (!url) return undefined
  try {
    const { protocol } = new URL(url)
    return protocol === 'http:' || protocol === 'https:' ? url : undefined
  } catch { return undefined }
}

// Splits text on [N] markers and renders them as clickable links
function AnnotatedText({ text, sources }: { text: string; sources?: Source[] }) {
  if (!sources?.length) return <span className="whitespace-pre-wrap">{text}</span>

  const parts = text.split(/(\[\d+\])/g)
  return (
    <span className="whitespace-pre-wrap">
      {parts.map((part, i) => {
        const match = part.match(/^\[(\d+)\]$/)
        if (!match) return part
        const idx = parseInt(match[1], 10) - 1
        const src = sources[idx]
        const href = src ? safeUrl(src.url) : undefined
        return href ? (
          <a
            key={i}
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            title={src?.title}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontFamily: 'monospace',
              fontSize: '10px',
              fontWeight: 700,
              lineHeight: 1,
              padding: '1px 5px',
              borderRadius: '4px',
              verticalAlign: 'super',
              textDecoration: 'none',
              background: 'rgba(79,70,229,.12)',
              color: 'var(--indigo)',
              border: '1px solid rgba(79,70,229,.25)',
              marginLeft: '1px',
              transition: 'background .15s',
            }}
            onMouseEnter={e => (e.currentTarget.style.background = 'rgba(79,70,229,.22)')}
            onMouseLeave={e => (e.currentTarget.style.background = 'rgba(79,70,229,.12)')}
          >
            {match[1]}
          </a>
        ) : (
          <span key={i} style={{ fontFamily: 'monospace', fontSize: '10px', color: 'var(--indigo)', verticalAlign: 'super' }}>
            {part}
          </span>
        )
      })}
    </span>
  )
}

export function MessageBubble({ message }: { message: Message }) {
  const isUser = message.role === 'user'

  if (isUser) {
    return (
      <div className="msg-row user">
        <div className="bubble user whitespace-pre-wrap">{message.text}</div>
      </div>
    )
  }

  return (
    <div className="msg-row bot">
      <div className="bot-avatar">L</div>
      <div style={{ minWidth: 0, maxWidth: '80%' }}>
        <div className="bubble bot">
          <AnnotatedText text={message.text} sources={message.sources} />
          <ResponseCards
            responseType={message.responseType}
            confidence={message.confidence}
            cards={message.cards}
          />
        </div>
      </div>
    </div>
  )
}
