# NpcForge — Plano 4: Exportar e importar NPCs

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Exportar NPCs (lista filtrada ou um NPC) para um arquivo JSON e importá-lo em outra instalação, em tudo ou nada, com os NPCs entrando sem campanha.

**Architecture:** O formato do arquivo vive no Core (`NpcExportFile`/`ExportedNpc`, validados com os mesmos atributos do cadastro). Um `NpcTransferService` na Infrastructure exporta (reaproveitando os filtros da lista, extraídos para uma função compartilhada) e importa (um único `SaveChanges`, transacional). No front, o `NpcsApi` baixa e envia arquivos, funções puras leem o arquivo e formatam os erros, e um `ImportNpcsDialog` faz a prévia e mostra o resultado.

**Tech Stack:** .NET 10, EF Core + Npgsql, xUnit + Testcontainers, Angular 22 + Material + Vitest.

**Spec:** `docs/superpowers/specs/2026-10-29-npc-export-import-design.md` (complementa `docs/superpowers/specs/2026-10-28-npcforge-design.md`).

## Global Constraints

- Formato: `format = "npcforge-npcs"`, `version = 1` (constante `NpcExportFile.CurrentVersion`), `exportedAt` em UTC, `npcs` = lista de `ExportedNpc`.
- O arquivo **nunca** contém ids, campanha, datas de NPC nem dados de conta. Inclui as anotações do mestre.
- Importação: tudo ou nada, todos os NPCs com `CampaignId = null`, ids e datas novos, importar de novo cria cópias.
- Limites: no máximo **500 NPCs** por arquivo e **5 MB** por requisição. Decisão deste plano: acima de 5 MB o próprio servidor recusa com **413** (`[RequestSizeLimit]`), não 400 como a spec dizia — o efeito para o usuário é o mesmo ("arquivo grande demais"); o `TestServer` não aplica esse limite, então ele é conferido manualmente.
- Mensagens (em português, exatas):
  - formato errado: "Este arquivo não é uma exportação de NPCs do NpcForge."
  - versão não suportada: "Este arquivo foi exportado por uma versão mais nova do NpcForge."
  - lista vazia/ausente: "O arquivo não tem NPCs."
  - mais de 500: "O arquivo tem mais de 500 NPCs."
- Nome do arquivo baixado: `npcs-AAAA-MM-DD.json` (lista) e `npc-<slug>.json` (um NPC; slug = nome sem acentos, minúsculo, não alfanuméricos viram `-`, máx. 50 caracteres; slug vazio → `npc.json`).
- O CORS passa a **expor** o cabeçalho `Content-Disposition` (sem isso o Angular, em outra origem, não lê o nome do arquivo).
- Frontend: standalone, signals, OnPush, textos em português, erros via `errorMessages`.

## Review Focus

1. **Arquivo que não é do NpcForge** (um JSON qualquer, ou um arquivo de texto renomeado para `.json`): o diálogo avisa antes de chamar a API, e a API, se chamada, responde 400 com a mensagem de formato — nunca 500. Testes nas Tasks 2 e 3.
2. **Nome de NPC com acentos e símbolos no nome do arquivo** ("Ireena Kolyana!", "Ñandú"): o download sai `npc-ireena-kolyana.json`, `npc-nandu.json`, sem caracteres que quebrem o cabeçalho. Teste na Task 1.
3. **Exportar com filtros inválidos** (`campaignId=abc`): arquivo com lista vazia, como a tela mostraria — não erro. Teste na Task 1.
4. **Erro em NPC sem nome ou com índice fora do arquivo** ao formatar mensagens: `describeImportErrors` usa "NPC 3" quando não há nome e nunca quebra. Teste na Task 3.
5. **Importar estando na lista já filtrada em "Sem campanha"**: a lista recarrega e mostra os recém-importados (a URL não muda, então precisa de recarga explícita). Verificação no navegador na Task 4.

---

### Task 1: Formato do arquivo e exportação (backend)

