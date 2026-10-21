import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { Message } from '../api/messages'
import { API_BASE, getAccessToken } from '../api/session'
import { useAuth } from '../auth/useAuth'
import { touchConversation, upsertMessage, type ChatMessage } from '../messages/cache'
import { RealtimeContext, type ConnectionState } from './RealtimeContext'

type HubResponse<T> =
  { data: T; error: null } | { data: null; error: { code: string; message: string } }

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const connectionRef = useRef<HubConnection | null>(null)
  const [state, setState] = useState<ConnectionState>('connecting')

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/chat`, {
        accessTokenFactory: async () => (await getAccessToken()) ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build()

    connection.on('MessageReceived', (message: Message) => {
      upsertMessage(queryClient, message)
      touchConversation(queryClient, message)
    })
    connection.onreconnecting(() => setState('reconnecting'))
    connection.onreconnected(() => {
      setState('connected')
      void queryClient.invalidateQueries({ queryKey: ['messages'] })
      void queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })
    connection.onclose(() => setState('disconnected'))

    connectionRef.current = connection
    connection
      .start()
      .then(() => setState('connected'))
      .catch(() => {
        if (connectionRef.current === connection) setState('disconnected')
      })

    return () => {
      connectionRef.current = null
      void connection.stop()
    }
  }, [queryClient])

  async function deliver(message: ChatMessage) {
    const connection = connectionRef.current
    try {
      if (!connection) throw new Error('Not connected.')
      const response = await connection.invoke<HubResponse<Message>>('SendMessage', {
        conversationId: message.conversationId,
        messageId: message.id,
        content: message.content,
      })
      if (response.error) {
        upsertMessage(queryClient, { ...message, status: 'failed', error: response.error.message })
        return
      }
      upsertMessage(queryClient, response.data)
      touchConversation(queryClient, response.data)
    } catch {
      upsertMessage(queryClient, { ...message, status: 'failed', error: 'Not sent.' })
    }
  }

  async function sendMessage(conversationId: string, content: string) {
    if (!user) return
    const message: ChatMessage = {
      id: crypto.randomUUID(),
      conversationId,
      senderId: user.id,
      senderUserName: user.userName,
      content,
      sentAt: new Date().toISOString(),
      parentMessageId: null,
      editedAt: null,
      deletedAt: null,
      status: 'sending',
    }
    upsertMessage(queryClient, message)
    await deliver(message)
  }

  async function retryMessage(message: ChatMessage) {
    const pending: ChatMessage = { ...message, status: 'sending', error: undefined }
    upsertMessage(queryClient, pending)
    await deliver(pending)
  }

  return <RealtimeContext value={{ state, sendMessage, retryMessage }}>{children}</RealtimeContext>
}
