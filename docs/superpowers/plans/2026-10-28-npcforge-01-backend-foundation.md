# NpcForge — Plano 1: Fundação do backend

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** API .NET funcionando com autenticação, CRUD de campanhas e NPCs (com e sem ficha D&D 5e), testada e rodando via Docker, ainda sem IA.

**Architecture:** Três projetos (`Api → Core ← Infrastructure`). O Core contém entidades, regras de D&D, DTOs e interfaces de serviço; a Infrastructure implementa os serviços usando o `AppDbContext` do EF Core diretamente (o DbContext é o repositório, sem camada extra); a Api contém controllers finos, autenticação e configuração.

**Tech Stack:** .NET 10, ASP.NET Core (controllers), EF Core + Npgsql, ASP.NET Core Identity + JWT Bearer, xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.PostgreSql, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-28-npcforge-design.md` (seções 3, 4, 5, 8 e 9 nas partes de auth, campanhas e NPCs).

**Planos seguintes (fora deste):** 2 Angular sem IA · 3 IA + limite diário · 4 botões ✨ + warnings · 5 NPCs públicos · 6 provedor em nuvem + polimento.

## Global Constraints

- Target `net10.0` (SDK 10.0.x instalado). Arquivo de solução: `backend/NpcForge.slnx`.
- PostgreSQL 17 (`postgres:17-alpine`) no Compose e nos Testcontainers.
- Asserts com xUnit puro (`Assert.*`); não usar FluentAssertions (licença comercial na v8).
- Todo erro HTTP no formato `ProblemDetails` (`AddProblemDetails()`; validação via `ValidationProblem`).
- Recurso de outro usuário → **404**, nunca 403.
- Datas sempre em UTC, obtidas de `TimeProvider` injetado (`TimeProvider.System` em produção).
- Identificadores em inglês; mensagens para o usuário em português.
- Nenhum segredo commitado; configuração via variáveis de ambiente (`Jwt__Key`, `ConnectionStrings__Default` etc.). `.env` no `.gitignore`, `.env.example` versionado.
- Limites de texto (classe `NpcLimits`, decisão deste plano já que a spec só fixa `Name`):
  - `Name`, `Race`, `Occupation`, `CreatureType`, `Alignment`, `Speed`, `HitDice`, nome de trait/action: **100**
  - `SavingThrows`, `Skills`, `Senses`, `Languages`, `Damage`: **500**
  - `Appearance`, `Personality`, campanha `Description` e `Setting`, descrição de trait/action: **2000**
  - `Background`, `Notes`: **4000**
- Enums serializados como string (`JsonStringEnumConverter`).
- Commits pequenos, um por task, mensagens no padrão Conventional Commits.

## Review Focus

1. **Atributo baixo:** `AbilityModifier(1)` deve ser **-5** (arredondar para baixo, não truncar para zero como a divisão inteira do C#). Teste na Task 1.
2. **Busca com curinga:** `search=%` ou `search=_` deve buscar o caractere literal, não listar todos os NPCs. Teste na Task 5.
3. **Paginação absurda:** `pageSize=0`, `pageSize=1000` ou `page=-1` não podem dar erro 500 nem devolver milhares de itens; os valores são limitados (`page ≥ 1`, `pageSize` entre 1 e 100, padrão 20). Teste na Task 5.
4. **Acento e maiúsculas na busca:** buscar `ANÃO` encontra `anão`. Teste na Task 5.
5. **E-mail duplicado com outra caixa:** registrar `Joao@x.com` depois de `joao@x.com` deve retornar 400. Teste na Task 3.

---

### Task 1: Repositório, solução e regras de D&D

**Files:**
- Create: `npc-forge/.gitignore` (`dotnet new gitignore` + linhas `.env`, `node_modules/`)
- Create: `backend/NpcForge.slnx`
- Create: `backend/src/NpcForge.Core/NpcForge.Core.csproj` (`classlib`)
- Create: `backend/src/NpcForge.Infrastructure/NpcForge.Infrastructure.csproj` (`classlib`, referencia Core)
- Create: `backend/src/NpcForge.Api/NpcForge.Api.csproj` (`webapi --use-controllers`, referencia Core e Infrastructure)
- Create: `backend/tests/NpcForge.UnitTests/` (`xunit`, referencia Core)
- Create: `backend/tests/NpcForge.IntegrationTests/` (`xunit`, referencia Api)
- Create: `backend/src/NpcForge.Core/Rules/DndRules.cs`
- Test: `backend/tests/NpcForge.UnitTests/Rules/DndRulesTests.cs`

**Interfaces:**
- Produces: `static class DndRules` com
  - `int AbilityModifier(int score)`
  - `IReadOnlyList<string> ChallengeRatings` → `"0","1/8","1/4","1/2","1"…"30"` nessa ordem
  - `bool IsValidChallengeRating(string? cr)`
  - `int ProficiencyBonus(string cr)` (lança `ArgumentException` se o ND for inválido)
  - `bool IsValidHitDice(string? hitDice)` — regex `^\d+d\d+([+-]\d+)?$`

- [ ] **Step 1: Inicializar git e a estrutura**

Na pasta `npc-forge/`: `git init`, criar `.gitignore`, a solução e os 5 projetos com as referências acima. Remover os arquivos de exemplo dos templates (`Class1.cs`, `UnitTest1.cs`, `WeatherForecast*`).

- [ ] **Step 2: Verificar que a solução compila**

Run: `dotnet build backend/NpcForge.slnx`
Expected: `Build succeeded` com 0 erros.

- [ ] **Step 3: Escrever os testes que falham**

```csharp
[Theory]
[InlineData(1, -5)] [InlineData(8, -1)] [InlineData(9, -1)] [InlineData(10, 0)]
[InlineData(11, 0)] [InlineData(12, 1)] [InlineData(20, 5)] [InlineData(30, 10)]
public void AbilityModifier_FollowsFloorRule(int score, int expected)
    => Assert.Equal(expected, DndRules.AbilityModifier(score));

