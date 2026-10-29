namespace NpcForge.IntegrationTests;

/// <summary>
/// A API num Kestrel de verdade (porta livre), em vez do servidor em memória. Só o Kestrel aplica
/// limites como o [RequestSizeLimit]; o TestServer ignora.
/// </summary>
public class KestrelApiFactory : ApiFactory
{
    public KestrelApiFactory() => UseKestrel(0);
}

[CollectionDefinition("api-kestrel")]
public class KestrelCollection : ICollectionFixture<KestrelApiFactory>;
