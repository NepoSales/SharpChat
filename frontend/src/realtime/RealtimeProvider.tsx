import {
  HttpTransportType,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { getChanges, type Message, type Receipt } from '../api/messages'
import { API_BASE, getAccessToken } from '../api/session'
import { useAuth } from '../auth/useAuth'
import { conversationKeys, useConversations } from '../conversations/queries'
import {
  applyReceipt,
  cachedConversationIds,
  lastChangeSeq,
  markReadLocally,
  messageKeys,
  touchConversation,
  upsertMessage,
  type ChatMessage,
} from '../messages/cache'
import { RealtimeContext, type ConnectionState } from './RealtimeContext'
import { retryDelay } from './retry'

type HubResponse<T> =
  { data: T; error: null } | { data: null; error: { code: string; message: string } }

type ReceiptKind = 'MarkDelivered' | 'MarkRead'

const RECEIPT_DEBOUNCE_MS = 400
const MAX_SYNC_PAGES = 5

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userId = user?.id ?? ''
  const queryClient = useQueryClient()
  const conversations = useConversations()
  const connectionRef = useRef<HubConnection | null>(null)
  const outboxRef = useRef(new Map<string, ChatMessage>())
  const syncingRef = useRef(new Map<string, Promise<void>>())
  const receiptsRef = useRef(new Map<string, number>())
  const sentReceiptsRef = useRef(new Map<string, number>())
  const receiptTimerRef = useRef<number | null>(null)
  const [state, setState] = useState<ConnectionState>('connecting')

  async function deliver(message: ChatMessage) {
    const connection = connectionRef.current
    if (!connection || connection.state !== HubConnectionState.Connected) return
    try {
      const response = await connection.invoke<HubResponse<Message>>('SendMessage', {
        conversationId: message.conversationId,
        messageId: message.id,
        content: message.content,
      })
      outboxRef.current.delete(message.id)
      if (response.error) {
        upsertMessage(queryClient, { ...message, status: 'failed', error: response.error.message })
        return
      }
      upsertMessage(queryClient, response.data)
      touchConversation(queryClient, response.data, userId)
    } catch {
      return
    }
  }

  function flushOutbox() {
    outboxRef.current.forEach((message) => void deliver(message))
  }

  function scheduleReceipt(kind: ReceiptKind, conversationId: string, seq: number) {
    const key = `${kind}:${conversationId}`
    if (seq <= Math.max(receiptsRef.current.get(key) ?? 0, sentReceiptsRef.current.get(key) ?? 0)) {
      return
    }
    receiptsRef.current.set(key, seq)
    if (receiptTimerRef.current !== null) return
    receiptTimerRef.current = window.setTimeout(() => {
      receiptTimerRef.current = null
      void flushReceipts()
    }, RECEIPT_DEBOUNCE_MS)
  }

  async function flushReceipts() {
    const connection = connectionRef.current
    if (!connection || connection.state !== HubConnectionState.Connected) return
    const pending = [...receiptsRef.current]
    receiptsRef.current.clear()
    await Promise.all(
      pending.map(async ([key, seq]) => {
        const [kind, conversationId] = key.split(':') as [ReceiptKind, string]
        try {
          const response = await connection.invoke<HubResponse<Receipt>>(kind, {
            conversationId,
            seq,
          })
          if (response.data) {
            sentReceiptsRef.current.set(key, seq)
            applyReceipt(queryClient, response.data, userId)
          }
        } catch {
          return
        }
      }),
    )
  }

  function syncConversation(conversationId: string) {
    const running = syncingRef.current.get(conversationId)
    if (running) return running
    const task = (async () => {
      for (let page = 0; page < MAX_SYNC_PAGES; page++) {
        const after = lastChangeSeq(queryClient, conversationId)
        if (after === null) return
        const changes = await getChanges(conversationId, after)
        changes.items.forEach((message) => upsertMessage(queryClient, message))
        const newest = changes.items.at(-1)
        if (newest) {
          touchConversation(queryClient, newest, userId)
          if (newest.senderId !== userId)
            scheduleReceipt('MarkDelivered', conversationId, newest.seq)
        }
        if (!changes.hasMore) return
      }
      await queryClient.resetQueries({ queryKey: messageKeys.list(conversationId) })
    })()
      .catch(() => undefined)
      .finally(() => syncingRef.current.delete(conversationId))
    syncingRef.current.set(conversationId, task)
    return task
  }

  function resync() {
    cachedConversationIds(queryClient).forEach((id) => void syncConversation(id))
    void queryClient.invalidateQueries({ queryKey: conversationKeys.all })
    flushOutbox()
    void flushReceipts()
  }

  function handleMessage(message: Message) {
    const known = lastChangeSeq(queryClient, message.conversationId)
    upsertMessage(queryClient, message)
    touchConversation(queryClient, message, userId)
    if (message.senderId !== userId) {
      scheduleReceipt('MarkDelivered', message.conversationId, message.seq)
    }
    if (known !== null && message.changeSeq > known + 1) {
      void syncConversation(message.conversationId)
    }
  }

  const handlers = useRef<{
    handleMessage: (message: Message) => void
    resync: () => void
    applyReceipt: (receipt: Receipt) => void
  } | null>(null)
  useEffect(() => {
    handlers.current = {
      handleMessage,
      resync,
      applyReceipt: (receipt) => applyReceipt(queryClient, receipt, userId),
    }
  })

  useEffect(() => {
    let stopped = false
    let attempt = 0
    let retryTimer: number | undefined

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/chat`, {
        accessTokenFactory: async () => (await getAccessToken()) ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount),
      })
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build()

    connection.on('MessageReceived', (message: Message) => handlers.current?.handleMessage(message))
    connection.on('ReceiptUpdated', (receipt: Receipt) => handlers.current?.applyReceipt(receipt))
    connection.onreconnecting(() => setState('reconnecting'))
    connection.onreconnected(() => {
      setState('connected')
      handlers.current?.resync()
    })
    connection.onclose(() => {
      if (!stopped) {
        setState('reconnecting')
        void start()
      }
    })

    async function start() {
      try {
        await connection.start()
        if (stopped) return
        attempt = 0
        setState('connected')
        handlers.current?.resync()
      } catch {
        if (stopped) return
        retryTimer = window.setTimeout(() => void start(), retryDelay(attempt++))
      }
    }

    connectionRef.current = connection
    void start()

    return () => {
      stopped = true
      window.clearTimeout(retryTimer)
      connectionRef.current = null
      void connection.stop()
    }
  }, [])

  const summaries = conversations.data
  useEffect(() => {
    if (state !== 'connected' || !summaries) return
    summaries
      .filter((summary) => summary.lastSeq > summary.lastDeliveredSeq)
      .forEach((summary) => scheduleReceipt('MarkDelivered', summary.id, summary.lastSeq))
  })

  function sendMessage(conversationId: string, content: string) {
    if (!user) return
    const message: ChatMessage = {
      id: crypto.randomUUID(),
      conversationId,
      senderId: user.id,
      senderUserName: user.userName,
      content,
      sentAt: new Date().toISOString(),
      seq: 0,
      changeSeq: 0,
      parentMessageId: null,
      editedAt: null,
      deletedAt: null,
      status: 'sending',
    }
    outboxRef.current.set(message.id, message)
    upsertMessage(queryClient, message)
    void deliver(message)
  }

  function retryMessage(message: ChatMessage) {
    const pending: ChatMessage = { ...message, status: 'sending', error: undefined }
    outboxRef.current.set(pending.id, pending)
    upsertMessage(queryClient, pending)
    void deliver(pending)
  }

  function markRead(conversationId: string, seq: number) {
    if (seq <= 0) return
    markReadLocally(queryClient, conversationId, seq)
    scheduleReceipt('MarkRead', conversationId, seq)
  }

  return (
    <RealtimeContext value={{ state, sendMessage, retryMessage, markRead }}>
      {children}
    </RealtimeContext>
  )
}
