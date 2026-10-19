using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharpChat.Domain.Auth;
using SharpChat.Domain.Conversations;
using SharpChat.Infrastructure.Identity;

namespace SharpChat.Infrastructure.Persistence;

/// <summary>
/// Uses <see cref="IdentityUserContext{TUser,TKey}"/> (no Identity roles): roles in SharpChat are per conversation.
/// </summary>
public sealed class SharpChatDbContext(DbContextOptions<SharpChatDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.ApplyConfigurationsFromAssembly(typeof(SharpChatDbContext).Assembly);
    }
}
