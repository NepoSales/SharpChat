import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Attachment } from '../api/attachments'
import { AttachmentList } from './AttachmentList'

const api = vi.hoisted(() => ({ imageUrl: vi.fn(), download: vi.fn() }))

vi.mock('../api/attachments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/attachments')>()),
  attachmentImageUrl: api.imageUrl,
  downloadAttachment: api.download,
}))

const image: Attachment = {
  id: 'img',
  fileName: 'screenshot.png',
  contentType: 'image/png',
  kind: 'Image',
  sizeBytes: 2048,
}

const pdf: Attachment = {
  id: 'pdf',
  fileName: 'spec.pdf',
  contentType: 'application/pdf',
  kind: 'File',
  sizeBytes: 3 * 1024 * 1024,
}

describe('AttachmentList', () => {
  beforeEach(() => {
    api.imageUrl.mockReset()
    api.download.mockReset()
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.open = true
    })
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.open = false
    })
  })

  it('shows images from blob URLs and files as cards with their size', async () => {
    api.imageUrl.mockResolvedValue('blob:image')
    render(<AttachmentList conversationId="c1" attachments={[image, pdf]} />)

    const thumb = await screen.findByRole('img', { name: 'screenshot.png' })
    expect(thumb).toHaveAttribute('src', 'blob:image')
    expect(api.imageUrl).toHaveBeenCalledWith('c1', 'img')
    expect(screen.getByText('spec.pdf')).toBeInTheDocument()
    expect(screen.getByText('3.0 MB')).toBeInTheDocument()
    expect(screen.getByText('PDF')).toBeInTheDocument()
  })

  it('opens the image full size and downloads files on request', async () => {
    api.imageUrl.mockResolvedValue('blob:image')
    api.download.mockResolvedValue(undefined)
    const user = userEvent.setup()
    render(<AttachmentList conversationId="c1" attachments={[image, pdf]} />)

    await user.click(await screen.findByRole('button', { name: 'Open image screenshot.png' }))
    expect(HTMLDialogElement.prototype.showModal).toHaveBeenCalled()
    expect(screen.getAllByRole('img', { name: 'screenshot.png', hidden: true })).toHaveLength(2)

    await user.click(screen.getByRole('button', { name: 'Download spec.pdf' }))
    expect(api.download).toHaveBeenCalledWith('c1', pdf)
  })

  it('says when an image cannot be loaded', async () => {
    api.imageUrl.mockRejectedValue(new Error('gone'))
    render(<AttachmentList conversationId="c1" attachments={[image]} />)

    expect(await screen.findByText(/image unavailable/)).toBeInTheDocument()
  })
})
