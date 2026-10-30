namespace NpcForge.IntegrationTests;

/// <summary>Banco próprio, sem a conta demo: o teste de concorrência precisa do "primeiro uso" de verdade.</summary>
public class DemoSeedApiFactory : ApiFactory;

[CollectionDefinition("api-demo-seed")]
public class DemoSeedCollection : ICollectionFixture<DemoSeedApiFactory>;
