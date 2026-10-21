using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Conversations;
using SharpChat.Domain.Conversations;

namespace SharpChat.Infrastructure.Persistence.Queries;

internal sealed class ConversationQueries(SharpChatDbContext db) : IConversationQueries
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Conversations, m => m.ConversationId, c => c.Id, (m, c) => new
            {
                Member = m,
                Conversation = c,
                LastMessageAt = db.Messages.Where(msg => msg.ConversationId == c.Id).Max(msg => (DateTimeOffset?)msg.SentAt),
                LastSeq = db.Messages.Where(msg => msg.ConversationId == c.Id).Max(msg => (long?)msg.Seq) ?? 0,
                UnreadCount = db.Messages
                    .Where(msg => msg.ConversationId == c.Id
                        && msg.Seq > m.LastReadSeq
                        && msg.SenderId != userId
                        && msg.ParentMessageId == null
                        && msg.DeletedAt == null)
                    .Take(ChatOptions.UnreadCountCap)
                    .Count(),
            })
            .OrderByDescending(x => x.LastMessageAt ?? x.Conversation.UpdatedAt)
            .Select(x => new ConversationSummaryDto(
                x.Conversation.Id,
                x.Conversation.Name,
                x.Conversation.Members.Count,
                x.Member.Role,
                x.Conversation.CreatedAt,
                x.Conversation.UpdatedAt,
                x.LastMessageAt,
                x.LastSeq,
                x.Member.LastDeliveredSeq,
                x.Member.LastReadSeq,
                x.UnreadCount))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid conversationId, CancellationToken cancellationToken) =>
        await Members(conversationId).ToListAsync(cancellationToken);

    public async Task<ConversationDto?> GetDetailsAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Id, c.Name, c.CreatedBy, c.CreatedAt, c.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        var members = await Members(conversationId).ToListAsync(cancellationToken);

        return new ConversationDto(
            conversation.Id, conversation.Name, conversation.CreatedBy, conversation.CreatedAt, conversation.UpdatedAt, members);
    }

    public Task<MemberDto?> GetMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        Members(conversationId, userId).FirstOrDefaultAsync(cancellationToken);

    public async Task<ConversationRole?> GetRoleAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        await db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.UserId == userId)
            .Select(m => (ConversationRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<MemberDto> Members(Guid conversationId, Guid? userId = null) =>
        db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && (userId == null || m.UserId == userId))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { Member = m, u.UserName })
            .OrderBy(x => x.Member.JoinedAt)
            .Select(x => new MemberDto(
                x.Member.UserId,
                x.UserName!,
                x.Member.Role,
                x.Member.JoinedAt,
                x.Member.LastDeliveredSeq,
                x.Member.LastReadSeq));
}
