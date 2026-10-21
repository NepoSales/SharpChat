import type { Element, ElementContent } from 'hast'
import ReactMarkdown, { type Components } from 'react-markdown'
import rehypeSanitize from 'rehype-sanitize'
import remarkGfm from 'remark-gfm'
import { CodeBlock } from './CodeBlock'
import styles from './Markdown.module.css'
import { remarkHtmlAsText } from './remarkHtmlAsText'

const components: Components = {
  pre: ({ node }) => {
    const code = node?.children.find(
      (child): child is Element => child.type === 'element' && child.tagName === 'code',
    )
    return <CodeBlock code={textOf(code).replace(/\n$/, '')} language={languageOf(code)} />
  },
  a: ({ href, children }) => (
    <a href={href} target="_blank" rel="noopener noreferrer nofollow">
      {children}
    </a>
  ),
  img: ({ src, alt }) =>
    typeof src === 'string' && src ? (
      <a href={src} target="_blank" rel="noopener noreferrer nofollow">
        {alt || src}
      </a>
    ) : (
      <>{alt}</>
    ),
}

export function Markdown({ content }: { content: string }) {
  return (
    <div className={styles.markdown}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkHtmlAsText]}
        rehypePlugins={[rehypeSanitize]}
        components={components}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}

function textOf(node: ElementContent | undefined): string {
  if (!node) return ''
  if (node.type === 'text') return node.value
  if (node.type === 'element') return node.children.map(textOf).join('')
  return ''
}

function languageOf(code: Element | undefined): string | null {
  const classNames = code?.properties.className
  if (!Array.isArray(classNames)) return null
  const language = classNames
    .map(String)
    .find((name) => name.startsWith('language-'))
    ?.slice('language-'.length)
  return language || null
}
