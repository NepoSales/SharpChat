import type { Root } from 'hast'
import { toJsxRuntime } from 'hast-util-to-jsx-runtime'
import { useEffect, useState } from 'react'
import { Fragment, jsx, jsxs } from 'react/jsx-runtime'
import styles from './CodeBlock.module.css'
import { highlight } from './highlighter'

type Props = {
  code: string
  language: string | null
}

export function CodeBlock({ code, language }: Props) {
  const [highlighted, setHighlighted] = useState<{ source: string; tree: Root } | null>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!language) return
    let cancelled = false
    highlight(code, language)
      .then((tree) => {
        if (!cancelled && tree) setHighlighted({ source: code, tree })
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [code, language])

  async function copy() {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      setCopied(false)
    }
  }

  const tree = highlighted?.source === code ? highlighted.tree : null

  return (
    <div className={styles.block}>
      <div className={styles.toolbar}>
        <span className={styles.language}>{language ?? 'text'}</span>
        <button type="button" className={styles.copy} onClick={() => void copy()}>
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      {tree ? (
        toJsxRuntime(tree, { Fragment, jsx, jsxs })
      ) : (
        <pre>
          <code>{code}</code>
        </pre>
      )}
    </div>
  )
}