[Theory]
[InlineData("0", 2)] [InlineData("1/8", 2)] [InlineData("4", 2)] [InlineData("5", 3)]
[InlineData("8", 3)] [InlineData("9", 4)] [InlineData("13", 5)] [InlineData("17", 6)]
[InlineData("21", 7)] [InlineData("25", 8)] [InlineData("29", 9)] [InlineData("30", 9)]
public void ProficiencyBonus_ByChallengeRating(string cr, int expected) => ...

// IsValidChallengeRating: true para "0","1/8","1/4","1/2","1","30";
//                         false para "31","1/3","-1","","abc","0.5", null
// IsValidHitDice: true para "4d8+4","1d6","10d10-5";
//                 false para "d8","4d","4d8+","abc","", null
// ProficiencyBonus("1/3") lança ArgumentException
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.UnitTests`
Expected: erro de compilação, `DndRules` não existe.

- [ ] **Step 5: Implementar `DndRules`**

`AbilityModifier` com `(int)Math.Floor((score - 10) / 2.0)`. `ProficiencyBonus`: ND < 5 → 2; senão `2 + (nd - 1) / 4` (divisão inteira), o que dá 5–8 → 3 … 29–30 → 9.

- [ ] **Step 6: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.UnitTests`
Expected: todos PASS.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: scaffold solution and add D&D 5e rules"
```

---

### Task 2: Entidades, DbContext e migration inicial

**Files:**
- Create: `backend/src/NpcForge.Core/Entities/Campaign.cs`, `Npc.cs`, `StatBlock.cs`, `StatBlockFeature.cs`, `StatBlockAction.cs`, `CreatureSize.cs`
- Create: `backend/src/NpcForge.Core/NpcLimits.cs`
- Create: `backend/src/NpcForge.Infrastructure/Identity/AppUser.cs`
- Create: `backend/src/NpcForge.Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/src/NpcForge.Infrastructure/Persistence/Configurations/CampaignConfiguration.cs`, `NpcConfiguration.cs`, `StatBlockConfiguration.cs`
- Create: `backend/src/NpcForge.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/NpcForge.Infrastructure/Migrations/*` (gerado)
- Create: `backend/.config/dotnet-tools.json` (tool local `dotnet-ef`)
- Modify: `backend/src/NpcForge.Api/Program.cs` (chamar `AddInfrastructure`; adicionar `public partial class Program;` no fim)
- Test: `backend/tests/NpcForge.IntegrationTests/ApiFactory.cs`, `Persistence/AppDbContextTests.cs`

**Interfaces:**
- Consumes: `NpcLimits` é usado por todas as tasks seguintes.
- Produces:
  - `Campaign { Guid Id; string UserId; string Name; string? Description; string? Setting; DateTime CreatedAt; DateTime UpdatedAt; List<Npc> Npcs }`
  - `Npc { Guid Id; string UserId; Guid? CampaignId; Campaign? Campaign; string Name; string? Race, Occupation, Appearance, Personality, Background, Notes; StatBlock? StatBlock; DateTime CreatedAt; DateTime UpdatedAt }`
  - `StatBlock { Guid NpcId; CreatureSize Size; string CreatureType; string Alignment; int ArmorClass; int HitPoints; string HitDice; string Speed; int Str, Dex, Con, Int, Wis, Cha; string ChallengeRating; string? SavingThrows, Skills, Senses, Languages; List<StatBlockFeature> Traits; List<StatBlockAction> Actions; List<StatBlockFeature> Reactions }`
  - `StatBlockFeature { string Name; string Description }`, `StatBlockAction : StatBlockFeature { int? AttackBonus; string? Damage }`
  - `enum CreatureSize { Tiny, Small, Medium, Large, Huge, Gargantuan }`
  - `AppUser : IdentityUser { string DisplayName }`
  - `AppDbContext : IdentityDbContext<AppUser>` com `DbSet<Campaign> Campaigns`, `DbSet<Npc> Npcs`, `DbSet<StatBlock> StatBlocks`
  - `static IServiceCollection AddInfrastructure(this IServiceCollection, IConfiguration)` — registra o `AppDbContext` com Npgsql usando `ConnectionStrings:Default` e `TimeProvider.System`
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` (fixture de coleção xUnit `"api"`) — sobe `PostgreSqlContainer`, injeta `ConnectionStrings:Default` e um `Jwt:Key` de teste via `UseSetting`, ambiente `Testing`, aplica as migrations no `InitializeAsync`

Mapeamento:
- `Campaign.UserId` e `Npc.UserId` → FK para `AspNetUsers`, `OnDelete(Cascade)`.
- `Npc.CampaignId` → FK, **`OnDelete(SetNull)`**.
- `StatBlock` em tabela própria, chave = `NpcId`, 1:0..1 com `Npc`, `OnDelete(Cascade)`.
- `Traits`, `Actions`, `Reactions` → `OwnsMany(...).ToJson()` (colunas `jsonb`).
- Tamanhos máximos das colunas = `NpcLimits`. `Size` salvo como string.
- Índices: `Npcs(UserId, UpdatedAt)`, `Campaigns(UserId)`.

- [ ] **Step 1: Escrever os testes que falham**

Em `AppDbContextTests` (usando um usuário criado direto no contexto):
- `SavesAndLoadsNpcWithStatBlockAndJsonLists`: salva um NPC com ficha contendo 2 `Actions` (uma com `AttackBonus = 5`, `Damage = "1d8+3"`) e 1 `Trait`; num novo escopo, carrega com `Include(n => n.StatBlock)` e verifica que `Actions.Count == 2`, `Actions[0].AttackBonus == 5`, `Size == CreatureSize.Medium`.
- `DeletingNpcDeletesStatBlock`: depois de excluir o NPC, `StatBlocks.CountAsync(s => s.NpcId == id) == 0`.
- `DeletingCampaignKeepsNpcsWithoutCampaign`: depois de excluir a campanha, o NPC continua existindo e `CampaignId == null`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests` (Docker precisa estar rodando)
Expected: erro de compilação (entidades inexistentes).

- [ ] **Step 3: Implementar entidades, configurações, `AppDbContext`, `AddInfrastructure` e `ApiFactory`**

- [ ] **Step 4: Gerar a migration**

Run: `dotnet tool restore` e depois `dotnet ef migrations add InitialCreate -p backend/src/NpcForge.Infrastructure -s backend/src/NpcForge.Api`
Expected: pasta `Migrations/` criada; o arquivo gerado contém `jsonb` para `Traits`, `Actions` e `Reactions` e `onDelete: ReferentialAction.SetNull` para `CampaignId`.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests`
Expected: 3 PASS.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: add domain entities, DbContext and initial migration"
```

---

### Task 3: Autenticação (Identity + JWT)

**Files:**
- Create: `backend/src/NpcForge.Api/Auth/JwtOptions.cs`, `JwtTokenService.cs`, `AuthDtos.cs`, `ClaimsPrincipalExtensions.cs`
- Create: `backend/src/NpcForge.Api/Controllers/AuthController.cs`
- Modify: `backend/src/NpcForge.Api/Program.cs`
- Test: `backend/tests/NpcForge.IntegrationTests/Auth/AuthTests.cs`, `TestAuthExtensions.cs`

**Interfaces:**
- Consumes: `AppUser`, `AppDbContext`, `ApiFactory` (Task 2).
- Produces:
  - `JwtOptions { string Key; string Issuer = "NpcForge"; string Audience = "NpcForge"; int ExpiresMinutes = 1440 }` (seção `Jwt`; validação no startup: `Key` com pelo menos 32 caracteres → `ValidateOnStart`)
  - `JwtTokenService.CreateToken(AppUser user) → (string Token, DateTime ExpiresAt)`; claims `sub` = user id, `email`, `name` = DisplayName
  - `ClaimsPrincipalExtensions.GetUserId(this ClaimsPrincipal) → string` (lê `sub`; JWT Bearer configurado com `MapInboundClaims = false`)
  - DTOs: `RegisterRequest(string Email, string Password, string DisplayName)` com `[Required]`, `[EmailAddress]`, `DisplayName` `[MaxLength(50)]`; `LoginRequest(string Email, string Password)`; `LoginResponse(string Token, DateTime ExpiresAt)`; `MeResponse(string Id, string Email, string DisplayName)`
  - Rotas: `POST /api/auth/register` → 201 `MeResponse`; `POST /api/auth/login` → 200 `LoginResponse` | 401; `GET /api/auth/me` → 200 `MeResponse` | 401
  - Helper de teste: `Task<HttpClient> ApiFactory.CreateAuthenticatedClientAsync()` — registra um usuário com e-mail único (`{Guid}@test.local`), faz login e devolve o client com o header `Authorization: Bearer`.

Configuração em `Program.cs`: `AddIdentityCore<AppUser>()` + `AddEntityFrameworkStores<AppDbContext>()`, senha com `RequiredLength = 8`, `RequireDigit = true`, `RequireNonAlphanumeric = false`, `RequireUppercase = false`, `RequireLowercase = true`, `User.RequireUniqueEmail = true`; `AddProblemDetails()`; `JsonStringEnumConverter`; política de autorização padrão exigindo usuário autenticado (fallback), com `[AllowAnonymous]` em register e login.

- [ ] **Step 1: Escrever os testes que falham**

- `Register_ValidData_Returns201`
- `Register_DuplicateEmailDifferentCase_Returns400` — registra `joao-{guid}@x.com` e depois a mesma string com `J` maiúsculo
- `Register_WeakPassword_Returns400` — senha `"abc"`; resposta é `ValidationProblemDetails`
- `Login_ValidCredentials_ReturnsTokenAndMeWorks` — com o token, `GET /api/auth/me` devolve o e-mail e o `DisplayName` registrados
- `Login_WrongPassword_Returns401` — corpo com `Content-Type: application/problem+json`
- `Me_WithoutToken_Returns401`

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~AuthTests`
Expected: FAIL (404 nas rotas de auth).

- [ ] **Step 3: Implementar opções, serviço de token, controller e configuração**

Erros do Identity no registro viram `ValidationProblem` (chave = código do erro do Identity, mensagem = descrição).

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~AuthTests`
Expected: 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat: add registration, login and JWT authentication"
```

---

### Task 4: Campanhas

**Files:**
- Create: `backend/src/NpcForge.Core/Campaigns/CampaignDtos.cs`, `ICampaignService.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/CampaignService.cs`
- Create: `backend/src/NpcForge.Api/Controllers/CampaignsController.cs`
- Modify: `backend/src/NpcForge.Infrastructure/DependencyInjection.cs` (registrar `ICampaignService`)
- Test: `backend/tests/NpcForge.IntegrationTests/Campaigns/CampaignsTests.cs`

**Interfaces:**
- Consumes: `GetUserId()` (Task 3), `CreateAuthenticatedClientAsync()` (Task 3).
- Produces:
  - `CampaignRequest(string Name, string? Description, string? Setting)` com `[Required]`, `[MaxLength]` de `NpcLimits`
  - `CampaignDto(Guid Id, string Name, string? Description, string? Setting, int NpcCount, DateTime CreatedAt, DateTime UpdatedAt)`
  - `ICampaignService`:
    - `Task<IReadOnlyList<CampaignDto>> ListAsync(string userId, CancellationToken ct)` — ordenado por `UpdatedAt` decrescente
    - `Task<CampaignDto?> GetAsync(string userId, Guid id, CancellationToken ct)`
    - `Task<CampaignDto> CreateAsync(string userId, CampaignRequest request, CancellationToken ct)`
    - `Task<CampaignDto?> UpdateAsync(string userId, Guid id, CampaignRequest request, CancellationToken ct)`
    - `Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)`
  - `null`/`false` → o controller responde 404.
  - Rotas: `GET/POST /api/campaigns`, `GET/PUT/DELETE /api/campaigns/{id}`; POST → 201 com `Location`; PUT → 200; DELETE → 204.

- [ ] **Step 1: Escrever os testes que falham**

- `Create_ThenGet_ReturnsCampaignWithZeroNpcs`
- `Update_ChangesFieldsAndUpdatedAt`
- `Delete_Returns204_ThenGetReturns404`
- `List_ReturnsOnlyOwnCampaigns_MostRecentlyUpdatedFirst`
- `OtherUser_GetPutDelete_Returns404` — usuário B tenta acessar a campanha do usuário A
- `Create_EmptyName_Returns400` e `Create_NameWith101Chars_Returns400`
- `WithoutToken_Returns401`

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~CampaignsTests`
Expected: FAIL (404 na rota).

- [ ] **Step 3: Implementar DTOs, `ICampaignService`, `CampaignService` e `CampaignsController`**

Toda query filtra por `UserId`. `NpcCount` via `c.Npcs.Count()` na projeção. `CreatedAt`/`UpdatedAt` vêm de `TimeProvider.GetUtcNow().UtcDateTime`.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~CampaignsTests`
Expected: 8 PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat: add campaign CRUD scoped to the current user"
```

---

### Task 5: NPCs — CRUD, filtros e paginação

**Files:**
- Create: `backend/src/NpcForge.Core/Npcs/NpcDtos.cs`, `NpcQuery.cs`, `INpcService.cs`
- Create: `backend/src/NpcForge.Core/Common/PagedResult.cs`, `ServiceResult.cs`
- Create: `backend/src/NpcForge.Core/Validation/ChallengeRatingAttribute.cs`, `HitDiceAttribute.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/NpcService.cs`, `NpcMapping.cs`
- Create: `backend/src/NpcForge.Api/Controllers/NpcsController.cs`
- Modify: `backend/src/NpcForge.Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/NpcForge.IntegrationTests/Npcs/NpcsCrudTests.cs`, `NpcsListTests.cs`

**Interfaces:**
- Consumes: `DndRules` (Task 1), `NpcLimits` e entidades (Task 2), helpers de teste (Task 3), rotas de campanha (Task 4).
- Produces:
  - `StatBlockRequest` — todos os campos de `StatBlock` exceto `NpcId`; `[Range(1, 30)]` em `ArmorClass` e nos seis atributos; `[Range(1, int.MaxValue)]` em `HitPoints`; `[ChallengeRating]`; `[HitDice]`; `[MaxLength]` de `NpcLimits`; listas `Traits`, `Actions`, `Reactions` (padrão vazio).
  - `StatBlockDto` — os mesmos campos + `AbilityModifiers Modifiers` (`record AbilityModifiers(int Str, int Dex, int Con, int Int, int Wis, int Cha)`) + `int ProficiencyBonus`.
  - `CreateNpcRequest(Guid? CampaignId, string Name, string? Race, string? Occupation, string? Appearance, string? Personality, string? Background, string? Notes, StatBlockRequest? StatBlock)`
  - `UpdateNpcRequest` — os mesmos campos, **sem** `StatBlock`.
  - `NpcDto(Guid Id, Guid? CampaignId, string Name, …campos narrativos…, StatBlockDto? StatBlock, DateTime CreatedAt, DateTime UpdatedAt)`
  - `NpcSummaryDto(Guid Id, string Name, string? Race, string? Occupation, Guid? CampaignId, string? CampaignName, bool HasStatBlock, string? ChallengeRating, DateTime UpdatedAt)`
  - `NpcQuery { string? CampaignId; string? Search; bool? HasStatBlock; int Page = 1; int PageSize = 20 }` — `CampaignId` aceita um GUID ou `"none"`
  - `PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`
  - `enum ServiceError { NotFound, InvalidCampaign }`; `record ServiceResult<T>(T? Value, ServiceError? Error)` com `static Ok(T)` e `static Fail(ServiceError)`
  - `INpcService`:
    - `Task<PagedResult<NpcSummaryDto>> ListAsync(string userId, NpcQuery query, CancellationToken ct)`
    - `Task<NpcDto?> GetAsync(string userId, Guid id, CancellationToken ct)`
    - `Task<ServiceResult<NpcDto>> CreateAsync(string userId, CreateNpcRequest request, CancellationToken ct)`
    - `Task<ServiceResult<NpcDto>> UpdateAsync(string userId, Guid id, UpdateNpcRequest request, CancellationToken ct)`
    - `Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)`
  - Controller: `NotFound` → 404; `InvalidCampaign` → 400 `ValidationProblem` com chave `campaignId` e mensagem "Campanha não encontrada."
  - Rotas: `GET /api/npcs` (query string = `NpcQuery`), `POST /api/npcs` → 201 + `Location`, `GET/PUT/DELETE /api/npcs/{id}` (PUT → 200, DELETE → 204).

Regras do `ListAsync`:
- `Page < 1` → 1; `PageSize` limitado a 1..100.
- `Search` busca em `Name`, `Race` e `Occupation` com `EF.Functions.ILike`, **escapando `\`, `%` e `_`** antes de montar `%termo%`.
- `CampaignId` diferente de `"none"` e que não é GUID válido → lista vazia.
- Ordem: `UpdatedAt` decrescente, depois `Id`.

- [ ] **Step 1: Escrever os testes de CRUD que falham (`NpcsCrudTests`)**

- `Create_NarrativeNpc_Returns201AndGetReturnsItWithoutStatBlock`
- `Create_WithStatBlock_ReturnsDerivedValues` — `Str = 18`, `ChallengeRating = "5"` → `StatBlock.Modifiers.Str == 4`, `StatBlock.ProficiencyBonus == 3`; `Actions[0].AttackBonus` preservado
- `Create_InvalidStatBlock_Returns400` — theory: `Str = 31`, `ChallengeRating = "1/3"`, `HitDice = "4d"`, `HitPoints = 0`; cada um retorna 400 com a chave do campo em `errors`
- `Create_WithOtherUsersCampaign_Returns400WithCampaignIdError`
- `Update_MovesNpcToAnotherCampaign_AndKeepsStatBlock`
- `Delete_Returns204_ThenGetReturns404`
- `OtherUser_GetPutDelete_Returns404`
- `DeletingCampaign_KeepsNpcWithNullCampaignId` (via API)
- `CampaignNpcCount_ReflectsNpcs` — 2 NPCs na campanha → `NpcCount == 2`

- [ ] **Step 2: Escrever os testes de listagem que falham (`NpcsListTests`)**

Cada teste usa um usuário novo, com NPCs criados pela API:
- `FiltersByCampaignId` e `FiltersByCampaignNone`
- `FiltersByHasStatBlock` (true e false)
- `Search_MatchesNameRaceOrOccupation_CaseInsensitiveWithAccentedLetters` — NPC com `Race = "anão"`; `search=ANÃO` encontra (buscar sem acento, `anao`, **não** precisa encontrar na v1)
- `Search_WildcardCharactersAreLiteral` — com NPCs "Bob" e "100% Bob", `search=%` devolve só "100% Bob"
- `Paging_ClampsInvalidValues` — `pageSize=1000` → `PageSize == 100`; `page=-1&pageSize=0` → `Page == 1`, `PageSize == 1`
- `Paging_ReturnsTotalCountAndRequestedPage` — 3 NPCs, `pageSize=2&page=2` → 1 item, `TotalCount == 3`
- `List_ExcludesOtherUsersNpcs`

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter "FullyQualifiedName~Npcs"`
Expected: FAIL (404 na rota).

- [ ] **Step 4: Implementar os atributos de validação, os DTOs, `NpcMapping`, `NpcService` e `NpcsController`**

`[ChallengeRating]` e `[HitDice]` delegam para `DndRules.IsValidChallengeRating` / `IsValidHitDice` (aceitando `null` — a obrigatoriedade fica no `[Required]`). `NpcMapping` monta o `StatBlockDto` com `DndRules.AbilityModifier` e `DndRules.ProficiencyBonus`. `UpdateAsync` atualiza `UpdatedAt`; `CreateAsync` valida que a campanha (se informada) é do usuário.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter "FullyQualifiedName~Npcs"`
Expected: todos PASS.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: add NPC CRUD with filters, search and pagination"
```

---

### Task 6: Adicionar e remover ficha

**Files:**
- Modify: `backend/src/NpcForge.Core/Npcs/INpcService.cs`
- Modify: `backend/src/NpcForge.Infrastructure/Services/NpcService.cs`
- Modify: `backend/src/NpcForge.Api/Controllers/NpcsController.cs`
- Test: `backend/tests/NpcForge.IntegrationTests/Npcs/StatBlockTests.cs`

**Interfaces:**
- Consumes: `StatBlockRequest`, `NpcDto`, `NpcMapping` (Task 5).
- Produces:
  - `Task<NpcDto?> SetStatBlockAsync(string userId, Guid npcId, StatBlockRequest request, CancellationToken ct)` — cria ou substitui a ficha; atualiza `Npc.UpdatedAt`
  - `Task<NpcDto?> RemoveStatBlockAsync(string userId, Guid npcId, CancellationToken ct)` — sem ficha é no-op e devolve o NPC
  - Rotas: `PUT /api/npcs/{id}/statblock` → 200 `NpcDto`; `DELETE /api/npcs/{id}/statblock` → 200 `NpcDto`; `null` → 404.

- [ ] **Step 1: Escrever os testes que falham**

- `Put_AddsStatBlockToNarrativeNpc`
- `Put_ReplacesExistingStatBlock` — ND "1" → "10"; `ProficiencyBonus` passa de 2 para 4; listas antigas substituídas
- `Put_InvalidStatBlock_Returns400` (`Dex = 0`)
- `Delete_RemovesStatBlock_NpcRemainsNarrative` — depois disso, `GET /api/npcs?hasStatBlock=false` inclui o NPC
- `OtherUser_PutDelete_Returns404`

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~StatBlockTests`
Expected: FAIL (405/404 nas rotas).

- [ ] **Step 3: Implementar os dois métodos e as rotas**

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~StatBlockTests`
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat: add endpoints to set and remove an NPC stat block"
```

---

### Task 7: Docker, CORS, migrations automáticas e README

**Files:**
- Create: `backend/src/NpcForge.Api/Dockerfile` (multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`; contexto de build = `backend/`)
- Create: `docker-compose.yml` (serviços `postgres` com volume nomeado e healthcheck `pg_isready`; `api` dependendo de `postgres` saudável, porta `8080:8080`, `ASPNETCORE_ENVIRONMENT=Development`)
- Create: `.env.example` (`POSTGRES_PASSWORD=`, `JWT_KEY=` com instrução de gerar 32+ caracteres)
- Create: `README.md` (o que é o projeto, pré-requisitos, como rodar com Docker, como rodar os testes; seções de prints e decisões de arquitetura marcadas para o Plano 6)
- Modify: `backend/src/NpcForge.Api/Program.cs`
- Modify: `backend/src/NpcForge.Api/appsettings.json` (`Cors:AllowedOrigins: ["http://localhost:4200"]`, `Jwt:Issuer/Audience/ExpiresMinutes`, sem `Key` e sem senha)
- Test: `backend/tests/NpcForge.IntegrationTests/CorsTests.cs`

**Interfaces:**
- Consumes: tudo das tasks anteriores.
- Produces:
  - Política CORS padrão com as origens de `Cors:AllowedOrigins`, qualquer header e método.
  - Em `Development`: `db.Database.MigrateAsync()` no startup. Em `Testing` não (o `ApiFactory` já migra).
  - Para rodar a API fora do Docker: `dotnet user-secrets` com `Jwt:Key` e `ConnectionStrings:Default` (documentado no README).

- [ ] **Step 1: Escrever os testes que falham**

- `Preflight_FromAllowedOrigin_ReturnsAllowOriginHeader` — `OPTIONS /api/campaigns` com `Origin: http://localhost:4200` e `Access-Control-Request-Method: GET` → header `Access-Control-Allow-Origin: http://localhost:4200`
- `Preflight_FromOtherOrigin_HasNoAllowOriginHeader` — `Origin: http://evil.example`

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/NpcForge.IntegrationTests --filter FullyQualifiedName~CorsTests`
Expected: FAIL no primeiro teste (header ausente).

- [ ] **Step 3: Implementar CORS e migration no startup; criar Dockerfile, Compose, `.env.example` e README**

- [ ] **Step 4: Rodar a suíte inteira**

Run: `dotnet test backend/NpcForge.slnx`
Expected: todos os testes unitários e de integração PASS.

- [ ] **Step 5: Verificar o ambiente Docker de ponta a ponta**

Run: copiar `.env.example` para `.env` com valores preenchidos; `docker compose up -d --build`; depois registrar com `POST http://localhost:8080/api/auth/register`, fazer login e chamar `GET http://localhost:8080/api/campaigns` com o token.
Expected: 201, 200 com token, 200 com `[]`. Encerrar com `docker compose down`.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "chore: add Docker setup, CORS, dev migrations and README"
```
