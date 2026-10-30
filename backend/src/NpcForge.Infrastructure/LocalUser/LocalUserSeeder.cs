using Microsoft.AspNetCore.Identity;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Infrastructure.LocalUser;

/// <summary>Cria a conta da versão local (vazia, sem NPCs) se ela ainda não existir.</summary>
public class LocalUserSeeder(UserManager<AppUser> users)
{
    public const string Email = "mestre@npcforge.local";
    public const string Password = "senha1234";

    public async Task EnsureCreatedAsync()
    {
        if (await users.FindByEmailAsync(Email) is not null) return;

        var user = new AppUser { UserName = Email, Email = Email, DisplayName = "Mestre", EmailConfirmed = true };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Não foi possível criar o usuário local: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
