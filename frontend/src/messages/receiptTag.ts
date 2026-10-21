import type { Member } from '../api/conversations'
import type { ChatMessage } from './cache'

export type ReceiptTag = {
  label: string
  tone: 'muted' | 'accent' | 'error'
  readers: string[]
}

export function receiptTag(message: ChatMessage, others: Member[]): ReceiptTag {
  if (message.status === 'sending') return { label: 'sending', tone: 'muted', readers: [] }
  if (message.status === 'failed') return { label: 'failed', tone: 'error', readers: [] }

  const total = others.length
  const readers = others.filter((member) => member.lastReadSeq >= message.seq)
  const delivered = others.filter((member) => member.lastDeliveredSeq >= message.seq).length
  const readerNames = readers.map((member) => member.userName)

  if (total === 0) return { label: 'sent', tone: 'muted', readers: readerNames }
  if (readers.length > 0) {
    return {
      label: `read ${readers.length}/${total}`,
      tone: readers.length === total ? 'accent' : 'muted',
      readers: readerNames,
    }
  }
  if (delivered > 0) return { label: `delivered ${delivered}/${total}`, tone: 'muted', readers: [] }
  return { label: 'sent', tone: 'muted', readers: [] }
}
