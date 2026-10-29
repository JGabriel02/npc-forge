namespace NpcForge.IntegrationTests;

/// <summary>A API como na versão web: IA desligada e nenhuma configuração do Ollama.</summary>
public class AiDisabledApiFactory : ApiFactory
{
    protected override bool AiEnabled => false;
}

[CollectionDefinition("api-ai-disabled")]
public class AiDisabledCollection : ICollectionFixture<AiDisabledApiFactory>;
