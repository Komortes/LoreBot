import type { ChatCard, ChatResponseType } from '../types'

const labels: Record<ChatResponseType, string> = {
  answer: 'Ответ',
  character: 'Персонаж',
  timeline: 'Хронология',
  comparison: 'Сравнение',
  no_context: 'Нет данных',
  guardrail_blocked: 'Заблокировано',
  rate_limited: 'Лимит',
}

export function ResponseCards({
  responseType,
  confidence,
  cards,
}: {
  responseType?: ChatResponseType
  confidence?: number | null
  cards?: ChatCard[]
}) {
  const showType = responseType && responseType !== 'answer'
  const showConf = confidence != null && confidence > 0
  const hasCards = cards && cards.length > 0
  if (!showType && !showConf && !hasCards) return null

  const pct = showConf ? Math.round(confidence! * 100) : 0
  const confClass = pct >= 70 ? 'high' : pct >= 40 ? 'mid' : 'low'

  return (
    <>
      {(showType || showConf) && (
        <div className="response-meta">
          {showType && (
            <span className={`type-badge ${responseType}`}>
              <span className="type-dot" />
              {labels[responseType!]}
            </span>
          )}
          {showConf && (
            <div className="conf-bar-wrap">
              <div className="conf-track">
                <div className={`conf-fill ${confClass}`} style={{ width: `${pct}%` }} />
              </div>
              <span className="conf-label">{pct}%</span>
            </div>
          )}
        </div>
      )}
      {hasCards && (
        <div className="lore-cards">
          {cards!.map((card, i) => (
            <div key={`${card.type}-${i}`} className="lore-card">
              <div className="lore-card-header">
                <span className="lore-card-title">{card.title || card.type}</span>
                <span className="lore-card-type">{card.type}</span>
              </div>
              {card.body && <p className="lore-card-body">{card.body}</p>}
            </div>
          ))}
        </div>
      )}
    </>
  )
}
