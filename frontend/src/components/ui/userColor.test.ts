import { describe, expect, it } from 'vitest'
import { USER_COLOR_COUNT, userColor } from './userColor'

describe('userColor', () => {
  it('always gives the same user the same color', () => {
    expect(userColor('3f2c9a10-0000-4000-8000-000000000001')).toBe(
      userColor('3f2c9a10-0000-4000-8000-000000000001'),
    )
  })

  it('spreads users over the whole palette', () => {
    const colors = new Set(
      Array.from({ length: 200 }, (_, i) => userColor(crypto.randomUUID() + i)),
    )

    expect(colors.size).toBe(USER_COLOR_COUNT)
    colors.forEach((color) => expect(color).toBeGreaterThanOrEqual(0))
  })
})
