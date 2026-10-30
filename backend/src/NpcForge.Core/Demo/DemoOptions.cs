namespace NpcForge.Core.Demo;

/// <summary>Conta de visitante somente leitura (ligada só na versão web).</summary>
public class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }
}
