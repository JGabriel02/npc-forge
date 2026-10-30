# NpcForge: versão web no ar (plano de implementação)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pôr a versão web do NpcForge (sem IA) no ar, com custo zero: Angular no Azure Static Web Apps, API no Azure App Service F1, banco no Neon. Inclui conta de visitante somente leitura, limite de tentativas e deploy automático pelo GitHub Actions.

**Architecture:**
- **Backend:** ganha uma rota de saúde, *forwarded headers* + *rate limiter* nas rotas de autenticação e uma conta demo. A conta demo é criada sob demanda por um seeder idempotente e protegida por um filtro global que recusa escritas.
- **Frontend:** ganha o fluxo de visitante e um aviso de "servidor acordando".
- **Infraestrutura:** criada uma vez à mão, seguindo `docs/deploy.md`.
- **Pipeline:** um único workflow testa tudo; em push no `main` aplica as migrations no Neon com um *migration bundle*, publica a API (login por OIDC) e o frontend, e faz um smoke test.

**Tech Stack:**
- .NET 10 (ASP.NET Core, EF Core + Npgsql, `Microsoft.AspNetCore.RateLimiting`, `ForwardedHeaders`);
- Angular 22 + Vitest;
- GitHub Actions (`azure/login@v2`, `azure/webapps-deploy@v3`, `Azure/static-web-apps-deploy@v1`);
- Azure App Service F1 Linux, Azure Static Web Apps Free, Neon Free.

**Spec:** `docs/superpowers/specs/2026-10-29-web-deploy-design.md`

## Global Constraints

**Mensagens (exatas):**
- 403 do visitante: `A conta de visitante é só para leitura. Crie uma conta para salvar seus NPCs.`
- 429 do limite: `Muitas tentativas. Aguarde um minuto e tente de novo.`
- 503 da saúde: `O banco de dados não respondeu.`
- Aviso de servidor lento: `Acordando o servidor… a primeira visita do dia pode levar até um minuto.`
- Texto estático em `index.html`: `Carregando o NpcForge… a primeira visita do dia pode levar até um minuto.`
- Botão na tela de login: `Entrar como visitante`.
- Faixa: `Você está como visitante`, com o link `Criar conta`.

**Conta demo:**
- E-mail `visitante@npcforge.demo`, nome de exibição `Visitante`, **sem senha** (só se entra por `POST /api/auth/demo`).
- A claim no JWT é `demo`, com valor `true`.

**Configuração:**
- `Demo:Enabled`, padrão `false`.
- `RateLimit:AuthPerMinute`, padrão `10`; vale para cadastro, login e visitante, por IP, em janela fixa de 1 minuto.

**Tempos no frontend:** o aviso de lentidão aparece aos **4 s**; o tempo-limite de cada requisição à API é de **90 s**.

**Regiões:**
- **Azure:** `East US` (`eastus`).
- **Neon:** `aws-us-east-1`, porque o Neon não aceita mais projetos novos em regiões da Azure; as duas regiões ficam na Virgínia.

**Ferramentas:**
- Node **22** no CI (é a versão local, v22.23.2).
- `dotnet-ef` **10.x** como ferramenta local do repositório (`backend/.config/dotnet-tools.json`).

**Nomes dos recursos:**
- Grupo de recursos `npcforge-rg`.
- App Service `npcforge-api-<sufixo>`; o nome tem de ser único no mundo, e o sufixo é escolhido na Task 7.
- Static Web App `npcforge-web`.

**Segredos:** nenhum no repositório. Só em App Settings da Azure e em secrets do GitHub.

**Commits:** um commit por task, terminando com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Rulings (desvios da spec, decididos ao planejar)

- **R1. Seed da demo sob demanda, não na subida da API.** O seeder roda dentro de `POST /api/auth/demo`: se a conta não existe, cria conta e conteúdo numa transação; senão, só emite o token. Motivo:
  - na subida, o seeder rodaria antes das migrations nos testes (o `ApiFactory` migra depois de o host subir);
  - em produção, as migrations acontecem no pipeline, separadas da subida.

  O comportamento visível é o mesmo: a demo existe quando alguém clica, e reiniciar não duplica.
- **R2. `/api/features` também informa `demo`.** O Angular só mostra o botão de visitante quando a instalação tem a demo ligada; a versão local não mostra.
- **R3. O frontend sabe que é visitante pelo `/api/auth/me`.** `MeResponse` ganha `isDemo`, e o Angular não decodifica o JWT.
- **R4. `index.html` ganha um texto estático de carregamento.** O app inicializador espera `/api/features` antes de desenhar a tela. Com o servidor dormindo, a página ficaria em branco até um minuto, e um snackbar não aparece antes de o Angular subir.

## Review Focus