**Files:**
- Create: `backend/src/NpcForge.Core/Npcs/NpcExportFile.cs`
- Create: `backend/src/NpcForge.Core/Npcs/INpcTransferService.cs`
- Create: `backend/src/NpcForge.Core/Npcs/FileNames.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/NpcFiltering.cs`
- Create: `backend/src/NpcForge.Infrastructure/Services/NpcTransferService.cs`
- Modify: `backend/src/NpcForge.Infrastructure/Services/NpcService.cs` (`ListAsync` passa a usar `NpcFiltering.Apply`)
- Modify: `backend/src/NpcForge.Infrastructure/Services/NpcMapping.cs` (`ToExported(Npc)`)
- Modify: `backend/src/NpcForge.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/NpcForge.Api/Controllers/NpcsController.cs` (ações de exportação)
- Modify: `backend/src/NpcForge.Api/Program.cs` (CORS `.WithExposedHeaders("Content-Disposition")`)
- Test: `backend/tests/NpcForge.UnitTests/Npcs/FileNamesTests.cs`, `backend/tests/NpcForge.IntegrationTests/Npcs/NpcExportTests.cs`

**Interfaces:**
- Produces:
  - `record ExportedNpc([Required, MaxLength(100)] string Name, string? Race, string? Occupation, string? Appearance, string? Personality, string? Background, string? Notes, StatBlockRequest? StatBlock)` — mesmos `[MaxLength]` de `CreateNpcRequest` em cada campo.
  - `class NpcExportFile { const string FormatName = "npcforge-npcs"; const int CurrentVersion = 1; string Format; int Version; DateTime ExportedAt; List<ExportedNpc>? Npcs }` (atributos de validação entram na Task 2).
  - `static class NpcFiltering { IQueryable<Npc> Apply(IQueryable<Npc> npcs, NpcQuery query) }` — mesma semântica de hoje (`none`, GUID, `hasStatBlock`, busca com ILIKE escapado); `campaignId` não-GUID → `npcs.Where(_ => false)`.
  - `static class FileNames { string Slug(string name); string ForList(DateOnly day); string ForNpc(string npcName) }` → `npcs-2026-10-29.json`, `npc-rahadin.json`.
  - `interface INpcTransferService`:
    - `Task<NpcExportFile> ExportAsync(string userId, NpcQuery filters, CancellationToken ct)` — sem paginação, ordem `UpdatedAt` desc
    - `Task<NpcExportFile?> ExportOneAsync(string userId, Guid id, CancellationToken ct)` — `null` se não for do usuário
  - `NpcMapping.ToExported(Npc) → ExportedNpc` (ficha como `StatBlockRequest`, sem valores derivados).
  - Rotas: `GET /api/npcs/export` (query = `NpcQuery`; `page`/`pageSize` ignorados) e `GET /api/npcs/{id}/export`; ambas definem `Content-Disposition: attachment; filename="…"` e devolvem o `NpcExportFile` serializado com as opções JSON da API (camelCase, enums como texto).

- [ ] **Step 1: Escrever os testes que falham**
- `FileNamesTests`:
  - `Slug("Rahadin") == "rahadin"`; `Slug("Ireena Kolyana!") == "ireena-kolyana"`; `Slug("Ñandú") == "nandu"`; `Slug("!!!") == ""`
  - `Slug(new string('a', 60) + " b")` tem 50 caracteres e não termina com `-`
  - `ForNpc("Rahadin") == "npc-rahadin.json"`; `ForNpc("!!!") == "npc.json"` (Review Focus 2)
  - `ForList(new DateOnly(2026, 10, 29)) == "npcs-2026-10-29.json"`
