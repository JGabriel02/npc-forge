namespace NpcForge.Core;

/// <summary>Tamanhos máximos dos campos de texto, compartilhados por entidades, validação e banco.</summary>
public static class NpcLimits
{
    public const int ShortText = 100;
    public const int MediumText = 500;
    public const int LongText = 2000;
    public const int VeryLongText = 4000;

    /// <summary>Máximo de itens em cada lista da ficha (traits, ações, reações).</summary>
    public const int StatBlockListItems = 30;
}