1. **Visitante tenta alterar por um caminho que não é botão** (URL `/npcs/new` digitada, `PUT /statblock`, importação, campanha): sempre 403 com a mensagem, nunca alteração. Coberto na Task 4 (tabela de rotas) e na Task 5 (guard de rota).
2. **Dois "Entrar como visitante" ao mesmo tempo no primeiro uso**: uma única conta demo, sem conteúdo duplicado, e as duas requisições recebem token. Coberto na Task 3 (teste concorrente).
3. **Cabeçalho `X-Forwarded-For` forjado pelo cliente**: com `ForwardLimit = 1`, vale só o último IP (o que o proxy da Azure acrescenta), então forjar valores à esquerda não escapa do limite. Coberto na Task 2.
4. **Primeira visita com o servidor dormindo**: a página mostra o texto de carregamento, não fica em branco, e a chamada não é abortada antes de 90 s. Coberto na Task 6 e verificado no ar na Task 8.
5. **Recarregar uma rota interna no site publicado** (`/npcs/123`): abre o app, não dá 404. Coberto na Task 8 (`navigationFallback` + verificação no navegador).

---

### Task 1: Rota de saúde e `demo` em `/api/features`

**Files:**
- Create:
  - `backend/src/NpcForge.Api/Controllers/HealthController.cs`;
  - `backend/src/NpcForge.Core/Demo/DemoOptions.cs`.
- Modify:
  - `backend/src/NpcForge.Api/Controllers/FeaturesController.cs`;
  - `backend/src/NpcForge.Infrastructure/DependencyInjection.cs` (registrar `DemoOptions`);
  - `backend/src/NpcForge.Api/appsettings.json` (`"Demo": { "Enabled": false }`);
  - `backend/tests/NpcForge.IntegrationTests/ApiFactory.cs`.
- Test:
  - `backend/tests/NpcForge.IntegrationTests/HealthTests.cs`;
  - `backend/tests/NpcForge.IntegrationTests/Ai/AiFeatureFlagTests.cs`.

**Interfaces:**
- Produces:
  - `public class DemoOptions { public const string Section = "Demo"; public bool Enabled { get; set; } }`, registrada com `AddOptions<DemoOptions>().BindConfiguration(DemoOptions.Section)`;
  - `public record FeaturesDto(bool Ai, bool Demo)`;
  - `GET /api/health`, anônima;
  - `ApiFactory` ganha `protected virtual bool DemoEnabled => true;` e aplica `builder.UseSetting("Demo:Enabled", ...)`. A Task 4 cria `DemoDisabledApiFactory` para o caso desligado.

- [ ] **Step 1: Testes que falham**

```csharp
// HealthTests.cs  [Collection("api")]
[Fact] public async Task Health_WithDatabaseUp_Returns200WithoutLogin()
{
    var response = await factory.CreateClient().GetAsync("/api/health");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
}

// AiFeatureFlagTests.cs, na classe AiEnabledFeatureTests
[Fact] public async Task Features_ReportsDemo_WithoutLogin()
{
    var features = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/features");
    Assert.True(features.GetProperty("demo").GetBoolean());
}
```

- [ ] **Step 2: Rodar.** `dotnet test backend/NpcForge.slnx --filter "FullyQualifiedName~Health|FullyQualifiedName~Features_ReportsDemo"`. Esperado: FALHA (404 em `/api/health` e propriedade `demo` ausente).

- [ ] **Step 3: Implementar.**
  - `HealthController`: `[AllowAnonymous] [Route("api/health")]`; `GET` chama `db.Database.CanConnectAsync(ct)`.
    - Se conectou: `200 { status = "ok" }`.
    - Se não: `Problem(statusCode: 503, title: "O banco de dados não respondeu.")`.
  - `FeaturesController`: injeta também `IOptions<DemoOptions>`.

- [ ] **Step 4: Rodar a suíte inteira.** `dotnet test backend/NpcForge.slnx`. Esperado: tudo passa.

- [ ] **Step 5: Commit** `feat(api): health endpoint and demo flag in features`.

---

### Task 2: Forwarded headers e limite de tentativas

**Files:**
- Create:
  - `backend/src/NpcForge.Api/RateLimiting/AuthRateLimit.cs`;
  - `backend/tests/NpcForge.IntegrationTests/RateLimitedApiFactory.cs`.
- Modify:
  - `backend/src/NpcForge.Api/Program.cs`;
  - `backend/src/NpcForge.Api/Controllers/AuthController.cs` (`[EnableRateLimiting(AuthRateLimit.Policy)]` em register, login e, na Task 4, demo);
  - `backend/src/NpcForge.Api/appsettings.json` (`"RateLimit": { "AuthPerMinute": 10 }`);
  - `backend/tests/NpcForge.IntegrationTests/ApiFactory.cs` (`protected virtual int AuthPerMinute => 1000;`, aplicado como `RateLimit:AuthPerMinute`).
- Test: `backend/tests/NpcForge.IntegrationTests/Auth/RateLimitTests.cs`.

