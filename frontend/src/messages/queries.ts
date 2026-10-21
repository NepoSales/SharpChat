import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { getHistory, getMessage, getReplies } from '../api/messages'
import { findMessage, messageKeys, type ChatMessage, type ChatPage } from './cache'

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

export function useReplies(conversationId: string, parentId: string) {
  return useInfiniteQuery({
    queryKey: messageKeys.replies(conversationId, parentId),
    queryFn: ({ pageParam, signal }): Promise<ChatPage> =>
      getReplies(conversationId, parentId, pageParam, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    staleTime: Infinity,
  })
}

export function useThreadParent(conversationId: string, parentId: string) {
  const queryClient = useQueryClient()
  const history = useMessages(conversationId)
  const loaded = history.data?.pages
    .flatMap((page) => page.items)
    .find((item) => item.id === parentId)
  const single = useQuery({
    queryKey: messageKeys.single(conversationId, parentId),
    queryFn: ({ signal }): Promise<ChatMessage> => getMessage(conversationId, parentId, signal),
    enabled: !loaded && !findMessage(queryClient, conversationId, parentId),
    staleTime: Infinity,
  })
  return { parent: loaded ?? single.data, isError: !loaded && single.isError }
}
