using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Conversations;
using SharpChat.Application.Diagnostics;
using SharpChat.Domain.Attachments;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Attachments;

public sealed class AttachmentService(
    IAttachmentRepository attachments,
    IFileStorage storage,
    MembershipService membership,
    ICurrentUser currentUser,
    IOptions<ChatOptions> options,
    ChatMetrics metrics,
    TimeProvider time)
{
    public async Task<Result<AttachmentDto>> UploadAsync(
        Guid conversationId, string? fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanSeek)
        {
            throw new ArgumentException("The upload stream must be seekable.", nameof(content));
        }

        var uploaderId = currentUser.RequiredUserId();
        if (!await membership.IsMemberAsync(conversationId, uploaderId, cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        if (length <= 0)
        {
            return AttachmentErrors.Empty;
        }

        var maxBytes = options.Value.MaxAttachmentBytes;
        if (length > maxBytes)
        {
            return AttachmentErrors.TooLarge(maxBytes);
        }

        var name = FileNames.Sanitize(fileName);
        var header = new byte[(int)Math.Min(FileTypeDetector.HeaderSize, length)];
        await content.ReadExactlyAsync(header, cancellationToken);
        content.Seek(0, SeekOrigin.Begin);

        var type = FileTypeDetector.Detect(header, name, isWholeFile: length <= header.Length);
        if (type is null)
        {
            return AttachmentErrors.UnsupportedType;
        }

        var id = Guid.CreateVersion7();
        var key = $"{conversationId:N}/{id:N}";
        await storage.SaveAsync(key, content, cancellationToken);

        var attachment = Attachment.Create(
            id, conversationId, uploaderId, name, type.ContentType, type.Kind, length, key, time.GetUtcNow());
        attachments.Add(attachment);
        try
        {
            await attachments.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        metrics.AttachmentUploaded(type.Kind.ToString().ToLowerInvariant(), length);
        return AttachmentDto.From(attachment);
    }

    public async Task<Result<AttachmentContent>> OpenAsync(
        Guid conversationId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId();
        if (!await membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        var attachment = await attachments.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null || attachment.ConversationId != conversationId || !attachment.CanBeReadBy(userId))
        {
            return AttachmentErrors.NotFound;
        }

        var stream = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return stream is null
            ? AttachmentErrors.NotFound
            : new AttachmentContent(AttachmentDto.From(attachment), stream);
    }
}