**Interfaces:**
- Produces:
  - `public static class AuthRateLimit { public const string Policy = "auth"; public static IServiceCollection AddAuthRateLimit(this IServiceCollection services, IConfiguration configuration); }`;
  - `RateLimitedApiFactory : ApiFactory`, com `AuthPerMinute => 3`, coleção `"api-rate-limited"`.

**Por que o `ApiFactory` padrão usa 1000:** todos os testes chamam cadastro e login (`CreateAuthenticatedClientAsync`) do mesmo IP. Com o limite real, a suíte inteira tomaria 429.

- [ ] **Step 1: Testes que falham** (`[Collection("api-rate-limited")]`; cada teste usa um IP próprio no `X-Forwarded-For` para não interferir nos outros)

```csharp
[Fact] public async Task Login_OverLimit_Returns429WithMessage()
{
    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");
    for (var i = 0; i < 3; i++) await client.PostAsJsonAsync("/api/auth/login", new { email = "x@test.local", password = "errada123" });

    var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "x@test.local", password = "errada123" });

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.",
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
}

[Fact] public async Task DifferentForwardedIps_HaveSeparateLimits()
    // 3 logins de 203.0.113.20 esgotam o limite; o 1º login de 203.0.113.21 NÃO é 429.

[Fact] public async Task SpoofedLeftmostForwardedIp_DoesNotEscapeLimit()
    // Header "198.51.100.{i}, 203.0.113.30" (o valor à esquerda muda a cada vez, o último é fixo):
    // na 4ª tentativa → 429. Só o último valor (o que o proxy da Azure acrescenta) conta.

[Fact] public async Task Register_IsAlsoLimited()
    // 4 cadastros de 203.0.113.40 → o 4º é 429.
```

- [ ] **Step 2: Rodar.** `--filter "FullyQualifiedName~RateLimitTests"`. Esperado: FALHA (nenhum 429).

- [ ] **Step 3: Implementar.**
  - **`AddAuthRateLimit`:** `AddRateLimiter`, com política `Policy` = `RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido", _ => new FixedWindowRateLimiterOptions { PermitLimit = <RateLimit:AuthPerMinute>, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })`.
    - `RejectionStatusCode = 429`.
    - `OnRejected` escreve um `ProblemDetails` (via `IProblemDetailsService`) com o título da mensagem de 429.
  - **`Program.cs`:**
    - `builder.Services.Configure<ForwardedHeadersOptions>(o => { o.ForwardedHeaders = XForwardedFor | XForwardedProto; o.KnownNetworks.Clear(); o.KnownProxies.Clear(); })`. Mantém `ForwardLimit` no padrão (1). Comentário explicando: o App Service é o proxy, e só o último IP do cabeçalho é confiável.
    - `app.UseForwardedHeaders()` **antes** de `UseExceptionHandler`.
    - `app.UseRateLimiter()` depois de `UseCors()`.

- [ ] **Step 4: Rodar a suíte inteira.** Esperado: tudo passa, inclusive `AuthTests` (o lockout do Identity continua igual).

- [ ] **Step 5: Commit** `feat(api): rate limit auth endpoints per client IP`.

---

### Task 3: Seeder da conta demo e conteúdo original

**Files:**
- Create:
  - `backend/src/NpcForge.Infrastructure/Demo/DemoSeeder.cs`;
  - `backend/src/NpcForge.Infrastructure/Demo/demo-npcs.json` (recurso embutido, `LogicalName="NpcForge.Infrastructure.Demo.demo-npcs.json"`).
- Modify:
  - `backend/src/NpcForge.Infrastructure/NpcForge.Infrastructure.csproj`;
  - `backend/src/NpcForge.Infrastructure/Services/NpcMapping.cs`;
  - `backend/src/NpcForge.Infrastructure/Services/NpcTransferService.cs` (passa a usar `ToNewNpc`);
  - `backend/src/NpcForge.Infrastructure/DependencyInjection.cs` (`AddScoped<DemoSeeder>()`).
- Test:
  - `backend/tests/NpcForge.IntegrationTests/Demo/DemoSeederTests.cs`;
  - `backend/tests/NpcForge.IntegrationTests/DemoSeedApiFactory.cs` (subclasse vazia de `ApiFactory`, coleção `"api-demo-seed"`, banco próprio sem a demo).

**Interfaces:**
- Produces:
  - `public class DemoSeeder(AppDbContext db, UserManager<AppUser> users, TimeProvider time)`, com:
    - `public const string Email = "visitante@npcforge.demo"`;
    - `public Task<AppUser> EnsureSeededAsync(CancellationToken ct)`, que devolve a conta demo e a cria, com o conteúdo, se faltar;
    - `public static string ReadSeedFile()`, que devolve o JSON embutido;
  - `internal static Npc ToNewNpc(this ExportedNpc exported, string userId, Guid? campaignId, DateTime now)` em `NpcMapping`. Atribui `Id` novo e as datas, e liga o `StatBlock` via `ToEntity` (o que normaliza os dados de vida).

