import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as conversationsApi from '../api/conversations'
import type { ConversationRole } from '../api/conversations'

export const conversationKeys = {
  all: ['conversations'] as const,
  detail: (id: string) => ['conversations', id] as const,
}

export function useConversations() {
  return useQuery({
    queryKey: conversationKeys.all,
    queryFn: ({ signal }) => conversationsApi.listConversations(signal),
  })
}

export function useConversation(id: string) {
  return useQuery({
    queryKey: conversationKeys.detail(id),
    queryFn: ({ signal }) => conversationsApi.getConversation(id, signal),
  })
}

export function useCreateConversation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: conversationsApi.createConversation,
    onSuccess: (conversation) => {
      queryClient.setQueryData(conversationKeys.detail(conversation.id), conversation)
      return queryClient.invalidateQueries({ queryKey: conversationKeys.all, exact: true })
    },
  })
}

function useMemberMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: conversationKeys.all }),
  })
}

export function useAddMember(conversationId: string) {
  return useMemberMutation((userName: string) =>
    conversationsApi.addMember(conversationId, userName),
  )
}

export function useChangeRole(conversationId: string) {
  return useMemberMutation(({ userId, role }: { userId: string; role: ConversationRole }) =>
    conversationsApi.changeRole(conversationId, userId, role),
  )
}

export function useRemoveMember(conversationId: string) {
  return useMemberMutation((userId: string) =>
    conversationsApi.removeMember(conversationId, userId),
  )
}
