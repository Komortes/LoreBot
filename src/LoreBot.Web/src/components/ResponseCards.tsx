import type { ChatCard, ChatResponseType } from '../types'

const labels: Record<ChatResponseType, string> = {
  answer: 'Answer',
  character: 'Character',
  timeline: 'Timeline',
  comparison: 'Comparison',
  no_context: 'No context',
  guardrail_blocked: 'Blocked',
  rate_limited: 'Rate limited',
}

export function ResponseCards({
  responseType,
  cards,
}: {
  responseType?: ChatResponseType
  cards?: ChatCard[]
}) {
  if (!cards || cards.length === 0) return null

  return (
    <div className="mt-3 space-y-2">
      {responseType && (
        <div className="text-[11px] font-medium uppercase tracking-wide text-gray-400">
          {labels[responseType]}
        </div>
      )}
      <div className="grid gap-2">
        {cards.map((card, index) => (
          <article
            key={`${card.type}-${card.title ?? index}`}
            className="rounded-md border border-gray-700 bg-gray-900/70 px-3 py-2"
          >
            <div className="flex items-start justify-between gap-3">
              <h3 className="text-sm font-medium text-gray-100">
                {card.title || card.type}
              </h3>
              <span className="shrink-0 rounded border border-gray-700 px-1.5 py-0.5 text-[11px] text-gray-400">
                {card.type}
              </span>
            </div>
            {card.body && (
              <p className="mt-1 text-sm leading-relaxed text-gray-300">
                {card.body}
              </p>
            )}
          </article>
        ))}
      </div>
    </div>
  )
}