**Conteúdo de `demo-npcs.json`:**
- Formato `npcforge-npcs` versão 1, com 8 NPCs **originais**, sem nomes de livros publicados:
  - 4 com ficha (ND de 1/4 a 5) e 4 narrativos;
  - cada NPC tem um campo extra `"campaign"`, que pode ser `"A Estalagem do Corvo Branco"`, `"Contrabandistas de Porto Névoa"` ou `null`;
  - 3 NPCs na primeira campanha, 3 na segunda, 2 sem campanha.
- Textos em português, com notas do mestre preenchidas.
- O arquivo continua importável pela rota normal, que ignora o campo `campaign`.

**Algoritmo de `EnsureSeededAsync`** (é o que os testes não determinam sozinhos):
1. `FindByEmailAsync(Email)`; se existir, retorna.
2. Abre uma transação (`db.Database.BeginTransactionAsync`). Cria o usuário com `users.CreateAsync(user)`, **sem senha**, `DisplayName = "Visitante"`, `EmailConfirmed = true`.
3. Desserializa o arquivo duas vezes, com `JsonSerializerDefaults.Web` mais o mesmo `JsonStringEnumConverter(allowIntegerValues: false)` da API: uma como `NpcExportFile`, outra como `record SeedCampaigns(List<SeedNpc> Npcs)` com `record SeedNpc(string? Campaign)`. Os índices batem.
4. Cria uma `Campaign` por nome distinto não nulo e os NPCs com `ToNewNpc`. Um único `SaveChangesAsync`, depois o commit.
5. Se o `CreateAsync` ou o `SaveChanges` falhar por e-mail duplicado (`DbUpdateException` com `PostgresException` de `UniqueViolation`, ou `IdentityResult` com `DuplicateEmail`/`DuplicateUserName`): desfaz a transação e retorna `FindByEmailAsync(Email)`. Outra requisição criou a demo ao mesmo tempo.

- [ ] **Step 1: Testes que falham** (`[Collection("api")]`; cada teste pega um `DemoSeeder` num scope novo de `factory.Services`)

```csharp
[Fact] public async Task EnsureSeeded_TwiceInARow_CreatesOneAccountAndContentOnce()
{
    var first = await Seed();  var second = await Seed();
    Assert.Equal(first.Id, second.Id);
    Assert.Equal(8, await CountNpcs(first.Id));
    Assert.Equal(2, await CountCampaigns(first.Id));
}

[Fact] public async Task EnsureSeeded_Concurrently_CreatesOneAccount()
{
    var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Seed()));
    Assert.Single(results.Select(u => u.Id).Distinct());
    Assert.Equal(8, await CountNpcs(results[0].Id));
}

[Fact] public async Task SeedContent_LinksCampaignsAsInFile()
    // 3 NPCs em "A Estalagem do Corvo Branco", 3 em "Contrabandistas de Porto Névoa", 2 sem campanha; 4 com ficha.

[Fact] public async Task SeedFile_IsAValidExportFile()
{
    // Valida pela rota real: um usuário comum importa o arquivo da demo.
    var client = await factory.CreateAuthenticatedClientAsync();
    var response = await client.PostAsync("/api/npcs/import",
        new StringContent(DemoSeeder.ReadSeedFile(), Encoding.UTF8, "application/json"));
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}

[Fact] public async Task DemoAccount_CannotLogInWithAPassword()
    // POST /api/auth/login { email = DemoSeeder.Email, password = "senha1234" } → 401.
```

Os testes compartilham o banco da coleção `api`, então a conta demo pode já existir quando cada um roda. Por isso nenhum teste depende de ela não existir antes. O teste concorrente só é forte se rodar com o banco sem a demo: ele fica numa classe com `[Collection("api-demo-seed")]` e uma `DemoSeedApiFactory : ApiFactory` própria (banco limpo).

- [ ] **Step 2: Rodar.** `--filter "FullyQualifiedName~DemoSeederTests"`. Esperado: FALHA (`DemoSeeder` não existe).

- [ ] **Step 3: Implementar** o seeder, o `ToNewNpc` (e trocar a criação de NPC no `NpcTransferService.ImportAsync` para usá-lo) e o arquivo de conteúdo.

- [ ] **Step 4: Rodar a suíte inteira.** Esperado: tudo passa, inclusive os testes de importação existentes (a refatoração não muda nada).

- [ ] **Step 5: Commit** `feat(api): demo account seeder with original sample NPCs`.

---

### Task 4: Entrar como visitante e conta somente leitura

**Files:**
- Create:
  - `backend/src/NpcForge.Api/Auth/DemoReadOnlyFilter.cs`;
  - `backend/tests/NpcForge.IntegrationTests/DemoDisabledApiFactory.cs` (`DemoEnabled => false`, coleção `"api-demo-disabled"`).
