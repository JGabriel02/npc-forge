using Microsoft.AspNetCore.Identity;

namespace NpcForge.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}
