import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { RealtimeContext, type RealtimeContextValue } from '../realtime/RealtimeContext'
import type { ChatMessage } from './cache'
import { MessageItem } from './MessageItem'

const realtime: RealtimeContextValue = {
  state: 'connected',
  sendMessage: vi.fn(),
  retryMessage: vi.fn(),
  editMessage: vi.fn(async () => null),
  deleteMessage: vi.fn(async () => null),
  markRead: vi.fn(),
  on: vi.fn(() => () => undefined),
  getOnlineMembers: vi.fn(async () => []),
  sendTyping: vi.fn(),
}

function wrapper({ children }: { children: ReactNode }) {
  return <RealtimeContext value={realtime}>{children}</RealtimeContext>
}

function message(extra: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id: 'm1',
    conversationId: 'c1',
    senderId: 'alice',
    senderUserName: 'alice',
    content: 'hello',
    sentAt: '2026-01-01T10:00:00Z',
    seq: 1,
    changeSeq: 1,
    parentMessageId: null,
    replyCount: 0,
    lastReplyAt: null,
    lastReplySeq: null,
    editedAt: null,
    deletedAt: null,
    ...extra,
  }
}

function renderItem(props: { message?: ChatMessage; isMine?: boolean; canModerate?: boolean }) {
  return render(
    <MessageItem
      message={props.message ?? message()}
      showHeader
      otherMembers={null}
      isMine={props.isMine ?? false}
      canModerate={props.canModerate ?? false}
      editing={false}
      onEditingChange={vi.fn()}
      thread={{ hasNewReplies: false, onOpen: vi.fn() }}
    />,
    { wrapper },
  )
}

function actions() {
  return screen.queryAllByRole('button').map((button) => button.textContent)
}

describe('MessageItem actions', () => {
  it('lets the author reply, edit and delete', () => {
    renderItem({ isMine: true })

    expect(actions()).toEqual(['reply', 'edit', 'delete'])
  })

  it('lets moderators delete but not edit messages of others', () => {
    renderItem({ canModerate: true })

    expect(actions()).toEqual(['reply', 'delete'])
  })

  it('only lets other members reply', () => {
    renderItem({})

    expect(actions()).toEqual(['reply'])
  })

  it('offers no actions while a message is still sending', () => {
    renderItem({ isMine: true, message: message({ seq: 0, changeSeq: 0, status: 'sending' }) })

    expect(actions()).toEqual([])
  })
})

describe('MessageItem states', () => {
  it('shows a placeholder for deleted messages and hides their actions', () => {
    renderItem({
      isMine: true,
      message: message({ content: '', deletedAt: '2026-01-01T11:00:00Z' }),
    })

    expect(screen.getByText('message deleted')).toBeInTheDocument()
    expect(actions()).toEqual([])
  })

  it('marks edited messages', () => {
    renderItem({ message: message({ editedAt: '2026-01-01T10:05:00Z' }) })

    expect(screen.getByText('(edited)')).toBeInTheDocument()
  })

  it('summarizes the thread under the parent', () => {
    renderItem({
      message: message({ replyCount: 2, lastReplyAt: '2026-01-01T10:30:00Z', lastReplySeq: 3 }),
    })

    expect(screen.getByRole('button', { name: /2 replies/ })).toBeInTheDocument()
  })

  it('shows who sent the message as a prompt', () => {
    const { unmount } = renderItem({ isMine: false })
    expect(screen.getByTitle('alice')).toHaveTextContent('alice›')
    unmount()

    renderItem({ isMine: true })
    expect(screen.getByTitle('alice')).toHaveTextContent('you›')
    expect(screen.getByTitle('alice')).toHaveAttribute('data-mine', 'true')
  })
})