- Modify:
  - `backend/src/NpcForge.Api/Controllers/AuthController.cs`;
  - `backend/src/NpcForge.Api/Auth/JwtTokenService.cs`;
  - `backend/src/NpcForge.Api/Auth/AuthDtos.cs`;
  - `backend/src/NpcForge.Api/Program.cs` (`AddControllers(o => o.Filters.Add<DemoReadOnlyFilter>())`).
- Test: `backend/tests/NpcForge.IntegrationTests/Demo/DemoAccessTests.cs`.

**Interfaces:**
- Consumes: `DemoSeeder.EnsureSeededAsync` (Task 3); `DemoOptions` (Task 1); `AuthRateLimit.Policy` (Task 2).
- Produces:
  - `POST /api/auth/demo`: anônima, `[EnableRateLimiting(AuthRateLimit.Policy)]`. Responde `LoginResponse`, ou 404 com `Demo:Enabled=false`;
  - `JwtTokenService.CreateToken(AppUser user, bool demo = false)`: com `demo`, inclui `new Claim("demo", "true")`;
  - `public record MeResponse(string Id, string Email, string DisplayName, bool IsDemo)`. `IsDemo` vem da claim do token atual (`User.HasClaim("demo", "true")`);
  - `public static class DemoClaims { public const string Type = "demo"; public static bool IsDemo(this ClaimsPrincipal user); }`, em `DemoReadOnlyFilter.cs`;
  - helper de teste `public static Task<HttpClient> CreateDemoClientAsync(this ApiFactory factory)` em `TestAuthExtensions.cs`.

**Regra do filtro** (`IAsyncResourceFilter`, para rodar antes do model binding):
- Se o método é `GET`, `HEAD` ou `OPTIONS`, **ou** o endpoint tem `IAllowAnonymous`, **ou** `!user.IsDemo()`: segue.
- Senão: `403` com `ProblemDetails` cujo título é a mensagem de visitante.

- [ ] **Step 1: Testes que falham**

```csharp
// DemoAccessTests.cs  [Collection("api")]
[Fact] public async Task DemoLogin_ReturnsTokenForTheDemoAccount()
{
    var client = await factory.CreateDemoClientAsync();
    var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
    Assert.Equal("visitante@npcforge.demo", me.GetProperty("email").GetString());
    Assert.True(me.GetProperty("isDemo").GetBoolean());
}

[Fact] public async Task RegularUser_IsNotDemo()  // me.isDemo == false

[Theory]
[InlineData("POST", "/api/npcs")]
[InlineData("PUT", "/api/npcs/{npc}")]
[InlineData("DELETE", "/api/npcs/{npc}")]
[InlineData("PUT", "/api/npcs/{npc}/statblock")]
[InlineData("DELETE", "/api/npcs/{npc}/statblock")]
[InlineData("POST", "/api/npcs/import")]
[InlineData("POST", "/api/campaigns")]
[InlineData("PUT", "/api/campaigns/{campaign}")]
[InlineData("DELETE", "/api/campaigns/{campaign}")]
public async Task Demo_CannotWrite(string method, string route)
    // {npc}/{campaign} = ids reais da conta demo (pegos via GET). Corpo: "{}" com application/json.
    // Esperado: 403 e title == "A conta de visitante é só para leitura. Crie uma conta para salvar seus NPCs."
    // Depois: GET /api/npcs da demo continua com 8 NPCs.

[Theory]
[InlineData("/api/npcs")]
[InlineData("/api/campaigns")]
[InlineData("/api/npcs/export")]
public async Task Demo_CanRead(string route)  // 200

[Fact] public async Task Demo_CanExportOneNpc()  // GET /api/npcs/{id}/export → 200

[Fact] public async Task RegularUser_CanStillWrite()  // POST /api/npcs com usuário comum → 201

// [Collection("api-demo-disabled")]
[Fact] public async Task DemoLogin_WhenDisabled_Returns404()
```

- [ ] **Step 2: Rodar.** `--filter "FullyQualifiedName~DemoAccessTests"`. Esperado: FALHA (404 na rota demo e `isDemo` ausente).

- [ ] **Step 3: Implementar.**
  - `AuthController.Demo`:
    - se `Demo:Enabled` estiver desligado, `NotFound()`;
    - senão, `EnsureSeededAsync` e depois `CreateToken(user, demo: true)`.
  - `Me` preenche `IsDemo`.
  - Registrar o filtro.

- [ ] **Step 4: Rodar a suíte inteira.** Esperado: tudo passa.

- [ ] **Step 5: Commit** `feat(api): read-only visitor login`.

---

### Task 5: Visitante no Angular

