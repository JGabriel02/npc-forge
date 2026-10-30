namespace NpcForge.IntegrationTests;

/// <summary>A API com o limite de tentativas baixo (3/min), para testar o 429 sem centenas de requisições.</summary>
public class RateLimitedApiFactory : ApiFactory
{
    protected override int AuthPerMinute => 3;
}

[CollectionDefinition("api-rate-limited")]
public class RateLimitedCollection : ICollectionFixture<RateLimitedApiFactory>;
