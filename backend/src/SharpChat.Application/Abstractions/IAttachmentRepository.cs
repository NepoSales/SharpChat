using SharpChat.Domain.Attachments;

namespace SharpChat.Application.Abstractions;

public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(Guid attachmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Attachment>> GetByIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Attachment>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken);

    void Add(Attachment attachment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