**Files:**
- Modify:
  - `frontend/src/app/core/api/api.models.ts` (`MeResponse.isDemo: boolean`);
  - `frontend/src/app/core/features/features.service.ts` (signal `demo`);
  - `frontend/src/app/core/auth/auth.service.ts`;
  - `frontend/src/app/core/auth/auth.guards.ts`;
  - `frontend/src/app/core/auth/jwt.interceptor.ts` (`/auth/demo` entra em `AUTH_ENDPOINTS`);
  - `frontend/src/app/app.routes.ts`;
  - `frontend/src/app/features/auth/login.page.ts` e `.html`;
  - `frontend/src/app/core/layout/shell.component.html`, `.ts` e `.scss`;
  - as telas com botões de escrita:
    - `npc-list.page.html`: Importar, Novo NPC e o Novo NPC do estado vazio;
    - `npc-view.page.html`: Editar e Excluir;
    - `campaign-list.page.html`: os dois "Nova campanha";
    - `campaign-detail.page.html`: Editar, Excluir e Novo NPC.
- Test:
  - `auth.service.spec.ts`;
  - `auth.guards.spec.ts`;
  - `login.page.spec.ts`;
  - `shell.component.spec.ts`;
  - `npc-list.page.spec.ts` (criar ou estender os que existem).

**Interfaces:**
- Produces:
  - `AuthService.loginAsDemo(): Observable<void>` (`POST /auth/demo`, salva a sessão, carrega o usuário);
  - `AuthService.isDemo = computed(() => this.user()?.isDemo === true)`;
  - `AuthService.leaveDemoToRegister(): Promise<void>` (encerra a sessão e navega para `/register`);
  - `FeaturesService.demo: Signal<boolean>`;
  - `writerGuard: CanActivateFn`: o visitante vai para `/npcs`. Aplicado em `npcs/new` e `npcs/:id/edit`.

- [ ] **Step 1: Specs que falham**
  - **Login:**
    - com `features.demo()` verdadeiro, mostra o botão `Entrar como visitante`; com falso, não mostra;
    - o clique chama `loginAsDemo` e navega para `/npcs`;
    - um erro 429 mostra `Muitas tentativas. Aguarde um minuto e tente de novo.`; o mesmo vale na tela de cadastro (`register.page.spec.ts`).
  - **Shell:**
    - para o visitante, mostra `Você está como visitante` e o link `Criar conta`, que chama `leaveDemoToRegister`;
    - para um usuário comum, não mostra a faixa.
  - **`writerGuard`:** o visitante recebe `UrlTree('/npcs')`; um usuário comum recebe `true`.
  - **Lista de NPCs:** para o visitante, não há botões `Importar` nem `Novo NPC`, mas `Exportar` existe.
  - **`AuthService.loginAsDemo`:** faz `POST .../auth/demo` e depois `GET .../auth/me`; `isDemo()` passa a ser verdadeiro.

- [ ] **Step 2: Rodar.** `cd frontend && npx ng test --watch=false`. Esperado: as specs novas falham.

- [ ] **Step 3: Implementar.** Envolver cada botão de escrita em `@if (!auth.isDemo())`, injetando `AuthService` como `protected readonly auth` nas páginas.

- [ ] **Step 4: Rodar os testes e o build.** `npx ng test --watch=false` e `npx ng build`. Esperado: tudo passa. O bundle inicial fica perto dos 326 KB atuais (o login é lazy).

- [ ] **Step 5: Ver no navegador** (API local com `Demo__Enabled=true` no `.env`/compose). Conferir:
  - entrar como visitante;
  - a lista com as 2 campanhas e os 8 NPCs;
  - nenhum botão de escrita;
  - `/npcs/new` digitado volta para `/npcs`;
  - Exportar funciona;
  - Criar conta leva ao cadastro.

- [ ] **Step 6: Commit** `feat(web): visitor login and read-only UI`.

---

### Task 6: Aviso de servidor acordando

**Files:**
- Create: `frontend/src/app/core/api/slow-server.interceptor.ts`.
- Modify:
  - `frontend/src/app/app.config.ts` (`withInterceptors([jwtInterceptor, slowServerInterceptor])`);
  - `frontend/src/index.html` (texto estático dentro de `<app-root>`, com estilo inline mínimo e centralizado).
- Test: `frontend/src/app/core/api/slow-server.interceptor.spec.ts`.

**Interfaces:**
- Produces: `slowServerInterceptor: HttpInterceptorFn`. Só age em URLs que começam com `environment.apiBaseUrl`.
  - Aplica `timeout(90_000)`.
  - Se a requisição ainda estiver pendente aos 4 000 ms, abre um `MatSnackBar` com o aviso (sem botão, sem duração).
  - Fecha o aviso quando **todas** as requisições lentas terminam. Um contador no módulo evita um snackbar por requisição.

- [ ] **Step 1: Specs que falham** (`vi.useFakeTimers()` + `HttpTestingController` + `MatSnackBar` espionado)
  - requisição que responde em 3 s: o snackbar não abre;
  - requisição pendente aos 4 s: abre com `Acordando o servidor… a primeira visita do dia pode levar até um minuto.`;
  - quando ela responde: o snackbar é fechado;
  - duas requisições lentas: abre uma vez e fecha só quando a segunda termina;
  - requisição pendente aos 90 s: o observable erra com `TimeoutError`;
  - URL fora da API: nada acontece.