- `NpcExportTests` (`[Collection("api")]`, usuários novos):
  - `Export_WithFilters_ReturnsOnlyMatchingNpcs_AllPages` — 25 NPCs, 3 com ficha → `?hasStatBlock=true` traz 3; sem filtro traz 25 (mais que o `pageSize` padrão 20)
  - `Export_FileHasFormatVersionAndNoIdsOrCampaign` — JSON tem `format = "npcforge-npcs"`, `version = 1`, `exportedAt`; o NPC não tem as propriedades `id`, `campaignId`, `createdAt`, `updatedAt`; tem `notes` e `statBlock.actions`
  - `Export_SetsAttachmentFileName` — `Content-Disposition` contém `npcs-` e `.json`; o de um NPC chamado "Ireena Kolyana!" contém `npc-ireena-kolyana.json`
  - `Export_InvalidCampaignFilter_ReturnsEmptyList` (Review Focus 3)
  - `ExportOne_OtherUsersNpc_Returns404`
  - `Export_ExcludesOtherUsersNpcs`
  - `Cors_ExposesContentDisposition` — preflight/GET com `Origin: http://localhost:4200` traz `Access-Control-Expose-Headers` contendo `Content-Disposition`
- Os testes existentes de listagem (`NpcsListTests`) continuam passando após a extração do filtro.

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar** — `Slug` remove acentos com `string.Normalize(NormalizationForm.FormD)` + descarta `UnicodeCategory.NonSpacingMark`.
- [ ] **Step 4: Rodar a suíte inteira** — `dotnet test backend/NpcForge.slnx` → todos PASS.
- [ ] **Step 5: Commit** — `feat: export NPCs to a versioned JSON file`

---

### Task 2: Importação (backend)

**Files:**
- Modify: `backend/src/NpcForge.Core/Npcs/NpcExportFile.cs` (atributos de validação)
- Modify: `backend/src/NpcForge.Core/Npcs/INpcTransferService.cs`
- Modify: `backend/src/NpcForge.Infrastructure/Services/NpcTransferService.cs`
- Modify: `backend/src/NpcForge.Api/Controllers/NpcsController.cs`
- Test: `backend/tests/NpcForge.IntegrationTests/Npcs/NpcImportTests.cs`

**Interfaces:**
- Consumes: `NpcExportFile`, `ExportedNpc`, `ExportAsync` (Task 1); `StatBlockRequest.ToEntity` (normaliza dados de vida).
- Produces:
  - Validação em `NpcExportFile` (o MVC valida tudo, inclusive cada NPC, antes da ação; erros com chave `npcs[2].statBlock.str`):
    - `Format`: `[Required, AllowedValues(FormatName, ErrorMessage = "Este arquivo não é uma exportação de NPCs do NpcForge.")]`
    - `Version`: `[Range(1, CurrentVersion, ErrorMessage = "Este arquivo foi exportado por uma versão mais nova do NpcForge.")]`
    - `Npcs`: `[Required(ErrorMessage = "O arquivo não tem NPCs."), MinLength(1, ErrorMessage = "O arquivo não tem NPCs."), MaxLength(500, ErrorMessage = "O arquivo tem mais de 500 NPCs."), NoNullElements]`
  - `Task<int> ImportAsync(string userId, NpcExportFile file, CancellationToken ct)` — cria todos os NPCs (`CampaignId = null`, ids/datas novos) e grava com **um** `SaveChangesAsync`; devolve a quantidade.
  - Rota: `POST /api/npcs/import` com `[RequestSizeLimit(5 * 1024 * 1024)]` → 201 `{ "imported": N }`.

