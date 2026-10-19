using Microsoft.EntityFrameworkCore;

namespace SharpChat.Infrastructure.Persistence;

public sealed class SharpChatDbContext(DbContextOptions<SharpChatDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SharpChatDbContext).Assembly);
    }
}
