export interface Source {
  title: string
  url?: string
  category?: string
  similarity: number
}

export type ChatResponseType =
  | 'answer'
  | 'character'
  | 'timeline'
  | 'comparison'
  | 'no_context'
  | 'guardrail_blocked'
  | 'rate_limited'

export interface ChatCard {
  type: string
  title?: string
  body?: string
}

export interface ChatResponse {
  type: ChatResponseType
  answer: string
  sources: Source[]
  confidence?: number | null
  cards: ChatCard[]
  tokensUsed: number
  fromCache: boolean
}

export interface Message {
  role: 'user' | 'assistant'
  text: string
  sources?: Source[]
  responseType?: ChatResponseType
  confidence?: number | null
  cards?: ChatCard[]
}

export interface Universe {
  slug: string
  name: string
  description?: string
  wikiUrl?: string
}
