import type { ChatMessage } from './cache'

export function lastEditable(messages: ChatMessage[] | undefined, userId: string | undefined) {
  return messages
    ?.filter(
      (message) => message.senderId === userId && message.seq !== 0 && message.deletedAt === null,
    )
    .at(-1)
}