- [ ] **Step 2: Rodar.** Esperado: falham (o interceptor não existe).

- [ ] **Step 3: Implementar** o interceptor e o texto estático do `index.html` (o Angular substitui o conteúdo de `<app-root>` ao desenhar).

- [ ] **Step 4: Rodar testes e build.** Esperado: tudo passa.

- [ ] **Step 5: Commit** `feat(web): wake-up notice for a sleeping server`.

---

### Task 7: Recursos na Azure e no Neon, guiados por `docs/deploy.md`

Esta task é feita **com o usuário**: criar recursos nas contas dele é ação dele. Claude escreve o guia, prepara comandos e confere cada resultado. O Azure CLI não está instalado na máquina; o guia usa o portal, com os comandos `az` como alternativa opcional.

**Files:**
- Create:
  - `docs/deploy.md`;
  - `backend/.config/dotnet-tools.json` (`dotnet new tool-manifest` + `dotnet tool install dotnet-ef --version 10.*`).

**Interfaces:**
- Produces (anotados no fim de `docs/deploy.md`, sem segredos):
  - URL da API `https://npcforge-api-<sufixo>.azurewebsites.net`;
  - URL do site `https://<gerado>.azurestaticapps.net`;
  - nome do App Service;
  - secrets do GitHub cadastrados.

- [ ] **Step 1: Conferir o .NET 10 no F1 Linux.** No portal, ao criar o App Service, a pilha "`.NET 10`" aparece para Linux? (Ou `az webapp list-runtimes --os linux`, que deve mostrar `DOTNETCORE:10.0`.) Se **não** aparecer: parar e apresentar ao usuário a alternativa da spec (App Service F1 rodando a imagem do Dockerfile via GitHub Container Registry). Isso muda as Tasks 7 e 8 e exige um ruling antes de seguir.

- [ ] **Step 2: Migration bundle local.** Rodar com o Postgres do Docker:
  - `cd backend && dotnet tool restore`;
  - `dotnet ef migrations bundle --project src/NpcForge.Infrastructure --startup-project src/NpcForge.Api -o ../efbundle.exe --force`;
  - `../efbundle.exe --connection "Host=localhost;Database=npcforge;..."`.

  Esperado: `No migrations were applied. The database is already up to date.` (prova que o bundle funciona e é idempotente). Adicionar `efbundle*` ao `.gitignore`.

- [ ] **Step 3: Escrever `docs/deploy.md`**, em português, com passos numerados e o que conferir em cada um:
  1. **Alerta de orçamento** na assinatura (R$ 1).
  2. **Neon:** projeto `npcforge`, região `aws-us-east-1`. Converter a URL `postgresql://user:senha@host/db?sslmode=require` para `Host=host;Database=db;Username=user;Password=senha;SSL Mode=Require`.
  3. **Grupo de recursos** `npcforge-rg` em `East US`.
  4. **App Service:** plano Linux **F1** e app `npcforge-api-<sufixo>` com pilha .NET 10.
     - App Settings: `ConnectionStrings__Default`, `Jwt__Key` (gerar com `openssl rand -base64 48`), `Cors__AllowedOrigins__0` (preenchido no passo 5), `Ai__Enabled=false`, `Demo__Enabled=true`.
     - Em Configuração Geral: **HTTPS Only** ligado.
  5. **Static Web App** `npcforge-web`, plano Free, origem "Outro" (o deploy vem do nosso workflow). Copiar a URL gerada para `Cors__AllowedOrigins__0` e o *deployment token* para o GitHub.
  6. **OIDC:**
     - criar um registro de aplicativo `npcforge-github` no Entra ID;
     - criar uma credencial federada (repositório `JGabriel02/npc-forge`, entidade *Branch* `main`);
     - dar o papel **Contributor** ao app no escopo do grupo `npcforge-rg`.
  7. **Secrets do GitHub:** `NEON_CONNECTION_STRING`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_STATIC_WEB_APPS_API_TOKEN`.
  8. **Recriar a conta demo:** apagar o usuário `visitante@npcforge.demo` no banco (SQL no editor do Neon: `DELETE FROM "AspNetUsers" WHERE "Email" = 'visitante@npcforge.demo';`. Campanhas, NPCs e fichas saem junto: as chaves estrangeiras para o usuário já são `ON DELETE CASCADE`). O próximo "Entrar como visitante" recria tudo.
  9. **O que esperar do plano gratuito:**
     - F1: cerca de 60 min de CPU/dia, sem Always On, dorme quando parado;
     - Neon: suspende após 5 min parado, 1 GB por projeto;
     - Static Web Apps Free: 100 GB/mês de tráfego.

- [ ] **Step 4: Usuário executa os passos 1–7**, com Claude conferindo cada resultado (por exemplo, `curl https://npcforge-api-<sufixo>.azurewebsites.net/` responde, mesmo que 404, e o projeto Neon aceita conexão pelo editor SQL).

