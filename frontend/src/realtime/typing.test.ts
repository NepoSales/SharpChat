import { describe, expect, it } from 'vitest'
import { describeTyping, typingKey } from './typing'

const user = (userName: string) => ({ userId: userName, userName })

describe('typing', () => {
  it('describes who is typing', () => {
    expect(describeTyping([])).toBeNull()
    expect(describeTyping([user('alice')])).toBe('alice is typing…')
    expect(describeTyping([user('alice'), user('bob')])).toBe('alice and bob are typing…')
    expect(describeTyping([user('alice'), user('bob'), user('carol')])).toBe(
      'Several people are typing…',
    )
  })

  it('keeps the main timeline and each thread apart', () => {
    expect(typingKey('c1', null)).not.toBe(typingKey('c1', 'm1'))
    expect(typingKey('c1', 'm1')).not.toBe(typingKey('c1', 'm2'))
  })
})
