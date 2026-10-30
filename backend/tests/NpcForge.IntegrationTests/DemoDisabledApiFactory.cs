namespace NpcForge.IntegrationTests;

/// <summary>A API como na versão local: sem conta de visitante.</summary>
public class DemoDisabledApiFactory : ApiFactory
{
    protected override bool DemoEnabled => false;
}

[CollectionDefinition("api-demo-disabled")]
public class DemoDisabledCollection : ICollectionFixture<DemoDisabledApiFactory>;