- [ ] **Step 1: Escrever os testes que falham** (`NpcImportTests`, `[Collection("api")]`):
- `RoundTrip_ExportThenImportWithAnotherUser_CopiesEverythingWithoutCampaign` — usuário A cria campanha + 2 NPCs (um com ficha, ações e anotações), exporta; usuário B importa → 201 `imported = 2`; B lista 2 NPCs com `campaignId = null`, ids diferentes dos de A, e o `GET` de cada um bate com o original em nome, textos, anotações, ficha e ações
- `Import_SameFileTwice_CreatesCopies` — 2 importações → 4 NPCs
- `Import_OneInvalidNpc_ImportsNothing` — 3 NPCs, o 2º com `str = 31` → 400, `errors` contém a chave `npcs[1].statBlock.str` (comparação sem diferenciar maiúsculas), e B continua com 0 NPCs
- `Import_WrongFormat_Returns400WithMessage` — `format = "outra-coisa"` → mensagem exata (Review Focus 1)
- `Import_NotAnExportAtAll_Returns400` — corpo `{ "foo": 1 }` → 400 (nunca 500) (Review Focus 1)
- `Import_NewerVersion_Returns400WithMessage` — `version = 2`
- `Import_EmptyList_Returns400WithMessage` e `Import_MoreThan500_Returns400WithMessage`
- `Import_UnknownFieldsAreIgnored` — NPC com propriedade extra `"mood": "feliz"` e arquivo com `"generator": "x"` → 201
- `Import_NormalizesHitDice` — `"4d8+4"` vira `"4d8 + 4"`
- `Import_WithoutToken_Returns401`

- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar validação, serviço e rota.**
- [ ] **Step 4: Rodar a suíte inteira** → todos PASS.
- [ ] **Step 5: Commit** — `feat: import NPCs from an export file, all or nothing`

---

### Task 3: Frontend — API, leitura do arquivo, mensagens e botões de exportar

**Files:**
- Modify: `frontend/src/app/core/api/api.models.ts` (`ExportedNpc`, `NpcExportFile`, `ImportResult`)
- Modify: `frontend/src/app/features/npcs/npcs.api.ts`
- Create: `frontend/src/app/shared/download.ts`
- Create: `frontend/src/app/features/npcs/npc-transfer.ts`
- Modify: `frontend/src/app/features/npcs/npc-list.page.ts` (+ html) — botão "Exportar"
- Modify: `frontend/src/app/features/npcs/npc-view.page.ts` (+ html) — botão "Exportar"
- Test: `frontend/src/app/shared/download.spec.ts`, `frontend/src/app/features/npcs/npc-transfer.spec.ts`

**Interfaces:**
- Consumes: rotas das Tasks 1 e 2.
- Produces:
  - `NpcsApi.exportList(filters: Omit<NpcQuery, 'page' | 'pageSize'>): Observable<HttpResponse<Blob>>` (`responseType: 'blob'`, `observe: 'response'`); `exportOne(id: string)` idem; `importFile(file: unknown): Observable<{ imported: number }>`.
  - `download.ts`: `fileNameFrom(contentDisposition: string | null, fallback: string): string`; `saveResponse(response: HttpResponse<Blob>, fallback: string): void` (cria um link temporário com `URL.createObjectURL` e revoga depois).
  - `npc-transfer.ts`:
    - `type ReadResult = { ok: true; file: NpcExportFile; preview: { name: string; hasStatBlock: boolean }[] } | { ok: false; error: string }`
    - `readExportFile(text: string): ReadResult` — JSON inválido → "O arquivo não é um JSON válido."; `format` diferente → mesma mensagem da API; `version > 1` → mesma mensagem da API; `npcs` vazio/ausente → "O arquivo não tem NPCs."
    - `describeImportErrors(problem: ProblemDetails, file: NpcExportFile): string[]` — chave `npcs[i].campo` → `"NPC {i+1} ({nome}): {mensagem}"`; sem nome → `"NPC {i+1}: …"`; chave sem índice → só a mensagem; índice fora da lista → `"NPC {i+1}: …"`.
  - Botões: lista → "Exportar" (desabilitado com `totalCount = 0`; `title` "Exportar N NPCs"), usa os filtros atuais da URL; visualização → "Exportar" ao lado de "Editar". Falha no download → snackbar "Não foi possível exportar. Tente novamente."

