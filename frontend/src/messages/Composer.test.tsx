import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Attachment } from '../api/attachments'
import { ActivityContext, type ActivityContextValue } from '../realtime/ActivityContext'
import { RealtimeContext, type RealtimeContextValue } from '../realtime/RealtimeContext'
import { Composer } from './Composer'

const upload = vi.hoisted(() => vi.fn())

vi.mock('../api/attachments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/attachments')>()),
  uploadAttachment: upload,
}))

const sendMessage = vi.fn()

const realtime = {
  state: 'connected',
  sendMessage,
  retryMessage: vi.fn(),
  editMessage: vi.fn(async () => null),
  deleteMessage: vi.fn(async () => null),
  markRead: vi.fn(),
  on: vi.fn(() => () => undefined),
  getOnlineMembers: vi.fn(async () => []),
  sendTyping: vi.fn(),
} satisfies RealtimeContextValue

const activity = { notifyTyping: vi.fn() } as unknown as ActivityContextValue

function wrapper({ children }: { children: ReactNode }) {
  return (
    <RealtimeContext value={realtime}>
      <ActivityContext value={activity}>{children}</ActivityContext>
    </RealtimeContext>
  )
}

function attachment(name: string): Attachment {
  return { id: `id-${name}`, fileName: name, contentType: 'text/plain', kind: 'File', sizeBytes: 3 }
}

function file(name: string, size = 3) {
  return new File([new Uint8Array(size)], name, { type: 'text/plain' })
}

function renderComposer() {
  render(<Composer conversationId="c1" label="Message #team" />, { wrapper })
  return {
    input: screen.getByLabelText('Message #team'),
    fileInput: screen.getByTestId('file-input') as HTMLInputElement,
  }
}

describe('Composer attachments', () => {
  beforeEach(() => {
    upload.mockReset()
    sendMessage.mockReset()
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
  })

  it('uploads picked files and sends them with the message, even without text', async () => {
    upload.mockImplementation(async (_c: string, f: File) => attachment(f.name))
    const user = userEvent.setup()
    const { input, fileInput } = renderComposer()

    await user.upload(fileInput, [file('build.log'), file('notes.md')])
    await screen.findAllByText('3 B')
    await user.type(input, '{Enter}')

    expect(upload).toHaveBeenCalledTimes(2)
    expect(sendMessage).toHaveBeenCalledWith('c1', '', null, [
      attachment('build.log'),
      attachment('notes.md'),
    ])
    expect(screen.queryByText('build.log')).not.toBeInTheDocument()
  })

  it('does not send while an upload is still running', async () => {
    let finish: (value: Attachment) => void = () => undefined
    upload.mockImplementation(() => new Promise<Attachment>((resolve) => (finish = resolve)))
    const user = userEvent.setup()
    const { input, fileInput } = renderComposer()

    await user.upload(fileInput, file('slow.log'))
    await user.type(input, 'caption{Enter}')
    expect(sendMessage).not.toHaveBeenCalled()

    await act(async () => finish(attachment('slow.log')))
    await user.type(input, '{Enter}')
    expect(sendMessage).toHaveBeenCalledWith('c1', 'caption', null, [attachment('slow.log')])
  })

  it('rejects files over 10 MB without uploading them and blocks sending until removed', async () => {
    const user = userEvent.setup()
    const { input, fileInput } = renderComposer()

    await user.upload(fileInput, file('huge.zip', 10 * 1024 * 1024 + 1))
    await user.type(input, 'hi{Enter}')

    expect(upload).not.toHaveBeenCalled()
    expect(screen.getByText('Larger than 10 MB.')).toBeInTheDocument()
    expect(sendMessage).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Remove huge.zip' }))
    await user.type(input, '{Enter}')
    expect(sendMessage).toHaveBeenCalledWith('c1', 'hi', null, [])
  })

  it('keeps at most four files and says why', async () => {
    upload.mockImplementation(async (_c: string, f: File) => attachment(f.name))
    const user = userEvent.setup()
    const { fileInput } = renderComposer()

    await user.upload(
      fileInput,
      ['a', 'b', 'c', 'd', 'e'].map((name) => file(`${name}.txt`)),
    )

    await waitFor(() => expect(upload).toHaveBeenCalledTimes(4))
    expect(screen.getByRole('status')).toHaveTextContent('Up to 4 files per message.')
    expect(screen.queryByText('e.txt')).not.toBeInTheDocument()
  })

  it('shows the server error when an upload fails', async () => {
    upload.mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    const { fileInput } = renderComposer()

    await user.upload(fileInput, file('a.txt'))

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
  })

  it('accepts pasted files', async () => {
    upload.mockImplementation(async (_c: string, f: File) => attachment(f.name))
    const user = userEvent.setup()
    const { input } = renderComposer()

    await user.click(input)
    await user.paste({
      files: [file('pasted.txt')],
      items: [],
      types: ['Files'],
      getData: () => '',
    } as unknown as DataTransfer)

    expect(await screen.findByText('pasted.txt')).toBeInTheDocument()
    expect(upload).toHaveBeenCalledOnce()
  })
})
