using SharpChat.Domain.Common;

namespace SharpChat.Domain.Attachments;

public sealed class Attachment
{
    private Attachment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UploaderId { get; private set; }
    public Guid? MessageId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public AttachmentKind Kind { get; private set; }
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public bool IsPending => MessageId is null;

    public static Attachment Create(
        Guid id,
        Guid conversationId,
        Guid uploaderId,
        string fileName,
        string contentType,
        AttachmentKind kind,
        long sizeBytes,
        string storageKey,
        DateTimeOffset uploadedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new Attachment
        {
            Id = id,
            ConversationId = conversationId,
            UploaderId = uploaderId,
            FileName = fileName,
            ContentType = contentType,
            Kind = kind,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedAt = uploadedAt,
        };
    }

    public Result LinkTo(Guid messageId, Guid conversationId, Guid senderId)
    {
        if (IsDeleted || !IsPending || UploaderId != senderId || ConversationId != conversationId)
        {
            return AttachmentErrors.Unavailable;
        }

        MessageId = messageId;
        return Result.Success();
    }

    public bool CanBeReadBy(Guid userId) => !IsDeleted && (!IsPending || UploaderId == userId);

    public void Delete(DateTimeOffset now) => DeletedAt ??= now;
}
