# NpcForge — Plano 3: IA (Ollama) e limite diário de gerações

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A partir de um prompt curto ("anão ferreiro rabugento, ND 2"), a IA gera um NPC completo (com ou sem ficha D&D 5e) que preenche o formulário de criação; respostas ruins do modelo são corrigidas ou recusadas, e cada usuário tem até 5 gerações por dia.

**Architecture:** No Core: `IAiProvider` (só fala com o modelo, sem saber de RPG), `PromptBuilder` (monta os prompts a partir de templates), `NpcDraftValidator` (parse + correção do JSON, código puro) e `NpcGenerationService` (orquestra limite, prompt, chamada, nova tentativa e timeout). Na Infrastructure: `OllamaProvider` (HTTP para a API do Ollama) e `AiUsageService` (contador diário no Postgres com reserva atômica). Na Api: `AiController`. No front: um card de prompt no topo de `/npcs/new` que preenche o formulário existente. Tudo isso fica atrás da chave `Ai:Enabled` (padrão `false`): a versão web roda sem IA, e quem quiser liga a IA localmente seguindo o tutorial do README.

**Tech Stack:** .NET 10, Ollama (API `/api/chat` com saída estruturada por JSON schema), EF Core + Npgsql (SQL atômico), xUnit + Testcontainers, Angular 22 + Vitest.

**Spec:** `docs/superpowers/specs/2026-10-28-npcforge-design.md` (seção 5 "IA" e `/features`, seção 6 inteira — incluindo "IA ligada ou desligada (feature flag)" —, seção 7 "Fluxo de criação" e contador, seção 8 itens de IA e limite diário).

**Fora deste plano:** `suggest-field` e botões ✨ (Plano 4), provedor em nuvem e limite por minuto (Plano 6).

## Global Constraints

- **Feature flag `Ai:Enabled`, padrão `false`** (spec §6): desligada, os endpoints `/api/ai/*` respondem 404 "A IA está desativada nesta instalação.", nenhum serviço de IA é registrado nem exige configuração, e o frontend esconde o card de IA. Uma única base de código — nada de branches separados por versão.
- **Ollama nativo no Windows (decisão deste plano, diverge da spec):** a spec previa o Ollama no `docker compose`; como a máquina de desenvolvimento já tem o Ollama instalado e uma GPU NVIDIA (RTX 5060 Ti, 16 GB), usamos o Ollama nativo — no Windows, GPU dentro de container exige configuração extra. A API no container acessa `http://host.docker.internal:11434`; rodando a API fora do Docker, `http://localhost:11434`. O `docker-compose.yml` ganha um serviço `ollama` opcional sob o profile `ollama-docker` para quem não tem o Ollama instalado.
- Modelo padrão: `qwen2.5:14b` (bom em português e em JSON estruturado; ~9 GB, cabe nos 16 GB de VRAM). Configurável em `Ai:Ollama:Model`.
- Configuração (seção `Ai`): `Enabled` = `false`; `Provider` = `Ollama`; `Ollama:BaseUrl`; `Ollama:Model`; `TimeoutSeconds` = 60; `DailyGenerationLimit` = 5.
- Todo o conteúdo gerado em **português do Brasil**; regras de D&D 5e (Livro do Jogador 2024).
- O endpoint de geração **não salva** nada: devolve um rascunho; salvar continua sendo `POST /api/npcs`.
- Limite diário: dia em **UTC**, conta só gerações bem-sucedidas, reserva atômica no banco (requisições simultâneas não passam do limite), devolução da reserva quando a geração falha ou é cancelada.
- Status: 400 validação/campanha alheia, 429 limite atingido (`ProblemDetails` com extensão `resetsAt`), 502 IA falhou/indisponível, 504 timeout. Títulos em português.
- Nenhum teste depende de IA real nem de rede externa (`FakeAiProvider` nos testes de integração; `HttpMessageHandler` falso nos testes do `OllamaProvider`).
- Frontend: standalone, signals, OnPush, textos em português; erros via `errorMessages`.

## Review Focus

