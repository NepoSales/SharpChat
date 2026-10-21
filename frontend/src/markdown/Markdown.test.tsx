import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Markdown } from './Markdown'

function renderMarkdown(content: string) {
  return render(<Markdown content={content} />).container
}

describe('Markdown', () => {
  it('renders basic markdown', () => {
    const container = renderMarkdown('**bold**, _italic_, `inline` and ~~gone~~')

    expect(container.querySelector('strong')).toHaveTextContent('bold')
    expect(container.querySelector('em')).toHaveTextContent('italic')
    expect(container.querySelector('code')).toHaveTextContent('inline')
    expect(container.querySelector('del')).toHaveTextContent('gone')
  })

  it('renders GitHub flavored lists and tables', () => {
    const container = renderMarkdown('- one\n- two\n\n| a | b |\n|---|---|\n| 1 | 2 |')

    expect(container.querySelectorAll('li')).toHaveLength(2)
    expect(container.querySelector('table')).toBeInTheDocument()
  })

  it('highlights fenced code blocks with the declared language', async () => {
    const container = renderMarkdown('```csharp\nvar answer = 42;\n```')

    expect(screen.getByText('csharp')).toBeInTheDocument()
    expect(container.querySelector('pre')).toHaveTextContent('var answer = 42;')
    await screen.findByText('var', { selector: 'span' }, { timeout: 5000 })
    expect(container.querySelector('pre')).toHaveTextContent('var answer = 42;')
  })

  it('shows code in an unknown language as plain text', () => {
    const container = renderMarkdown('```not-a-language\nhello\n```')

    expect(container.querySelector('pre')).toHaveTextContent('hello')
  })

  it('opens links in a new tab without leaking the opener', () => {
    renderMarkdown('[docs](https://example.com)')

    const link = screen.getByRole('link', { name: 'docs' })
    expect(link).toHaveAttribute('href', 'https://example.com')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer nofollow')
  })
})

describe('Markdown sanitization', () => {
  it('never creates elements from raw HTML', () => {
    const container = renderMarkdown(
      '<script>alert(1)</script>\n\n<img src=x onerror="alert(1)">\n\n<iframe src="https://evil.example"></iframe>\n\n<b onclick="alert(1)">hi</b>',
    )

    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('img')).toBeNull()
    expect(container.querySelector('iframe')).toBeNull()
    expect(container.querySelector('b')).toBeNull()
    expect(container.querySelector('[onerror], [onclick]')).toBeNull()
  })

  it('shows raw HTML as literal text', () => {
    const container = renderMarkdown('wrap it in a <div> please\n\n<b>not bold</b>')

    expect(container).toHaveTextContent('wrap it in a <div> please')
    expect(container).toHaveTextContent('<b>not bold</b>')
    expect(container.querySelector('.markdown div, .markdown b')).toBeNull()
  })

  it.each([
    '[click](javascript:alert(1))',
    '[click](JaVaScRiPt:alert(1))',
    '[click](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)',
    '[click](vbscript:msgbox(1))',
  ])('neutralizes dangerous link protocols: %s', (content) => {
    const container = renderMarkdown(content)

    const href = container.querySelector('a')?.getAttribute('href') ?? ''
    expect(href).not.toMatch(/^\s*(javascript|data|vbscript):/i)
  })

  it('renders images as links so remote images are never loaded', () => {
    const container = renderMarkdown('![tracking pixel](https://evil.example/pixel.png)')

    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByRole('link', { name: 'tracking pixel' })).toHaveAttribute(
      'href',
      'https://evil.example/pixel.png',
    )
  })

  it('does not execute code inside code blocks', () => {
    const container = renderMarkdown('```html\n<script>alert(1)</script>\n```')

    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('pre')).toHaveTextContent('<script>alert(1)</script>')
  })
})
