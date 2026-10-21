using Microsoft.AspNetCore.Identity;

namespace SharpChat.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public DateTimeOffset CreatedAt { get; set; }
}
