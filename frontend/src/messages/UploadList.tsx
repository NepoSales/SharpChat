import { formatSize } from '../api/attachments'
import styles from './UploadList.module.css'
import type { Upload } from './useUploads'

type Props = {
  uploads: Upload[]
  onRemove: (key: string) => void
}

export function UploadList({ uploads, onRemove }: Props) {
  if (uploads.length === 0) return null

  return (
    <ul className={styles.list} aria-label="Attachments">
      {uploads.map((upload) => (
        <li key={upload.key} className={styles.item} data-status={upload.status}>
          {upload.previewUrl ? (
            <img src={upload.previewUrl} alt="" className={styles.preview} />
          ) : (
            <span className={styles.icon} aria-hidden="true">
              FILE
            </span>
          )}
          <span className={styles.details}>
            <span className={styles.name} title={upload.name}>
              {upload.name}
            </span>
            <span className={styles.meta}>
              {upload.status === 'failed'
                ? upload.error
                : upload.status === 'uploading'
                  ? `${Math.round(upload.progress * 100)}% of ${formatSize(upload.size)}`
                  : formatSize(upload.size)}
            </span>
          </span>
          <button
            type="button"
            className={styles.remove}
            onClick={() => onRemove(upload.key)}
            aria-label={`Remove ${upload.name}`}
          >
            ×
          </button>
          {upload.status === 'uploading' && (
            <progress
              className={styles.progress}
              value={upload.progress}
              max={1}
              aria-label={`Uploading ${upload.name}`}
            />
          )}
        </li>
      ))}
    </ul>
  )
}