1. **Modelo devolve lixo:** texto antes/depois do JSON, ```` ```json ```` cercando o JSON, campos faltando, números como string (`"str": "16"`) — o validador extrai e converte o que der e corrige o resto com aviso, em vez de responder 502 de cara. Teste na Task 2.
2. **Corrida no limite:** 10 gerações simultâneas com limite 5 → exatamente 5 sucessos e 5 respostas 429. Teste na Task 4.
3. **Ollama desligado ou modelo não baixado:** a resposta é 502 com "A IA não está disponível agora." e a reserva do limite é devolvida — o usuário não perde uma geração do dia. Teste na Task 5.
4. **ND pedido x ND gerado:** com `challengeRating: "2"` no pedido, o rascunho sai com ND 2 mesmo que o modelo invente outro (com aviso). Sem ficha pedida, uma ficha devolvida pelo modelo é descartada. Teste na Task 5.
5. **Gerar por cima de um formulário já preenchido:** a geração substitui os campos gerados, mas preserva a campanha escolhida e as anotações do mestre. Teste na Task 8.
6. **IA desligada (versão web):** a API sobe sem nenhuma configuração do Ollama, `/api/ai/*` responde 404 em português, `/api/features` responde `{ "ai": false }` sem login, e o front esconde o card — inclusive se `/api/features` falhar. Testes nas Tasks 6 e 8.

---

### Task 1: Regras de D&D auxiliares (média de PV e ND mais próximo)

**Files:**
- Modify: `backend/src/NpcForge.Core/Rules/DndRules.cs`
- Test: `backend/tests/NpcForge.UnitTests/Rules/DndRulesTests.cs`

**Interfaces:**
- Produces:
  - `int AverageHitPoints(string hitDice)` — média do livro: `floor(qtd × (lados + 1) / 2) + modificador`, mínimo 1; lança `ArgumentException` se inválido. Aceita os mesmos formatos de `IsValidHitDice`.
  - `string NearestChallengeRating(double value)` — ND oficial mais próximo (frações valem 0,125 / 0,25 / 0,5); empate vai para o menor; valores < 0 → `"0"`, > 30 → `"30"`.
  - `double ChallengeRatingValue(string cr)` — `"1/8"` → 0,125 etc.; lança `ArgumentException` se inválido.

- [ ] **Step 1: Escrever os testes que falham**

- `AverageHitPoints`: `"2d8 + 2"` → 11; `"4d8+4"` → 22; `"1d4 – 1"` → 1 (2 − 1); `"1d4 - 5"` → 1 (mínimo); `"18d8 + 54"` → 135; `"4d"` lança.
- `NearestChallengeRating`: 0.3 → `"1/4"`; 0.4 → `"1/2"`; 7.4 → `"7"`; 7.5 → `"7"` (empate vai para o menor); 31 → `"30"`; -2 → `"0"`.
- `ChallengeRatingValue`: `"1/8"` → 0.125; `"12"` → 12.

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test backend/tests/NpcForge.UnitTests` → erro de compilação.
- [ ] **Step 3: Implementar** (reaproveita o `HitDiceRegex` existente para o parse).
- [ ] **Step 4: Rodar e ver passar.**
- [ ] **Step 5: Commit** — `feat: add hit point average and nearest challenge rating rules`

---

### Task 2: Validador e corretor do rascunho da IA

**Files:**
- Create: `backend/src/NpcForge.Core/Ai/NpcDraft.cs`
- Create: `backend/src/NpcForge.Core/Ai/NpcDraftValidator.cs`
- Test: `backend/tests/NpcForge.UnitTests/Ai/NpcDraftValidatorTests.cs`

**Interfaces:**
- Consumes: `DndRules` (Task 1), `NpcLimits`, `StatBlockRequest`/`StatBlockFeatureDto`/`StatBlockActionDto`/`CreatureSize` (existentes).
- Produces:
  - `record NpcDraft(string Name, string? Race, string? Occupation, string? Appearance, string? Personality, string? Background, StatBlockRequest? StatBlock)`
  - `record DraftResult(NpcDraft? Draft, IReadOnlyList<string> Warnings, string? ParseError)` — `ParseError != null` quando não deu para extrair um JSON utilizável (dispara a nova tentativa).
  - `static class NpcDraftValidator`:
    - `DraftResult Parse(string rawModelOutput, bool withStatBlock, string? requestedChallengeRating)`

Regras do `Parse` (cada correção gera um aviso em português no formato `"Ajustamos <campo>: <de> → <para>."` ou equivalente):
1. **Extração:** remove cercas ```` ``` ````/```` ```json ````; se houver texto em volta, usa o trecho do primeiro `{` ao último `}`. JSON inválido ou sem `name` → `ParseError` (mensagem curta descrevendo o problema, usada no prompt da nova tentativa).
2. **Leitura tolerante:** propriedades sem diferenciar maiúsculas; números aceitos como número ou string numérica; listas ausentes → vazias; textos com espaços nas pontas → `Trim`; texto vazio → `null`.
3. **Textos:** acima do limite de `NpcLimits` → truncado (aviso).
4. **Ficha** — só quando `withStatBlock`; se `!withStatBlock`, a ficha é descartada sem aviso. Se `withStatBlock` e o modelo não mandou ficha → `ParseError`.
   - atributos fora de 1–30 → limitados (aviso); CA fora de 1–30 → limitada (aviso);
   - `Size` desconhecido → `Medium` (aviso); aceita os nomes em inglês e os rótulos em português (`Médio`, `Grande`...);
   - `HitDice` inválido → string vazia (aviso "informe os dados de vida");
   - PV ≤ 0 ou ausente → `AverageHitPoints(HitDice)` se válido, senão 1 (aviso);
   - ND fora da lista → `NearestChallengeRating` (aviso); `requestedChallengeRating` informado e diferente do gerado → usa o pedido (aviso);
   - `HitDice` válido é normalizado com `NormalizeHitDice`;
   - listas acima de 30 itens → corta (aviso); itens sem nome são descartados (aviso).

- [ ] **Step 1: Escrever os testes que falham** (um JSON de exemplo válido como base em `TestDrafts.cs`, variado com substituições):
- `Parse_ValidJson_ReturnsDraftWithoutWarnings`
- `Parse_JsonInsideCodeFenceAndProse_IsExtracted` — "Claro! ```json {…} ``` Espero que goste."
- `Parse_NumbersAsStrings_AreConverted` — `"str": "16"`
- `Parse_InvalidJson_ReturnsParseError` e `Parse_MissingName_ReturnsParseError`
- `Parse_WithStatBlockButNoneReturned_ReturnsParseError`
- `Parse_NotWithStatBlock_DropsStatBlockSilently`
- `Parse_AbilityAbove30_IsClampedWithWarning` — `str: 35` → 30, aviso contém "Força"
- `Parse_InvalidChallengeRating_UsesNearestWithWarning` — `"7.4"` → `"7"`
- `Parse_RequestedChallengeRatingWins` — gerado `"5"`, pedido `"2"` → `"2"` + aviso
- `Parse_ZeroHitPoints_RecalculatedFromHitDice` — `"2d8 + 2"`, PV 0 → 11
- `Parse_InvalidHitDice_ClearedWithWarning`
- `Parse_PortugueseSizeLabel_IsAccepted` — `"Grande"` → `Large`
- `Parse_TooLongBackground_IsTruncatedWithWarning` — 5000 caracteres → 4000
- `Parse_HitDiceIsNormalizedToBookFormat` — `"4d8+4"` → `"4d8 + 4"`

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar** com `System.Text.Json` (`JsonDocument`), percorrendo as propriedades manualmente para a leitura tolerante.
- [ ] **Step 4: Rodar e ver passar.**
- [ ] **Step 5: Commit** — `feat: parse and correct AI-generated NPC drafts`

---

### Task 3: Prompts e provedor Ollama

**Files:**
- Create: `backend/src/NpcForge.Core/Ai/IAiProvider.cs`, `AiOptions.cs`, `AiProviderException.cs`
- Create: `backend/src/NpcForge.Core/Ai/PromptBuilder.cs`
- Create: `backend/src/NpcForge.Core/Ai/Prompts/system.txt`, `generate-npc.txt`, `retry.txt`, `npc-draft.schema.json`, `stat-block.schema.json` (embedded resources no `.csproj`)
- Create: `backend/src/NpcForge.Infrastructure/Ai/OllamaProvider.cs`
- Modify: `backend/src/NpcForge.Infrastructure/DependencyInjection.cs` (opções `Ai` sempre registradas — o `Enabled` é lido pelo filtro da Task 6; `PromptBuilder` e `AddHttpClient<IAiProvider, OllamaProvider>` com `BaseAddress` de `Ai:Ollama:BaseUrl` **só quando `Ai:Enabled` é `true`**; com `ValidateOnStart` nas opções do Ollama apenas nesse caso)
- Modify: `backend/src/NpcForge.Api/appsettings.json` (seção `Ai` com os padrões)
- Test: `backend/tests/NpcForge.UnitTests/Ai/PromptBuilderTests.cs`, `backend/tests/NpcForge.UnitTests/Ai/OllamaProviderTests.cs` (o projeto de testes unitários passa a referenciar a Infrastructure)

**Interfaces:**
- Produces:
  - `interface IAiProvider { Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken ct); }`
  - `class AiProviderException(string message, Exception? inner = null) : Exception` — modelo indisponível, modelo não encontrado, resposta HTTP de erro.
  - `class AiOptions { const Section = "Ai"; bool Enabled = false; string Provider = "Ollama"; int TimeoutSeconds = 60 [Range(5, 600)]; int DailyGenerationLimit = 5 [Range(1, 1000)]; OllamaOptions Ollama }`, `class OllamaOptions { string BaseUrl = "http://localhost:11434"; string Model = "qwen2.5:14b"; }`
  - `record AiPrompt(string System, string User, string JsonSchema)`
  - `class PromptBuilder` (singleton, carrega os recursos uma vez):
    - `AiPrompt BuildGenerateNpc(string userPrompt, string? campaignSetting, bool withStatBlock, string? challengeRating)`
    - `AiPrompt BuildRetry(AiPrompt original, string parseError)` — mesmo system/schema; user = original + bloco `retry.txt` com o erro
  - `OllamaProvider : IAiProvider` — `POST /api/chat` com `{ model, stream: false, format: <schema como objeto JSON>, options: { temperature: 0.8 }, messages: [system, user] }`; devolve `message.content`. `HttpRequestException` (Ollama desligado) → `AiProviderException("A IA não está disponível agora.")`; HTTP 404 com "not found" no corpo → `AiProviderException` citando o modelo e o comando `ollama pull`; `OperationCanceledException` propaga.

Conteúdo dos prompts (decisões):
- `system.txt`: assistente de mestre de D&D 5e (regras do Livro do Jogador 2024); responder **somente** JSON no schema; tudo em português do Brasil; NPCs coerentes com o pedido e com o cenário; números plausíveis para o ND; dados de vida no formato `2d8 + 2`; nunca incluir texto fora do JSON.
- `generate-npc.txt` com marcadores `{{prompt}}`, `{{setting}}` (ou "não informado"), `{{statBlockInstruction}}` ("inclua uma ficha completa de combate com ND {{cr}}" / "com ND adequado ao pedido" / "não inclua ficha").
- O schema `npc-draft.schema.json` tem os campos narrativos; quando `withStatBlock`, o `PromptBuilder` injeta `stat-block.schema.json` como propriedade `statBlock` obrigatória.
- O texto do usuário entra entre delimitadores (`<<<` `>>>`) e o system prompt avisa que instruções dentro dele não mudam o formato da resposta (mitigação básica de prompt injection; a validação do Task 2 é a proteção real).

- [ ] **Step 1: Escrever os testes que falham**
- `PromptBuilderTests`:
  - `BuildGenerateNpc_IncludesPromptSettingAndDelimiters`
  - `BuildGenerateNpc_WithoutSetting_SaysNotInformed`
  - `BuildGenerateNpc_WithStatBlockAndCr_RequiresStatBlockInSchemaAndMentionsCr` — `JsonDocument` do schema tem `statBlock` em `required`
  - `BuildGenerateNpc_WithoutStatBlock_SchemaHasNoStatBlock`
  - `BuildRetry_AppendsParseError`
- `OllamaProviderTests` (handler falso registra a requisição e devolve resposta pronta):
  - `SendsModelSchemaAndMessagesWithStreamFalse` — corpo tem `model`, `stream: false`, `format` como **objeto** (não string), 2 mensagens com os papéis certos
  - `ReturnsMessageContent`
  - `ConnectionRefused_ThrowsAiProviderExceptionInPortuguese`
  - `ModelNotFound_ThrowsAiProviderExceptionMentioningPull`

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar** prompts, schemas, `PromptBuilder`, `OllamaProvider`, opções e DI.
- [ ] **Step 4: Rodar e ver passar.**
- [ ] **Step 5: Commit** — `feat: add prompt builder and Ollama provider`

---

### Task 4: Limite diário de gerações

**Files:**
- Create: `backend/src/NpcForge.Core/Entities/AiUsage.cs`
- Create: `backend/src/NpcForge.Core/Ai/IAiUsageService.cs`, `AiUsageDto.cs`
- Create: `backend/src/NpcForge.Infrastructure/Persistence/Configurations/AiUsageConfiguration.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/AiUsageService.cs`
- Create: migration `AddAiUsage`
- Modify: `AppDbContext` (`DbSet<AiUsage> AiUsages`), `DependencyInjection.cs`
- Test: `backend/tests/NpcForge.UnitTests/Ai/AiUsageClockTests.cs`, `backend/tests/NpcForge.IntegrationTests/Ai/AiUsageServiceTests.cs`

**Interfaces:**
- Produces:
  - `AiUsage { string UserId; DateOnly Date; int GenerationCount }`, PK `(UserId, Date)`, FK para `AspNetUsers` com cascade.
  - `record AiUsageDto(int Used, int Limit, DateTime ResetsAt)`
  - `static class AiUsageClock { DateOnly Today(DateTimeOffset now); DateTime ResetsAt(DateTimeOffset now) }` — meia-noite UTC seguinte.
  - `AiUsageService(AppDbContext db, TimeProvider time, IOptions<AiOptions> options)` — "hoje" e `resetsAt` sempre via `AiUsageClock` com `time.GetUtcNow()` (spec §6/§8: o horário vem de `TimeProvider` injetado, o que permite simular a virada do dia).
  - `interface IAiUsageService`:
    - `Task<AiUsageDto> GetAsync(string userId, CancellationToken ct)`
    - `Task<bool> TryReserveAsync(string userId, CancellationToken ct)` — um único comando SQL: `INSERT … VALUES (@user, @today, 1) ON CONFLICT ("UserId","Date") DO UPDATE SET "GenerationCount" = "AiUsages"."GenerationCount" + 1 WHERE "AiUsages"."GenerationCount" < @limit RETURNING "GenerationCount"` → `true` se retornou linha.
    - `Task ReleaseAsync(string userId, CancellationToken ct)` — decrementa se > 0; usa `CancellationToken.None` internamente (a devolução precisa acontecer mesmo se a requisição foi cancelada).

- [ ] **Step 1: Escrever os testes que falham**
- Unitários: `Today_UsesUtcDate` (23:30 em −03:00 já é o dia seguinte em UTC); `ResetsAt_IsNextUtcMidnight`.
- Integração (`[Collection("api")]`, serviço resolvido do `ApiFactory`, usuário novo por teste):
  - `TryReserve_AllowsUpToLimit_ThenRefuses` — 5 × `true`, 6ª `false`; `GetAsync` → `Used = 5`
  - `Release_GivesBackOneReservation`
  - `Release_AtZero_StaysAtZero`
  - `Reservations_AreCountedPerUtcDay` — linha de **ontem** com 5 inserida direto no banco não impede reservar hoje
  - `ConcurrentReservations_NeverExceedLimit` — 10 `TryReserveAsync` em paralelo (cada um com seu escopo/DbContext) → exatamente 5 `true`

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar entidade, configuração, serviço e gerar a migration** (`dotnet ef migrations add AddAiUsage …`).
- [ ] **Step 4: Rodar e ver passar.**
- [ ] **Step 5: Commit** — `feat: add atomic daily AI generation limit`

---

### Task 5: Serviço de geração e endpoints

**Files:**
- Create: `backend/src/NpcForge.Core/Ai/GenerateNpcRequest.cs`, `GenerateNpcResponse.cs`, `INpcGenerationService.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/NpcGenerationService.cs`
- Create: `backend/src/NpcForge.Api/Controllers/AiController.cs`
- Modify: `backend/src/NpcForge.Core/Common/ServiceResult.cs` (novos valores de `ServiceError`)
- Test: `backend/tests/NpcForge.IntegrationTests/Ai/FakeAiProvider.cs`, `backend/tests/NpcForge.IntegrationTests/Ai/GenerateNpcTests.cs`
- Modify: `backend/tests/NpcForge.IntegrationTests/ApiFactory.cs` (`Ai:Enabled = true`; troca `IAiProvider` pelo `FakeAiProvider` singleton; `Ai:TimeoutSeconds = 2`)

**Interfaces:**
- Consumes: `NpcDraftValidator` (Task 2), `PromptBuilder`/`IAiProvider`/`AiProviderException`/`AiOptions` (Task 3), `IAiUsageService` (Task 4).
- Produces:
  - `ServiceError` ganha `DailyLimitReached`, `AiUnavailable`, `AiTimeout`.
  - `record GenerateNpcRequest([Required, MaxLength(500)] string Prompt, Guid? CampaignId, bool WithStatBlock, [ChallengeRating] string? ChallengeRating)`
  - `record GenerateNpcResponse(NpcDraft Draft, IReadOnlyList<string> Warnings, AiUsageDto Usage)`
  - `interface INpcGenerationService { Task<ServiceResult<GenerateNpcResponse>> GenerateAsync(string userId, GenerateNpcRequest request, CancellationToken ct); }`
  - Rotas: `POST /api/ai/generate-npc` → 200 `GenerateNpcResponse`; `GET /api/ai/usage` → 200 `AiUsageDto`.
  - Mapeamento no controller: `InvalidCampaign` → 400 (`campaignId`); `DailyLimitReached` → 429 `ProblemDetails` título "Você atingiu o limite de gerações de hoje." + extensão `resetsAt`; `AiUnavailable` → 502 com a mensagem da exceção (ou "A IA não conseguiu gerar agora. Tente novamente."); `AiTimeout` → 504 "A IA demorou demais para responder. Tente novamente."
  - `FakeAiProvider : IAiProvider` — fila thread-safe de respostas (`Enqueue(string)`, `EnqueueFailure(Exception)`, `EnqueueDelay(TimeSpan)`); fila vazia → JSON válido padrão (com ficha); registra os prompts recebidos (`Calls`).

Fluxo do `GenerateAsync`:
1. Campanha informada e não pertence ao usuário → `InvalidCampaign` (antes de reservar).
2. `TryReserveAsync` falso → `DailyLimitReached`.
3. Monta o prompt (com o `Setting` da campanha), chama o provedor com `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter(TimeoutSeconds)`.
4. `ParseError` → **uma** nova tentativa com `BuildRetry`; segundo erro → `AiUnavailable`.
5. Sucesso → devolve rascunho, avisos e `GetAsync` atualizado.
6. Qualquer falha depois da reserva (`AiProviderException`, timeout, cancelamento pelo cliente, parse) → `ReleaseAsync` antes de retornar/propagar. Timeout (token do `CancelAfter`, não o do cliente) → `AiTimeout`.

- [ ] **Step 1: Escrever os testes que falham** (`GenerateNpcTests`, cada um com usuário novo; o fake é resetado no início de cada teste):
- `Generate_ReturnsDraftWarningsAndUsage` — `Usage.Used == 1`, `Limit == 5`
- `Generate_SendsCampaignSettingToTheModel` — `Calls[0].User` contém o `Setting`
- `Generate_WithOtherUsersCampaign_Returns400AndDoesNotConsume`
- `Generate_FixesBadValuesWithWarnings` — fake devolve `str: 35`
- `Generate_RetriesOnceOnInvalidJson` — fila: lixo, válido → 200; `Calls.Count == 2`; o 2º prompt contém o erro
- `Generate_TwoInvalidResponses_Returns502AndDoesNotConsume` — `GET /api/ai/usage` → `Used == 0`
- `Generate_AiUnavailable_Returns502InPortugueseAndDoesNotConsume` (Review Focus 3)
- `Generate_SlowModel_Returns504AndDoesNotConsume` — `EnqueueDelay(5s)` com timeout de 2 s
- `Generate_RequestedChallengeRatingWins` e `Generate_WithoutStatBlock_DropsModelStatBlock` (Review Focus 4)
- `Generate_SixthOfTheDay_Returns429WithResetsAt`
- `Generate_ConcurrentRequests_ExactlyLimitSucceed` — 10 em paralelo → 5 × 200, 5 × 429 (Review Focus 2)
- `Generate_EmptyPrompt_Returns400` e `Generate_WithoutToken_Returns401`
- `Usage_ForNewUser_IsZeroWithResetsAt`

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar serviço, controller e fake.**
- [ ] **Step 4: Rodar a suíte inteira** — `dotnet test backend/NpcForge.slnx` → todos PASS.
- [ ] **Step 5: Commit** — `feat: add AI NPC generation endpoint with retry, timeout and daily limit`

---

### Task 6: Chave `Ai:Enabled` e endpoint `/api/features`

**Files:**
- Create: `backend/src/NpcForge.Api/Ai/RequiresAiAttribute.cs` (filtro de recurso)
- Create: `backend/src/NpcForge.Api/Controllers/FeaturesController.cs`
- Modify: `backend/src/NpcForge.Api/Controllers/AiController.cs` (`[RequiresAi]` na classe)
- Test: `backend/tests/NpcForge.IntegrationTests/AiDisabledApiFactory.cs`, `backend/tests/NpcForge.IntegrationTests/Ai/AiFeatureFlagTests.cs`

**Interfaces:**
- Consumes: `AiOptions.Enabled` (Task 3), `AiController` (Task 5).
- Produces:
  - `class RequiresAiAttribute : Attribute, IFilterFactory` que cria um `IResourceFilter` — com `Ai:Enabled = false`, encerra a requisição com 404 `ProblemDetails` "A IA está desativada nesta instalação." **antes** de o controller ser criado (assim a falta dos serviços de IA no DI não causa erro 500).
  - `GET /api/features` (`[AllowAnonymous]`) → `record FeaturesDto(bool Ai)`.
  - `AiDisabledApiFactory : ApiFactory` com `Ai:Enabled = false` e **sem** nenhuma configuração `Ai:Ollama:*`; coleção xUnit própria `"api-ai-disabled"`.

- [ ] **Step 1: Escrever os testes que falham**
- Com `ApiFactory` (IA ligada): `Features_ReportsAiEnabled_WithoutLogin` → `{ "ai": true }` sem token.
- Com `AiDisabledApiFactory`: `Features_ReportsAiDisabled`; `GenerateNpc_WhenDisabled_Returns404InPortuguese`; `Usage_WhenDisabled_Returns404`; `CrudStillWorks_WhenAiDisabled` (cadastrar, criar campanha e NPC normalmente).
- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar filtro, controller e factory de teste.**
- [ ] **Step 4: Rodar a suíte inteira** — `dotnet test backend/NpcForge.slnx` → todos PASS.
- [ ] **Step 5: Commit** — `feat: add Ai:Enabled feature flag and features endpoint`

---

### Task 7: Ollama de verdade — configuração, compose, tutorial e teste manual

**Files:**
- Modify: `docker-compose.yml` (api: `Ai__Enabled: ${AI_ENABLED:-false}`, `Ai__Ollama__Model: ${OLLAMA_MODEL:-qwen2.5:14b}`, `Ai__Ollama__BaseUrl: http://host.docker.internal:11434`, `extra_hosts: ["host.docker.internal:host-gateway"]`; serviço `ollama` com image `ollama/ollama`, volume e porta 11434 sob `profiles: ["ollama-docker"]`)
- Modify: `.env.example` (`AI_ENABLED=false` e `OLLAMA_MODEL=qwen2.5:14b`, com comentário explicando)
- Modify: `README.md` — seção **"Rodando com IA localmente"** em formato de tutorial passo a passo:
  1. Instalar o Ollama (link oficial) e confirmar com `ollama --version`.
  2. Baixar o modelo: `ollama pull qwen2.5:14b` (~9 GB; precisa de GPU com ~10 GB de VRAM para ficar rápido).
  3. No `.env`: `AI_ENABLED=true` (e `OLLAMA_MODEL` se quiser outro).
  4. `docker compose up -d --build` e abrir "Novo NPC": o card "Gerar com IA" aparece.
  5. Sem GPU ou com pouca memória: usar um modelo menor (ex.: `qwen2.5:7b` ou `qwen2.5:3b`) e aumentar `Ai__TimeoutSeconds`.
  6. Sem Ollama instalado: `docker compose --profile ollama-docker up -d` e `docker compose exec ollama ollama pull qwen2.5:14b` (mais lento sem GPU), apontando `Ai__Ollama__BaseUrl` para `http://ollama:11434`.
  7. Problemas comuns: "A IA não está disponível agora" (Ollama desligado ou modelo não baixado) e como conferir com `ollama list`.
  - Também uma linha na seção "Como rodar" deixando claro que a IA vem desligada e apontando para o tutorial.

- [ ] **Step 1: Baixar o modelo e validar o Ollama**
Run: iniciar o Ollama; `ollama pull qwen2.5:14b`; `ollama list`.
Expected: modelo listado.
- [ ] **Step 2: Atualizar compose, `.env.example` e README; colocar `AI_ENABLED=true` no `.env` local; `docker compose up -d --build`.**
Expected: `GET /api/features` → `{ "ai": true }`. Com `AI_ENABLED` ausente → `{ "ai": false }` e a API sobe normalmente.
- [ ] **Step 3: Teste manual pela API** (curl com usuário de teste): `POST /api/ai/generate-npc` com `{"prompt":"anão ferreiro rabugento","withStatBlock":true,"challengeRating":"2"}` e outro sem ficha com campanha de terror gótico.
Expected: 200 em menos de 60 s; textos em português; ND 2; ficha passa no `POST /api/npcs` sem 400; `Used` aumenta. Registrar o tempo de resposta e qualquer aviso recorrente no ledger — avisos frequentes indicam ajuste de prompt (feito aqui, com novo teste no `PromptBuilderTests` se mudar o template).
- [ ] **Step 4: Commit** — `chore: wire native Ollama into compose and add local AI tutorial`

---

### Task 8: Frontend — gerar NPC com IA na tela de criação

**Files:**
- Create: `frontend/src/app/core/features/features.service.ts`
- Create: `frontend/src/app/features/npcs/ai.api.ts`
- Create: `frontend/src/app/features/npcs/ai-prompt.component.ts` (+ html/scss)
- Modify: `frontend/src/app/core/api/api.models.ts` (`NpcDraft`, `GenerateNpcRequest`, `GenerateNpcResponse`, `AiUsageDto`)
- Modify: `frontend/src/app/features/npcs/npc-form.model.ts` (`draftToFormValue`)
- Modify: `frontend/src/app/features/npcs/npc-form.page.ts` (+ html) — o card só aparece na criação
- Modify: `frontend/src/app/core/api/problem-details.ts` (502/504/429 usam o título da API, que já vem em português)
- Test: `frontend/src/app/features/npcs/npc-form.model.spec.ts`, `frontend/src/app/core/api/problem-details.spec.ts`, `frontend/src/app/core/features/features.service.spec.ts`

**Interfaces:**
- Consumes: `POST /api/ai/generate-npc`, `GET /api/ai/usage` (Task 5); `GET /api/features` (Task 6); `loadNpcForm`, `NpcFormValue` (Plano 2).
- Produces:
  - `FeaturesService` (`providedIn: 'root'`): `readonly ai: Signal<boolean>` (começa `false`); carregado uma vez no `provideAppInitializer` com `GET /api/features`; erro → continua `false` (Review Focus 6).
  - O card de IA só é renderizado quando `features.ai()` é `true`; com IA desligada, `/npcs/new` fica exatamente como no Plano 2.
  - `AiApi` (`providedIn: 'root'`): `generateNpc(req: GenerateNpcRequest): Observable<GenerateNpcResponse>`, `usage(): Observable<AiUsageDto>`.
  - `draftToFormValue(draft: NpcDraft, current: NpcFormValue): NpcFormValue` — substitui nome, raça, profissão, aparência, personalidade, histórico e ficha (`includeStatBlock = draft.statBlock !== null`; `null` → `defaultStatBlockValue()`), **preserva** `campaignId` e `notes` do formulário atual.
  - `AiPromptComponent`:
    - inputs: `campaignId: string` (vazio = sem campanha), `withStatBlock: boolean`
    - output: `generated: { draft: NpcDraft; warnings: string[] }`, `withStatBlockChange: boolean`
    - estado interno: `usage` (carregado no início), `generating`, `messages`
    - UI: textarea "Descreva o NPC" (máx. 500, contador de caracteres), toggle "Incluir ficha de combate", select de ND (só com o toggle ligado; opção "Deixar a IA escolher"), botão "✨ Gerar com IA", texto "X de 5 gerações hoje"; durante a geração: spinner + "Gerando… pode levar alguns segundos" + botão "Cancelar" (cancela a assinatura, o que aborta a requisição); limite atingido: botão desabilitado e "Novas gerações às HH:mm" (hora local de `resetsAt`).
  - `NpcFormPage`: o toggle "Incluir ficha" do card fica sincronizado com o do formulário; ao gerar: `loadNpcForm(fb, form, draftToFormValue(draft, form.getRawValue()))`, `form.markAsDirty()`, avisos exibidos num bloco "A IA ajustou alguns valores" acima do formulário (somem ao gerar de novo); a mensagem "Revise e salve: nada foi salvo ainda." aparece após gerar.
  - `errorMessages`: para 429/502/504, usar `problem.title` (já em português) em vez das mensagens genéricas.

- [ ] **Step 1: Escrever os testes que falham**
- `FeaturesService turns ai on when the API says so` e `FeaturesService keeps ai off when the request fails` (Review Focus 6)
- `draftToFormValue preserves campaign and notes` (Review Focus 5)
- `draftToFormValue without stat block turns the toggle off and resets the sheet to defaults`
- `draftToFormValue maps null optional texts to empty strings`
- `errorMessages uses the API title for 429, 502 and 504`
- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar modelos, `AiApi`, `draftToFormValue`, ajuste no `errorMessages`, `AiPromptComponent` e integração no `NpcFormPage`.**
- [ ] **Step 4: Rodar e ver passar** — `npx ng test --watch=false` e `npx ng build` (sem aviso de orçamento).
- [ ] **Step 5: Verificar no navegador com o Ollama real**
Gerar um NPC narrativo numa campanha; gerar um inimigo com ND 3 (ficha preenchida, modificadores corretos); gerar de novo por cima (campanha e anotações preservadas); cancelar uma geração em andamento (contador não diminui); salvar o NPC gerado; esgotar as 5 gerações (botão desabilitado com horário de renovação — pode-se baixar `Ai__DailyGenerationLimit` temporariamente); parar o Ollama e gerar (mensagem "A IA não está disponível agora." e contador intacto); largura de celular; por fim, `AI_ENABLED=false` + `docker compose up -d` → `/npcs/new` sem o card e criação manual funcionando.
Expected: todos os comportamentos acima.
- [ ] **Step 6: Commit** — `feat(web): generate NPCs with AI on the create page`
