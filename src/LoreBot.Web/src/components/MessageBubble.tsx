import type { Message, Source } from '../types'
import { ResponseCards } from './ResponseCards'
import { SourceCard } from './SourceCard'

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
            className="citation-link"
          >
            {match[1]}
          </a>
        ) : (
          <span key={i} className="citation-fallback">
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
      <div className="message-stack">
        <div className="bubble bot">
          <AnnotatedText text={message.text} sources={message.sources} />
          <ResponseCards
            responseType={message.responseType}
            confidence={message.confidence}
            cards={message.cards}
          />
        </div>
        {!!message.sources?.length && (
          <div className="source-panel" aria-label="Источники ответа">
            <div className="source-panel-title">Источники</div>
            <div className="sources">
              {message.sources.map((source, index) => (
                <SourceCard key={`${source.title}-${index}`} source={source} index={index} />
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