- [ ] **Step 1: Escrever os testes que falham**
- `download.spec`: `fileNameFrom('attachment; filename="npc-rahadin.json"', 'x.json') === 'npc-rahadin.json'`; também sem aspas; `filename*=UTF-8''npcs-2026-10-29.json` decodificado; `null` → fallback.
- `npc-transfer.spec`:
  - `readExportFile` com JSON quebrado, com `{ "foo": 1 }`, com `version: 2`, com `npcs: []` → `ok: false` e a mensagem certa (Review Focus 1)
  - arquivo válido com 2 NPCs (um com ficha) → `preview` `[{ name, hasStatBlock: true }, { name, hasStatBlock: false }]`
  - `describeImportErrors` para `npcs[1].statBlock.str` com o 2º NPC "Rahadin" → `"NPC 2 (Rahadin): <mensagem>"`; NPC sem nome → `"NPC 2: …"`; índice 9 num arquivo de 3 → `"NPC 10: …"`; chave `format` → só a mensagem (Review Focus 4)
- [ ] **Step 2: Rodar e ver falhar.**
- [ ] **Step 3: Implementar modelos, `NpcsApi`, `download.ts`, `npc-transfer.ts` e os dois botões.**
- [ ] **Step 4: Rodar** `npx ng test --watch=false` e `npx ng build` → PASS, sem avisos.
- [ ] **Step 5: Verificar no navegador** — exportar a lista filtrada (arquivo com só os filtrados, nome `npcs-AAAA-MM-DD.json`) e um NPC com acento no nome.
- [ ] **Step 6: Commit** — `feat(web): export NPCs as JSON files`

---

### Task 4: Frontend — diálogo de importação, README e verificação ponta a ponta

**Files:**
- Create: `frontend/src/app/features/npcs/import-npcs.dialog.ts` (+ html/scss)
- Modify: `frontend/src/app/features/npcs/npc-list.page.ts` (+ html) — botão "Importar" e recarga
- Modify: `README.md` — seção "Levando seus NPCs para a versão web"

**Interfaces:**
- Consumes: `readExportFile`, `describeImportErrors`, `NpcsApi.importFile` (Task 3).
- Produces:
  - `ImportNpcsDialog` (`MatDialog`, fecha com `number` = quantidade importada, ou nada): input de arquivo (`accept=".json,application/json"`) → `FileReader`/`file.text()` → `readExportFile` → prévia (contagem, nomes, selo "com ficha"/"narrativo") ou erro; aviso fixo "Os NPCs entram sem campanha. Importar o mesmo arquivo de novo cria cópias."; botão "Importar N NPCs" com spinner; 400 → `describeImportErrors` numa lista `role="alert"` e o diálogo continua aberto; outros erros → `errorMessages`; `disableClose` enquanto envia.
  - `NpcListPage`: botão "Importar" no cabeçalho; ao fechar com `n`, snackbar "N NPCs importados" e navega para `?campaignId=none`; um signal `reload` entra junto com os filtros no gatilho da carga, para recarregar mesmo quando a URL já é `?campaignId=none` (Review Focus 5).

- [ ] **Step 1: Implementar o diálogo e a integração na lista.**
- [ ] **Step 2: Rodar** `npx ng test --watch=false` e `npx ng build` → PASS.
- [ ] **Step 3: README** — exportar na versão local (lista filtrada ou página do NPC), importar na web (botão "Importar" em NPCs), NPCs entram sem campanha, importar de novo cria cópias, arquivo com erro não importa nada.
- [ ] **Step 4: Verificar no navegador (ponta a ponta)** — inclui um arquivo acima de 5 MB (gerado à mão) para conferir que a API recusa com 413 e o diálogo mostra uma mensagem em português.
Com dois usuários: o usuário A exporta 2 NPCs (um gerado pela IA, com ficha) → o usuário B importa o arquivo → prévia correta → "2 NPCs importados" → lista em "Sem campanha" mostra os 2 → ficha igual à de A → atribuir uma campanha pela edição. Importar de novo estando já em "Sem campanha" → a lista recarrega com 4. Editar o arquivo à mão para `str: 31` no 2º NPC → erro "NPC 2 (…): …" e nada importado. Escolher um `.txt` renomeado → aviso no diálogo, sem chamada à API. Largura de celular.
Expected: todos os comportamentos acima.
- [ ] **Step 5: Commit** — `feat(web): import NPCs from a JSON file`
