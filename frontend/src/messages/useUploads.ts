import { useEffect, useRef, useState } from 'react'
import {
  MAX_ATTACHMENT_BYTES,
  MAX_ATTACHMENTS,
  uploadAttachment,
  type Attachment,
} from '../api/attachments'
import { errorMessage } from '../api/errors'

export type Upload = {
  key: string
  name: string
  size: number
  previewUrl: string | null
  progress: number
  status: 'uploading' | 'done' | 'failed'
  error?: string
  attachment?: Attachment
}

type Live = { controller: AbortController; previewUrl: string | null }

const IMAGE_TYPES = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp'])

export function useUploads(conversationId: string) {
  const [uploads, setUploads] = useState<Upload[]>([])
  const [notice, setNotice] = useState<string | null>(null)
  const liveRef = useRef(new Map<string, Live>())

  useEffect(() => {
    const live = liveRef.current
    return () => {
      live.forEach(({ controller, previewUrl }) => {
        controller.abort()
        if (previewUrl) URL.revokeObjectURL(previewUrl)
      })
      live.clear()
    }
  }, [])

  function update(key: string, patch: Partial<Upload>) {
    setUploads((current) =>
      current.map((upload) => (upload.key === key ? { ...upload, ...patch } : upload)),
    )
  }

  function add(files: File[]) {
    if (files.length === 0) return
    const room = MAX_ATTACHMENTS - uploads.length
    setNotice(files.length > room ? `Up to ${MAX_ATTACHMENTS} files per message.` : null)

    const added = files.slice(0, Math.max(room, 0)).map((file) => start(file))
    setUploads((current) => [...current, ...added])
  }

  function start(file: File): Upload {
    const key = crypto.randomUUID()
    const controller = new AbortController()
    const previewUrl = IMAGE_TYPES.has(file.type) ? URL.createObjectURL(file) : null
    liveRef.current.set(key, { controller, previewUrl })

    const upload: Upload = {
      key,
      name: file.name || 'pasted image',
      size: file.size,
      previewUrl,
      progress: 0,
      status: 'uploading',
    }

    if (file.size > MAX_ATTACHMENT_BYTES) {
      return { ...upload, status: 'failed', error: 'Larger than 10 MB.' }
    }

    uploadAttachment(
      conversationId,
      file,
      (progress) => update(key, { progress }),
      controller.signal,
    ).then(
      (attachment) => update(key, { status: 'done', progress: 1, attachment }),
      (failure: unknown) => {
        if (failure instanceof DOMException && failure.name === 'AbortError') return
        update(key, { status: 'failed', error: errorMessage(failure) })
      },
    )
    return upload
  }

  function release(key: string) {
    const live = liveRef.current.get(key)
    if (!live) return
    live.controller.abort()
    if (live.previewUrl) URL.revokeObjectURL(live.previewUrl)
    liveRef.current.delete(key)
  }

  function remove(key: string) {
    release(key)
    setNotice(null)
    setUploads((current) => current.filter((upload) => upload.key !== key))
  }

  function take(): Attachment[] {
    const attachments = uploads.flatMap((upload) => (upload.attachment ? [upload.attachment] : []))
    uploads.forEach((upload) => release(upload.key))
    setUploads([])
    setNotice(null)
    return attachments
  }

  const ready = uploads.length > 0 && uploads.every((upload) => upload.status === 'done')
  const blocking = uploads.some((upload) => upload.status !== 'done')

  return { uploads, notice, add, remove, take, ready, blocking }
}
