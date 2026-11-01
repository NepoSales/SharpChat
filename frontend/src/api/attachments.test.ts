import { describe, expect, it } from 'vitest'
import { formatSize } from './attachments'

describe('formatSize', () => {
  it.each([
    [512, '512 B'],
    [1536, '1.5 KB'],
    [200 * 1024, '200 KB'],
    [10 * 1024 * 1024, '10.0 MB'],
  ])('%i bytes is %s', (bytes, expected) => {
    expect(formatSize(bytes)).toBe(expected)
  })
})
