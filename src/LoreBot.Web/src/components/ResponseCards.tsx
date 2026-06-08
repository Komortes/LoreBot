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

const tones: Record<ChatResponseType, string> = {
  answer: 'border-gray-300 bg-gray-100 text-gray-700 dark:border-gray-700 dark:bg-gray-900/60 dark:text-gray-300',
  character: 'border-indigo-300 bg-indigo-50 text-indigo-700 dark:border-indigo-500/40 dark:bg-indigo-950/30 dark:text-indigo-200',
  timeline: 'border-sky-300 bg-sky-50 text-sky-700 dark:border-sky-500/40 dark:bg-sky-950/30 dark:text-sky-200',
  comparison: 'border-amber-300 bg-amber-50 text-amber-700 dark:border-amber-500/40 dark:bg-amber-950/30 dark:text-amber-200',
  no_context: 'border-gray-300 bg-gray-100 text-gray-700 dark:border-gray-600 dark:bg-gray-900/80 dark:text-gray-300',
  guardrail_blocked: 'border-red-300 bg-red-50 text-red-700 dark:border-red-500/40 dark:bg-red-950/30 dark:text-red-200',
  rate_limited: 'border-orange-300 bg-orange-50 text-orange-700 dark:border-orange-500/40 dark:bg-orange-950/30 dark:text-orange-200',
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
  const hasCards = cards && cards.length > 0
  const hasMeta = responseType && (responseType !== 'answer' || confidence != null)
  if (!hasCards && !hasMeta) return null

  const tone = responseType ? tones[responseType] : tones.answer

  return (
    <div className="mt-3 space-y-2">
      {hasMeta && (
        <div className={`inline-flex items-center gap-2 rounded-md border px-2 py-1 text-[11px] font-medium ${tone}`}>
          <span className="uppercase tracking-wide">{labels[responseType]}</span>
          {confidence != null && (
            <span className="text-gray-400">
              {Math.round(confidence * 100)}%
            </span>
          )}
        </div>
      )}
      {hasCards && (
        <div className="grid gap-2">
          {cards.map((card, index) => (
          <article
            key={`${card.type}-${card.title ?? index}`}
            className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 dark:border-gray-700 dark:bg-gray-900/70"
          >
            <div className="flex items-start justify-between gap-3">
              <h3 className="text-sm font-medium text-gray-900 dark:text-gray-100">
                {card.title || card.type}
              </h3>
              <span className="shrink-0 rounded border border-gray-300 px-1.5 py-0.5 text-[11px] text-gray-500 dark:border-gray-700 dark:text-gray-400">
                {card.type}
              </span>
            </div>
            {card.body && (
              <p className="mt-1 text-sm leading-relaxed text-gray-600 dark:text-gray-300">
                {card.body}
              </p>
            )}
          </article>
          ))}
        </div>
      )}
    </div>
  )
}
