import { useInfiniteQuery } from '@tanstack/react-query'
import { getHistory } from '../api/messages'
import { messageKeys, type ChatPage } from './cache'

export function useMessages(conversationId: string) {
  return useInfiniteQuery({
    queryKey: messageKeys.list(conversationId),
    queryFn: ({ pageParam, signal }): Promise<ChatPage> =>
      getHistory(conversationId, pageParam, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    staleTime: Infinity,
  })
}
