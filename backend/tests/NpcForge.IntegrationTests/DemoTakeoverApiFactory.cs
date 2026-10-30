namespace NpcForge.IntegrationTests;

/// <summary>Banco próprio: o teste cria um usuário com senha no e-mail da demo, o que estragaria os outros testes.</summary>
public class DemoTakeoverApiFactory : ApiFactory;

[CollectionDefinition("api-demo-takeover")]
public class DemoTakeoverCollection : ICollectionFixture<DemoTakeoverApiFactory>;
