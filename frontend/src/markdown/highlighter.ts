import type { Root } from 'hast'
import type { BundledLanguage, Highlighter } from 'shiki'

const THEME = 'github-dark-default'
const THEME_BACKGROUND = '#0d1117'
const SURFACE = '#171a21'

let shiki: Promise<{ highlighter: Highlighter; languages: ReadonlySet<string> }> | null = null

function loadShiki() {
  shiki ??= import('shiki').then(async (module) => ({
    highlighter: await module.createHighlighter({
      themes: [THEME],
      langs: [],
      engine: module.createJavaScriptRegexEngine(),
    }),
    languages: new Set(Object.keys(module.bundledLanguages)),
  }))
  return shiki
}

export async function highlight(code: string, language: string): Promise<Root | null> {
  const lang = language.toLowerCase() as BundledLanguage
  const { highlighter, languages } = await loadShiki()
  if (!languages.has(lang)) return null

  if (!highlighter.getLoadedLanguages().includes(lang)) {
    await highlighter.loadLanguage(lang)
  }
  return highlighter.codeToHast(code, {
    lang,
    theme: THEME,
    colorReplacements: { [THEME_BACKGROUND]: SURFACE },
  })
}
