namespace NpcForge.Core.LocalUser;

/// <summary>
/// Usuário fixo da versão local (e-mail e senha conhecidos, no README). Só vale em Development:
/// a API se recusa a iniciar com ele ligado em qualquer outro ambiente.
/// </summary>
public class LocalUserOptions
{
    public const string Section = "LocalUser";

    public bool Enabled { get; set; }
}