- [ ] **Step 5: Commit** `docs: deployment guide for Azure and Neon` (guia, manifest de ferramentas e `.gitignore`).

---

### Task 8: Configuração de produção, workflow e primeiro deploy

**Files:**
- Create:
  - `.github/workflows/deploy.yml`;
  - `frontend/public/staticwebapp.config.json`.
- Modify:
  - `frontend/src/environments/environment.ts` (`apiBaseUrl: 'https://npcforge-api-<sufixo>.azurewebsites.net/api'`);
  - `README.md`.
- Test: o próprio workflow (verde no GitHub) e a verificação no ar.

**Interfaces:**
- Consumes: as URLs e os secrets da Task 7; `GET /api/health` (Task 1).

**`staticwebapp.config.json`:**
```json
{ "navigationFallback": { "rewrite": "/index.html", "exclude": ["/*.{js,css,ico,png,svg,woff2,json}"] } }
```

**`deploy.yml`:**
- **Gatilhos:** `on: push` (todas as branches) e `pull_request`.
- **Variáveis do workflow:** `API_APP_NAME`, `API_URL`.
- **Jobs:**
  - `backend-tests` (`ubuntu-latest`): `actions/setup-dotnet@v4` com `10.0.x`, depois `dotnet test backend/NpcForge.slnx`.
  - `frontend-tests`: `actions/setup-node@v4` com Node 22 e cache npm, depois `npm ci`, `npx ng test --watch=false` e `npx ng build` (em `frontend/`). O build sobe como artefato (`frontend/dist/npc-forge-web/browser`).
  - `deploy`:
    - `needs: [backend-tests, frontend-tests]`;
    - `if: github.event_name == 'push' && github.ref == 'refs/heads/main'`;
    - `permissions: id-token: write, contents: read`;
    - `concurrency: deploy-production`, para dois deploys não se cruzarem.
- **Passos do `deploy`, nesta ordem:**
  1. `dotnet tool restore`, `dotnet publish src/NpcForge.Api -c Release -o publish` e o migration bundle (`-r linux-x64 --self-contained`).
  2. `./efbundle --connection "${{ secrets.NEON_CONNECTION_STRING }}"`. Se falhar, o job para aqui, e nada foi publicado.
  3. `azure/login@v2` com os três IDs.
  4. `azure/webapps-deploy@v3` com `app-name: ${{ env.API_APP_NAME }}` e `package: backend/publish`.
  5. Baixar o artefato do frontend e rodar `Azure/static-web-apps-deploy@v1` com `action: upload`, `app_location: <pasta do artefato>`, `skip_app_build: true` e `azure_static_web_apps_api_token`.
  6. **Smoke test:** até 10 tentativas, 15 s entre elas, de `curl -fsS $API_URL/api/health` e `curl -fsS $API_URL/api/features`. No fim, falha se alguma não passar.

- [ ] **Step 1: Escrever a configuração e o workflow.** Rodar `npx ng build` e conferir que `staticwebapp.config.json` aparece em `dist/npc-forge-web/browser/`.

- [ ] **Step 2: Push da branch `feat/web-deploy`** (pedir ao usuário antes de qualquer push). Esperado no GitHub: `backend-tests` e `frontend-tests` verdes, `deploy` pulado (não é `main`).

- [ ] **Step 3: Merge no `main` e push**, só quando o usuário disser "Merge e push" (depois da revisão final, ver abaixo). Esperado: `deploy` verde, com o smoke test passando.

- [ ] **Step 4: Verificação no ar** (navegador, site publicado):
  - cadastro de uma conta de teste;
  - login;
  - criar um NPC;
  - importar um arquivo exportado da versão local;
  - recarregar `/npcs/<id>`;
  - entrar como visitante (8 NPCs, nenhum botão de escrita, Exportar funciona);
  - 11 logins errados seguidos dão a mensagem de 429.
  - Depois de 20 min sem uso, abrir o site: aparece o texto de carregamento ou o aviso de "acordando", e a página carrega.

- [ ] **Step 5: README:**
  - link do site no topo;
  - 3 prints (lista de NPCs, ficha de um NPC, tela de login com o botão de visitante), em `docs/images/`;
  - seção "Versão web" com o aviso do sono do plano gratuito;
  - link para `docs/deploy.md`;
  - Roadmap: marcar a versão web.

- [ ] **Step 6: Commit** `ci: test and deploy to Azure on push to main` (workflow + configuração), e `docs: live site link and screenshots` (README e prints).

---

**Revisão final:**
- Depois da Task 6 e antes do primeiro merge (Task 8, Step 3), um revisor independente (opus) revisa a branch inteira contra esta spec e este plano.
- Achados Críticos e Importantes são corrigidos com TDD; os menores ficam registrados.
- Só então o usuário decide "Merge e push".
