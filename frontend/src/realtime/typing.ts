import type { TypingUser } from './ActivityContext'

export const TYPING_SEND_INTERVAL_MS = 3_000
export const TYPING_VISIBLE_MS = 5_000

export function typingKey(conversationId: string, parentMessageId: string | null) {
  return `${conversationId}:${parentMessageId ?? ''}`
}

export function describeTyping(users: TypingUser[]) {
  if (users.length === 0) return null
  if (users.length === 1) return `${users[0].userName} is typing…`
  if (users.length === 2) return `${users[0].userName} and ${users[1].userName} are typing…`
  return 'Several people are typing…'
}
