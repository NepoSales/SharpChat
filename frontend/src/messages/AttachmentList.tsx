import { useEffect, useRef, useState } from 'react'
import {
  attachmentImageUrl,
  downloadAttachment,
  formatSize,
  type Attachment,
} from '../api/attachments'
import { errorMessage } from '../api/errors'
import styles from './AttachmentList.module.css'

type Props = {
  conversationId: string
  attachments: Attachment[]
}

export function AttachmentList({ conversationId, attachments }: Props) {
  const [viewing, setViewing] = useState<Attachment | null>(null)
  const images = attachments.filter((attachment) => attachment.kind === 'Image')
  const files = attachments.filter((attachment) => attachment.kind !== 'Image')

  return (
    <div className={styles.list}>
      {images.length > 0 && (
        <div className={styles.images}>
          {images.map((image) => (
            <ImageThumb
              key={image.id}
              conversationId={conversationId}
              image={image}
              onOpen={() => setViewing(image)}
            />
          ))}
        </div>
      )}
      {files.map((file) => (
        <FileCard key={file.id} conversationId={conversationId} file={file} />
      ))}
      <ImageViewer
        conversationId={conversationId}
        image={viewing}
        onClose={() => setViewing(null)}
      />
    </div>
  )
}

type ImageState = { id: string; url: string | null; failed: boolean }

function useImageUrl(conversationId: string, imageId: string | null) {
  const [state, setState] = useState<ImageState | null>(null)

  useEffect(() => {
    if (!imageId) return
    let active = true
    attachmentImageUrl(conversationId, imageId).then(
      (url) => active && setState({ id: imageId, url, failed: false }),
      () => active && setState({ id: imageId, url: null, failed: true }),
    )
    return () => {
      active = false
    }
  }, [conversationId, imageId])

  return state?.id === imageId ? state : null
}

function ImageThumb({
  conversationId,
  image,
  onOpen,
}: {
  conversationId: string
  image: Attachment
  onOpen: () => void
}) {
  const state = useImageUrl(conversationId, image.id)

  if (state?.failed) {
    return <span className={styles.imageError}>[image unavailable] {image.fileName}</span>
  }

  return (
    <button
      type="button"
      className={styles.thumb}
      onClick={onOpen}
      title={`${image.fileName} · ${formatSize(image.sizeBytes)}`}
      aria-label={`Open image ${image.fileName}`}
      data-loading={!state?.url}
    >
      {state?.url && <img src={state.url} alt={image.fileName} className={styles.thumbImage} />}
    </button>
  )
}

function ImageViewer({
  conversationId,
  image,
  onClose,
}: {
  conversationId: string
  image: Attachment | null
  onClose: () => void
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const state = useImageUrl(conversationId, image?.id ?? null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (image && !dialog.open) dialog.showModal()
    if (!image && dialog.open) dialog.close()
  }, [image])

  return (
    <dialog
      ref={ref}
      className={styles.viewer}
      aria-label={image ? `Image ${image.fileName}` : 'Image'}
      onClose={onClose}
      onClick={(event) => {
        if (event.target === ref.current) onClose()
      }}
    >
      {image && (
        <figure className={styles.figure}>
          {state?.url && <img src={state.url} alt={image.fileName} className={styles.full} />}
          <figcaption className={styles.caption}>
            <span className={styles.captionName}>{image.fileName}</span>
            <span>{formatSize(image.sizeBytes)}</span>
            <button
              type="button"
              className={styles.link}
              onClick={() => void downloadAttachment(conversationId, image)}
            >
              download
            </button>
            <button type="button" className={styles.link} onClick={onClose}>
              close
            </button>
          </figcaption>
        </figure>
      )}
    </dialog>
  )
}

function extensionLabel(fileName: string) {
  const dot = fileName.lastIndexOf('.')
  const extension = dot > 0 ? fileName.slice(dot + 1) : ''
  return (extension || 'file').slice(0, 4).toUpperCase()
}

function FileCard({ conversationId, file }: { conversationId: string; file: Attachment }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function download() {
    setBusy(true)
    setError(null)
    try {
      await downloadAttachment(conversationId, file)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={styles.file}>
      <span className={styles.badge} aria-hidden="true">
        {extensionLabel(file.fileName)}
      </span>
      <span className={styles.fileName} title={file.fileName}>
        {file.fileName}
      </span>
      <span className={styles.size}>{formatSize(file.sizeBytes)}</span>
      <button
        type="button"
        className={styles.link}
        onClick={() => void download()}
        disabled={busy}
        aria-label={`Download ${file.fileName}`}
      >
        {busy ? 'downloading…' : 'download'}
      </button>
      {error && (
        <span className={styles.fileError} role="alert">
          {error}
        </span>
      )}
    </div>
  )
}
