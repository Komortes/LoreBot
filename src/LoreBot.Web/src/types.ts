export interface Source {
  title: string
  url?: string
  category?: string
  similarity: number
}

export interface ChatResponse {
  answer: string
  sources: Source[]
  tokensUsed: number
  fromCache: boolean
}

export interface Message {
  role: 'user' | 'assistant'
  text: string
  sources?: Source[]
}

export interface Universe {
  slug: string
  name: string
  description?: string
  wikiUrl?: string
}
