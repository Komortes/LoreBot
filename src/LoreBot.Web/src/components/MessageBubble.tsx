import type { Message } from '../types'
import { ResponseCards } from './ResponseCards'
import { SourceCard } from './SourceCard'

export function MessageBubble({ message }: { message: Message }) {
  const isUser = message.role === 'user'
  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-4`}>
      <div className={`max-w-[80%] rounded-2xl px-4 py-3 ${
        isUser
          ? 'bg-indigo-600 text-white rounded-br-sm'
          : 'bg-gray-800 text-gray-100 rounded-bl-sm'
      }`}>
        <p className="text-sm leading-relaxed whitespace-pre-wrap">{message.text}</p>
        {!isUser && (
          <ResponseCards
            responseType={message.responseType}
            confidence={message.confidence}
            cards={message.cards}
          />
        )}
        {!isUser && message.sources && message.sources.length > 0 && (
          <div className="mt-3 flex flex-col gap-1">
            {message.sources.map((s, i) => <SourceCard key={i} source={s} index={i} />)}
          </div>
        )}
      </div>
    </div>
  )
}
