import { ApiError } from './errors'
import { API_BASE, getAccessToken, refreshSession } from './session'

export type AttachmentKind = 'Image' | 'File'

export type Attachment = {
  id: string
  fileName: string
  contentType: string
  kind: AttachmentKind
  sizeBytes: number
}

export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024
export const MAX_ATTACHMENTS = 4

type UploadResult = { status: number; body: string; contentType: string }

export async function uploadAttachment(
  conversationId: string,
  file: File,
  onProgress: (fraction: number) => void,
  signal: AbortSignal,
): Promise<Attachment> {
  const path = `${API_BASE}/conversations/${conversationId}/attachments`
  let result = await post(path, file, await getAccessToken(), onProgress, signal)
  if (result.status === 401) {
    const refreshed = await refreshSession()
    if (refreshed) result = await post(path, file, refreshed, onProgress, signal)
  }

  if (result.status === 413) {
    throw new ApiError(413, { detail: 'The file is too large.' })
  }
  const response = new Response(result.body, {
    status: result.status,
    headers: { 'Content-Type': result.contentType },
  })
  if (!response.ok) throw await ApiError.fromResponse(response)
  return (await response.json()) as Attachment
}

function post(
  path: string,
  file: File,
  token: string | null,
  onProgress: (fraction: number) => void,
  signal: AbortSignal,
): Promise<UploadResult> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest()
    const abort = () => request.abort()
    request.open('POST', path)
    request.setRequestHeader('Accept', 'application/json')
    if (token) request.setRequestHeader('Authorization', `Bearer ${token}`)
    request.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress(event.loaded / event.total)
    }
    request.onload = () => {
      signal.removeEventListener('abort', abort)
      resolve({
        status: request.status,
        body: request.responseText,
        contentType: request.getResponseHeader('Content-Type') ?? '',
      })
    }
    request.onerror = () => {
      signal.removeEventListener('abort', abort)
      reject(new TypeError('Network error'))
    }
    request.onabort = () => reject(new DOMException('Upload cancelled', 'AbortError'))
    signal.addEventListener('abort', abort, { once: true })

    const form = new FormData()
    form.append('file', file, file.name)
    request.send(form)
  })
}

async function fetchContent(conversationId: string, attachmentId: string): Promise<Blob> {
  const path = `${API_BASE}/conversations/${conversationId}/attachments/${attachmentId}`
  const get = (token: string | null) =>
    fetch(path, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      credentials: 'same-origin',
    })

  let response = await get(await getAccessToken())
  if (response.status === 401) {
    const refreshed = await refreshSession()
    if (refreshed) response = await get(refreshed)
  }
  if (!response.ok) throw await ApiError.fromResponse(response)
  return response.blob()
}

const imageUrls = new Map<string, Promise<string>>()

export function attachmentImageUrl(conversationId: string, attachmentId: string): Promise<string> {
  let url = imageUrls.get(attachmentId)
  if (!url) {
    url = fetchContent(conversationId, attachmentId).then((blob) => URL.createObjectURL(blob))
    url.catch(() => imageUrls.delete(attachmentId))
    imageUrls.set(attachmentId, url)
  }
  return url
}

export async function downloadAttachment(conversationId: string, attachment: Attachment) {
  const blob = await fetchContent(conversationId, attachment.id)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = attachment.fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}

export function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(bytes < 10 * 1024 ? 1 : 0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
