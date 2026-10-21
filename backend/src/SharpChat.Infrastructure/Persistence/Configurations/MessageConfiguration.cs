using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpChat.Domain.Conversations;
using SharpChat.Domain.Messages;
using SharpChat.Infrastructure.Identity;

namespace SharpChat.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Content).HasColumnType("text").IsRequired();

        builder.HasIndex(m => new { m.ConversationId, m.Seq }).IsUnique();
        builder.HasIndex(m => new { m.ConversationId, m.ChangeSeq }).IsUnique();
        builder.HasIndex(m => new { m.ParentMessageId, m.Seq });
        builder.Ignore(m => m.IsDeleted);

        builder.HasOne<Conversation>().WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Message>().WithMany().HasForeignKey(m => m.ParentMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}
