using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Domain.Conversations;

namespace SharpChat.Infrastructure.Persistence.Repositories;

internal sealed class ConversationRepository(SharpChatDbContext db) : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken) =>
        db.Conversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    public void Add(Conversation conversation) => db.Conversations.Add(conversation);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The conversation was modified by another request.", ex);
        }
    }
}
