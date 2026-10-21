import { describe, expect, it } from 'vitest'
import type { Member } from '../api/conversations'
import type { ChatMessage } from './cache'
import { receiptTag } from './receiptTag'

function member(userName: string, lastDeliveredSeq: number, lastReadSeq: number): Member {
  return {
    userId: userName,
    userName,
    role: 'Member',
    joinedAt: '2026-01-01T00:00:00Z',
    lastDeliveredSeq,
    lastReadSeq,
  }
}

function message(seq: number, status?: ChatMessage['status']): ChatMessage {
  return {
    id: `m${seq}`,
    conversationId: 'c',
    senderId: 'me',
    senderUserName: 'me',
    content: 'hi',
    sentAt: '2026-01-01T00:00:00Z',
    seq,
    changeSeq: seq,
    parentMessageId: null,
    editedAt: null,
    deletedAt: null,
    status,
  }
}

describe('receiptTag', () => {
  it('shows pending and failed sends', () => {
    expect(receiptTag(message(0, 'sending'), [])).toMatchObject({ label: 'sending' })
    expect(receiptTag(message(0, 'failed'), [])).toMatchObject({ label: 'failed', tone: 'error' })
  })

  it('is sent until someone receives it', () => {
    const others = [member('bob', 4, 4), member('carol', 4, 4)]

    expect(receiptTag(message(5), others)).toMatchObject({ label: 'sent' })
  })

  it('counts deliveries before anyone reads', () => {
    const others = [member('bob', 5, 0), member('carol', 4, 0)]

    expect(receiptTag(message(5), others)).toMatchObject({ label: 'delivered 1/2' })
  })

  it('counts readers and highlights only when everyone read', () => {
    const partly = [member('bob', 5, 5), member('carol', 5, 2)]
    const everyone = [member('bob', 5, 5), member('carol', 9, 9)]

    expect(receiptTag(message(5), partly)).toMatchObject({
      label: 'read 1/2',
      tone: 'muted',
      readers: ['bob'],
    })
    expect(receiptTag(message(5), everyone)).toMatchObject({ label: 'read 2/2', tone: 'accent' })
  })

  it('is just sent in a conversation without other members', () => {
    expect(receiptTag(message(1), [])).toMatchObject({ label: 'sent' })
  })
})
