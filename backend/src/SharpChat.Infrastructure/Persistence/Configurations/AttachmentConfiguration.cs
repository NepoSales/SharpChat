using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpChat.Application.Attachments;
using SharpChat.Domain.Attachments;
using SharpChat.Domain.Conversations;
using SharpChat.Domain.Messages;
using SharpChat.Infrastructure.Identity;

namespace SharpChat.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.FileName).HasMaxLength(FileNames.MaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.StorageKey).HasMaxLength(100).IsRequired();
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(a => a.IsDeleted);
        builder.Ignore(a => a.IsPending);

        builder.HasIndex(a => a.MessageId);
        builder.HasIndex(a => a.UploadedAt).HasFilter("\"MessageId\" IS NULL");
        builder.HasIndex(a => a.DeletedAt).HasFilter("\"DeletedAt\" IS NOT NULL");

        builder.HasOne<Conversation>().WithMany().HasForeignKey(a => a.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UploaderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Message>().WithMany().HasForeignKey(a => a.MessageId).OnDelete(DeleteBehavior.Restrict);
    }
}
