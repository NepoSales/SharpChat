using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpChat.Domain.Conversations;
using SharpChat.Infrastructure.Identity;

namespace SharpChat.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Conversation.NameMaxLength).IsRequired();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every membership change also updates the conversation row, so xmin makes concurrent changes
        // (e.g. two adds racing past the member limit) fail instead of both succeeding.
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class ConversationMemberConfiguration : IEntityTypeConfiguration<ConversationMember>
{
    public void Configure(EntityTypeBuilder<ConversationMember> builder)
    {
        builder.ToTable("ConversationMembers");
        builder.HasKey(m => new { m.ConversationId, m.UserId });

        // "Which conversations is this user in?" runs on every connection and listing.
        builder.HasIndex(m => m.UserId);

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(m => m.CanManageMembers);
    }
}
