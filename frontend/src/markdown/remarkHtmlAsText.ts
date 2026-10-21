import type { Nodes, Root } from 'mdast'

export function remarkHtmlAsText() {
  return (tree: Root) => {
    convert(tree)
  }
}

function convert(node: Nodes) {
  if (!('children' in node)) return
  node.children = node.children.map((child) =>
    child.type === 'html' ? { type: 'text', value: child.value, position: child.position } : child,
  ) as typeof node.children
  node.children.forEach(convert)
}
