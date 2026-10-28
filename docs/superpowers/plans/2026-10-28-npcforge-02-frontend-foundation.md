# NpcForge — Plano 2: Frontend Angular (sem IA)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aplicação Angular que consome a API do Plano 1: login/cadastro, campanhas, lista de NPCs com filtros, formulário de NPC (narrativo ou com ficha) e visualização da ficha no estilo stat block de D&D. Sem IA.

**Architecture:** Projeto Angular standalone em `frontend/`, organizado em `core/` (auth, interceptor, guards, config, modelos da API), `features/` (auth, campaigns, npcs) e `shared/` (componentes e utilitários reutilizáveis). Estado local com signals; chamadas HTTP em services por recurso; formulários com Reactive Forms. A API roda via `docker compose up` em `http://localhost:8080`; o front roda com `ng serve` em `http://localhost:4200` e chama a API direto (o CORS do Plano 1 já libera essa origem).

**Tech Stack:** Angular 22 (CLI 22.2.x, standalone, signals, zoneless/test runner padrão do CLI), Angular Material 22, Reactive Forms, `HttpClient` com interceptors funcionais.

**Spec:** `docs/superpowers/specs/2026-10-28-npcforge-design.md` (seção 7, exceto `/explore` — Plano 5 — e a caixa de prompt/botões ✨ — Planos 3 e 4).

## Global Constraints

- Node 22.22+ (instalado: 22.23.2). CLI via `npx @angular/cli@22` — sem instalação global.
- Projeto em `frontend/`, nome `npc-forge-web`, prefixo de componentes `app`, estilos em SCSS, roteamento habilitado, **sem SSR**.
- Só standalone components; `inject()` em vez de injeção por construtor; `ChangeDetectionStrategy.OnPush` em todo componente.
- Estado de componente com `signal`/`computed`; nada de NgRx.
- URL da API vem de `src/environments/environment*.ts` (`apiBaseUrl: 'http://localhost:8080/api'`), nunca hardcoded em services.
- Token JWT em `localStorage` (chaves `npcforge.token` e `npcforge.expiresAt`) — trade-off aceito na spec.
- Todos os textos da interface em português.
- Rotas protegidas por `authGuard`, exceto `/login` e `/register` (que usam `guestGuard`).
- Testes de frontend (spec, seção 8): `AuthService` e `jwtInterceptor`, mais funções puras de mapeamento/formatação. Componentes são verificados no navegador, não por teste unitário.
- Valores de D&D espelham o backend: NDs `0, 1/8, 1/4, 1/2, 1–30`; tamanhos `Tiny, Small, Medium, Large, Huge, Gargantuan` (rótulos: Miúdo, Pequeno, Médio, Grande, Enorme, Imenso); atributos 1–30; listas da ficha com no máximo 30 itens.
- Commits pequenos, um por task, Conventional Commits.

## Review Focus

1. **Token expirado no `localStorage`:** abrir o app no dia seguinte não pode mostrar telas vazias com erros 401 em sequência; o app trata o token vencido como deslogado e vai para `/login`. Teste na Task 2.
2. **401 no próprio login:** senha errada mostra "E-mail ou senha inválidos." no formulário — o interceptor **não** pode tratar esse 401 como sessão expirada e redirecionar. Teste na Task 2.
3. **Erros de validação do servidor:** um 400 com `errors["StatBlock.Str"]` precisa aparecer no campo `Força` da ficha, e um erro sem campo correspondente (ex.: `DuplicateEmail`) aparece como mensagem geral — nunca é engolido. Teste na Task 1.
4. **Campos opcionais vazios:** apagar o texto de "Raça" deve enviar `null`, não `""`; `attackBonus` vazio vira `null`, não `0`. Teste na Task 5.
5. **Editar um NPC e desligar "Incluir ficha":** a ficha é removida no servidor (`DELETE /statblock`); ligar o toggle num NPC narrativo cria a ficha (`PUT /statblock`). Teste na Task 5.

---

### Task 1: Projeto Angular, configuração e utilitários da API

**Files:**
- Create: `frontend/` (via `npx @angular/cli@22 new npc-forge-web --directory frontend --style=scss --routing --ssr=false --skip-git`)
- Create: `frontend/src/environments/environment.ts`, `environment.development.ts` (`apiBaseUrl`)
- Modify: `frontend/angular.json` (`fileReplacements` para o ambiente de desenvolvimento, se o CLI não criar)
- Modify: `frontend/src/app/app.config.ts` (`provideHttpClient(withInterceptors([]))`, `provideRouter(routes, withComponentInputBinding())`, Material)
- Create: `frontend/src/app/core/api/api.models.ts`
- Create: `frontend/src/app/core/api/problem-details.ts`
- Test: `frontend/src/app/core/api/problem-details.spec.ts`
- Modify: `.gitignore` (garantir `frontend/node_modules/`, `frontend/.angular/`, `frontend/dist/`)

**Interfaces:**
- Produces:
  - `api.models.ts` — interfaces TS espelhando os DTOs do backend: `MeResponse`, `LoginResponse`, `CampaignDto`, `CampaignRequest`, `NpcDto`, `NpcSummaryDto`, `CreateNpcRequest`, `UpdateNpcRequest`, `StatBlockRequest`, `StatBlockDto`, `StatBlockFeatureDto`, `StatBlockActionDto`, `AbilityModifiers`, `PagedResult<T>`, `ProblemDetails { title?: string; status?: number; errors?: Record<string, string[]> }`, `type CreatureSize = 'Tiny' | 'Small' | 'Medium' | 'Large' | 'Huge' | 'Gargantuan'`; constantes `CHALLENGE_RATINGS: readonly string[]`, `CREATURE_SIZES: readonly { value: CreatureSize; label: string }[]`, `ABILITIES: readonly { key: 'str'|'dex'|'con'|'int'|'wis'|'cha'; label: string }[]` (Força, Destreza, Constituição, Inteligência, Sabedoria, Carisma), `STAT_BLOCK_LIST_MAX = 30`.
  - `applyServerErrors(form: AbstractControl, problem: ProblemDetails | null | undefined): string[]` — para cada chave de `errors`, converte o caminho para o controle (`"StatBlock.Str"` → `statBlock.str`, `"$.email"` → `email`, comparação sem diferenciar maiúsculas/minúsculas por segmento), chama `control.setErrors({ ...control.errors, server: mensagens.join(' ') })` e `markAsTouched()`; devolve as mensagens sem controle correspondente, mais `problem.title` se não houver nenhum `errors`.
  - `problemFrom(error: unknown): ProblemDetails | null` — extrai o corpo de um `HttpErrorResponse`.

- [ ] **Step 1: Criar o projeto e instalar o Angular Material**

Na raiz: `npx @angular/cli@22 new npc-forge-web --directory frontend --style=scss --routing --ssr=false --skip-git`; depois, em `frontend/`, `npx ng add @angular/material` (tema M3, tipografia e animações padrão). Remover o conteúdo de exemplo do `app.html`.

- [ ] **Step 2: Verificar build e testes do esqueleto**

Run: `npx ng build` e `npx ng test --watch=false` (em `frontend/`)
Expected: build sem erros; o runner de testes padrão do CLI executa e passa o teste gerado (ajuste o teste gerado se ele procurar o conteúdo de exemplo removido).

- [ ] **Step 3: Escrever os testes que falham de `applyServerErrors`**

```ts
// form: { email, statBlock: { str, hitDice }, traits: FormArray }
it('maps nested PascalCase keys to camelCase controls', ...)
  // errors: { 'StatBlock.Str': ['Deve ser entre 1 e 30.'] } → form.get('statBlock.str').errors.server === 'Deve ser entre 1 e 30.'
it('maps System.Text.Json "$." paths', ...)          // '$.email' → email
it('returns messages without a matching control', ...) // { DuplicateEmail: ['Este e-mail já está cadastrado.'] } → ['Este e-mail já está cadastrado.']
it('returns the title when there are no field errors', ...) // { title: 'E-mail ou senha inválidos.' } → ['E-mail ou senha inválidos.']
it('returns [] for null problem', ...)
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `npx ng test --watch=false`
Expected: FAIL — `applyServerErrors` não existe.

- [ ] **Step 5: Implementar `api.models.ts`, `problem-details.ts`, environments e `app.config.ts`**

- [ ] **Step 6: Rodar e ver passar**

Run: `npx ng test --watch=false` e `npx ng build`
Expected: todos PASS; build sem erros.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: scaffold Angular app with API models and server error mapping"
```

---

### Task 2: Autenticação (service, interceptor, guards, telas)

**Files:**
- Create: `frontend/src/app/core/auth/auth.service.ts`
- Create: `frontend/src/app/core/auth/jwt.interceptor.ts`
- Create: `frontend/src/app/core/auth/auth.guards.ts`
- Create: `frontend/src/app/features/auth/login.page.ts` (+ `.html`, `.scss`)
- Create: `frontend/src/app/features/auth/register.page.ts` (+ `.html`, `.scss`)
- Modify: `frontend/src/app/app.config.ts` (registrar `jwtInterceptor`), `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/core/auth/auth.service.spec.ts`, `jwt.interceptor.spec.ts`

**Interfaces:**
- Consumes: `environment.apiBaseUrl`, `MeResponse`, `LoginResponse`, `applyServerErrors`, `problemFrom` (Task 1).
- Produces:
  - `AuthService` (`providedIn: 'root'`):
    - `readonly user: Signal<MeResponse | null>`
    - `readonly isLoggedIn: Signal<boolean>` — `true` só se houver token **não expirado**
    - `getToken(): string | null` — `null` se ausente ou se `expiresAt <= agora`
    - `login(email: string, password: string): Observable<void>` — `POST /auth/login`, salva token e `expiresAt`, depois carrega `/auth/me` em `user`
    - `register(email: string, password: string, displayName: string): Observable<void>` — `POST /auth/register` e em seguida `login`
    - `loadUser(): Observable<MeResponse>` — usado na inicialização quando já há token
    - `logout(): void` — limpa `localStorage` e `user`, navega para `/login`
  - `jwtInterceptor: HttpInterceptorFn` — só para URLs que começam com `apiBaseUrl`: adiciona `Authorization: Bearer <token>` quando há token; em resposta 401 chama `logout()`, **exceto** para `/auth/login` e `/auth/register`.
  - `authGuard: CanActivateFn` — deslogado → `UrlTree` para `/login`; `guestGuard: CanActivateFn` — logado → `/campaigns`.
  - Inicialização: `provideAppInitializer` que chama `loadUser()` se houver token válido (erro → `logout()` silencioso).
  - Rotas: `''` → redirect `/campaigns`; `/login`, `/register` (guestGuard); o resto sob `authGuard`; `**` → `/campaigns`.

Telas: cartão centralizado com `mat-form-field`s; login (e-mail, senha) com link "Criar conta"; cadastro (nome de exibição, e-mail, senha com dica "mínimo 8 caracteres, com letra minúscula e número"). Erros do servidor via `applyServerErrors` (mensagens gerais acima do botão). Botão desabilitado e com spinner enquanto envia.

- [ ] **Step 1: Escrever os testes que falham do `AuthService`** (com `provideHttpClientTesting` e `HttpTestingController`)

- `login stores token and expiresAt and loads the user` — após `login`, `localStorage['npcforge.token']` é o token, `user()` é o `MeResponse` respondido no `GET /auth/me`, `isLoggedIn()` é `true`
- `getToken returns null when the token is expired` — `expiresAt` no passado → `null` e `isLoggedIn()` `false`
- `register posts then logs in` — espera `POST /auth/register` e depois `POST /auth/login`
- `logout clears storage and user and navigates to /login`

- [ ] **Step 2: Escrever os testes que falham do `jwtInterceptor`**

- `adds the bearer header to API requests when logged in`
- `does not add the header to other origins` — ex.: `https://example.com/x`
- `does not add the header when the token is expired`
- `logs out on 401 from an API call`
- `does not log out on 401 from /auth/login`

- [ ] **Step 3: Rodar e ver falhar**

Run: `npx ng test --watch=false`
Expected: FAIL — `AuthService`/`jwtInterceptor` não existem.

- [ ] **Step 4: Implementar service, interceptor, guards, inicializador e rotas**

- [ ] **Step 5: Rodar e ver passar**

Run: `npx ng test --watch=false`
Expected: todos PASS.

- [ ] **Step 6: Implementar as telas de login e cadastro**

- [ ] **Step 7: Verificar no navegador**

Com `docker compose up -d` e `npx ng serve`: abrir `http://localhost:4200` → redireciona para `/login`; cadastrar uma conta → cai em `/campaigns`; recarregar a página → continua logado; senha errada no login → mensagem no formulário, sem redirecionamento; apagar `npcforge.expiresAt` para uma data passada no DevTools e recarregar → volta para `/login`.
Expected: todos os comportamentos acima.

- [ ] **Step 8: Commit**

```bash
git add .
git commit -m "feat(web): add authentication with JWT interceptor and route guards"
```

---

### Task 3: Layout e campanhas

**Files:**
- Create: `frontend/src/app/core/layout/shell.component.ts` (+ `.html`, `.scss`)
- Create: `frontend/src/app/features/campaigns/campaigns.api.ts`
- Create: `frontend/src/app/features/campaigns/campaign-list.page.ts` (+ html/scss)
- Create: `frontend/src/app/features/campaigns/campaign-detail.page.ts` (+ html/scss)
- Create: `frontend/src/app/features/campaigns/campaign-form.dialog.ts` (+ html)
- Create: `frontend/src/app/shared/confirm.dialog.ts`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `AuthService.user`/`logout` (Task 2), `CampaignDto`, `CampaignRequest`, `NpcSummaryDto`, `PagedResult`, `applyServerErrors` (Task 1).
- Produces:
  - `ShellComponent` — toolbar com "NpcForge", links "Campanhas" e "NPCs", nome do usuário e "Sair"; `<router-outlet>` para as rotas protegidas (as rotas logadas são filhas do shell).
  - `CampaignsApi` (`providedIn: 'root'`): `list(): Observable<CampaignDto[]>`, `get(id: string)`, `create(req: CampaignRequest)`, `update(id: string, req: CampaignRequest)`, `delete(id: string): Observable<void>`.
  - `CampaignFormDialog` — `MatDialog` com dados `{ campaign?: CampaignDto }`; campos Nome (obrigatório, máx. 100), Descrição e Cenário/tom (máx. 2000, com dica "usado como contexto pela IA"); fecha devolvendo o `CampaignDto` salvo.
  - `ConfirmDialog` — dados `{ title: string; message: string; confirmLabel: string }`, fecha com `true`/`false`.
  - Rotas: `/campaigns` → `CampaignListPage`; `/campaigns/:id` → `CampaignDetailPage` (`id` via input binding).

Telas:
- **Lista:** grid responsivo de cards (nome, início da descrição, "N NPCs"); botão "Nova campanha"; estado vazio "Nenhuma campanha ainda" com o mesmo botão.
- **Detalhe:** nome, descrição e cenário; botões "Editar" e "Excluir" (confirmação: "Os NPCs desta campanha não serão excluídos; eles ficarão sem campanha."); lista dos NPCs da campanha (`GET /npcs?campaignId=...&pageSize=100`) com link para cada um e botão "Novo NPC" que abre `/npcs/new?campaignId=...`. Campanha inexistente/404 → mensagem "Campanha não encontrada" com link de volta.

- [ ] **Step 1: Implementar `CampaignsApi`, `ConfirmDialog`, `ShellComponent`, as telas e as rotas**

- [ ] **Step 2: Verificar build e testes**

Run: `npx ng build` e `npx ng test --watch=false`
Expected: build sem erros; testes existentes PASS.

- [ ] **Step 3: Verificar no navegador**

Criar duas campanhas; editar uma (o card reflete a mudança); abrir o detalhe; excluir uma com confirmação (some da lista); acessar `/campaigns/00000000-0000-0000-0000-000000000000` → "Campanha não encontrada"; "Sair" → `/login`. Testar também na largura de celular (375 px): cards em uma coluna, toolbar sem quebrar.
Expected: todos os comportamentos acima.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(web): add app shell and campaign pages"
```

---

### Task 4: Lista de NPCs com filtros e paginação

**Files:**
- Create: `frontend/src/app/features/npcs/npcs.api.ts`
- Create: `frontend/src/app/features/npcs/npc-list.page.ts` (+ html/scss)
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `CampaignsApi.list` (Task 3), modelos (Task 1).
- Produces:
  - `NpcQuery { campaignId?: string | 'none'; search?: string; hasStatBlock?: boolean; page: number; pageSize: number }`
  - `NpcsApi` (`providedIn: 'root'`): `list(query: NpcQuery): Observable<PagedResult<NpcSummaryDto>>` (omite parâmetros vazios), `get(id: string): Observable<NpcDto>`, `create(req: CreateNpcRequest): Observable<NpcDto>`, `update(id: string, req: UpdateNpcRequest): Observable<NpcDto>`, `delete(id: string): Observable<void>`, `setStatBlock(id: string, req: StatBlockRequest): Observable<NpcDto>`, `removeStatBlock(id: string): Observable<NpcDto>`.
  - Rota `/npcs` → `NpcListPage`.

Tela: barra de filtros (busca com debounce de 300 ms; select de campanha com "Todas", "Sem campanha" e as campanhas do usuário; select "Tipo": Todos / Com ficha / Só narrativo); lista em `mat-table` (colunas: Nome, Raça, Profissão, Campanha, Ficha — chip com "ND X" ou "Narrativo") — na largura de celular, cards empilhados em vez de tabela; `mat-paginator` com tamanhos 10/20/50; clicar na linha abre `/npcs/:id`; botão "Novo NPC". Os filtros e a página ficam na query string da URL (`?search=&campaignId=&type=&page=`), para que recarregar ou voltar no navegador preserve o estado. Estado vazio distinto para "nenhum NPC ainda" e "nenhum resultado para os filtros".

- [ ] **Step 1: Implementar `NpcsApi`, a tela e a rota**

- [ ] **Step 2: Verificar build e testes**

Run: `npx ng build` e `npx ng test --watch=false`
Expected: build sem erros; testes PASS.

- [ ] **Step 3: Verificar no navegador**

Criar ~25 NPCs (alguns com ficha, distribuídos em 2 campanhas e sem campanha) com um script no console do navegador que faz `fetch` em `POST /api/npcs` usando o token de `localStorage['npcforge.token']`. Depois: buscar por parte do nome; filtrar por campanha e por "Sem campanha"; filtrar "Com ficha"; trocar de página; recarregar a página com filtros ativos (estado preservado); buscar `%` (não lista tudo).
Expected: todos os comportamentos acima.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(web): add NPC list with filters and pagination"
```

---

### Task 5: Formulário de NPC (criar e editar, com ficha opcional)

**Files:**
- Create: `frontend/src/app/features/npcs/npc-form.model.ts`
- Create: `frontend/src/app/features/npcs/npc-form.page.ts` (+ html/scss)
- Create: `frontend/src/app/features/npcs/stat-block-form.component.ts` (+ html/scss)
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/features/npcs/npc-form.model.spec.ts`

**Interfaces:**
- Consumes: `NpcsApi` (Task 4), `CampaignsApi.list` (Task 3), constantes e `applyServerErrors` (Task 1).
- Produces:
  - `buildNpcForm(fb: NonNullableFormBuilder): NpcForm` — grupo tipado com `campaignId`, `name` (obrigatório, máx. 100), `race`, `occupation` (máx. 100), `appearance`, `personality` (máx. 2000), `background`, `notes` (máx. 4000), `includeStatBlock: boolean`, `statBlock` (grupo: `size`, `creatureType`, `alignment`, `armorClass`, `hitPoints`, `hitDice`, `speed`, `str…cha`, `challengeRating`, `savingThrows`, `skills`, `senses`, `languages`, e `FormArray`s `traits`, `actions`, `reactions`, com os mesmos limites do backend; `hitDice` com `Validators.pattern(/^\d+d\d+([+-]\d+)?$/)`). Quando `includeStatBlock` é `false`, o grupo `statBlock` fica **desabilitado** (não valida nem entra no `value`).
  - `defaultStatBlockValue(): StatBlockFormValue` — `Medium`, humanoide, neutro, CA 10, PV 4, `1d8`, `9 m`, atributos 10, ND `0`, listas vazias.
  - `toCreateRequest(value: NpcFormValue): CreateNpcRequest` e `toUpdateRequest(value): UpdateNpcRequest` — strings vazias/só espaços → `null`; `statBlock` = `null` quando `includeStatBlock` é `false`.
  - `toStatBlockRequest(value: StatBlockFormValue): StatBlockRequest` — `attackBonus` vazio → `null`; `damage` vazio → `null`.
  - `toFormValue(npc: NpcDto): NpcFormValue` — inverso, com `includeStatBlock = npc.statBlock !== null`.
  - `planSave(original: NpcDto | null, value: NpcFormValue): SaveStep[]` — `type SaveStep = { kind: 'create' } | { kind: 'update' } | { kind: 'setStatBlock' } | { kind: 'removeStatBlock' }`; criação → `[create]`; edição → `[update]` + `setStatBlock` se o toggle está ligado, ou `removeStatBlock` se está desligado e o original tinha ficha.
  - `StatBlockFormComponent` — recebe o `FormGroup` da ficha por input; seções "Básico" (tamanho, tipo, alinhamento, CA, PV, dados de vida, deslocamento, ND), "Atributos" (6 campos numéricos com o modificador calculado ao lado), "Detalhes" (resistências, perícias, sentidos, idiomas) e as três listas com "Adicionar" (desabilitado em 30 itens) e "Remover" por item.
  - Rotas: `/npcs/new` (aceita `?campaignId=` para pré-selecionar) e `/npcs/:id/edit`.

Tela: campos narrativos em um card; campanha em select ("Sem campanha" + campanhas); toggle "Incluir ficha de combate" (ao ligar pela primeira vez, preenche com `defaultStatBlockValue()`); `StatBlockFormComponent` abaixo do toggle; "Salvar" executa os passos de `planSave` em sequência e navega para `/npcs/:id`; "Cancelar" volta. Erros do servidor via `applyServerErrors` (as chaves `StatBlock.*` caem nos campos da ficha); mensagens gerais no topo. Ao sair com alterações não salvas, confirmação do navegador (`canDeactivate`).

- [ ] **Step 1: Escrever os testes que falham de `npc-form.model`**

- `toCreateRequest turns blank optional strings into null` — `race: '   '` → `null`
- `toCreateRequest omits the stat block when includeStatBlock is false` — `statBlock: null`
- `toStatBlockRequest maps empty attackBonus and damage to null` — `attackBonus: null` (não `0`)
- `toFormValue round-trips an NPC with stat block` — `toCreateRequest(toFormValue(npc))` preserva nome, ficha e ações
- `planSave on edit adds removeStatBlock when the toggle is turned off` — original com ficha + toggle `false` → `[update, removeStatBlock]`
- `planSave on edit adds setStatBlock when the toggle is on` — original narrativo + toggle `true` → `[update, setStatBlock]`
- `planSave on edit of a narrative NPC with toggle off is just update`
- `stat block group is disabled when includeStatBlock is false` — `form.valid` mesmo com `statBlock.str = 99`

- [ ] **Step 2: Rodar e ver falhar**

Run: `npx ng test --watch=false`
Expected: FAIL — `npc-form.model` não existe.

- [ ] **Step 3: Implementar `npc-form.model.ts`**

- [ ] **Step 4: Rodar e ver passar**

Run: `npx ng test --watch=false`
Expected: todos PASS.

- [ ] **Step 5: Implementar `StatBlockFormComponent`, `NpcFormPage` e as rotas**

- [ ] **Step 6: Verificar no navegador**

Criar um NPC narrativo; criar um com ficha (2 ações, uma com ataque); editar e desligar a ficha (a lista mostra "Narrativo"); editar e religar; abrir o formulário com uma campanha selecionada, excluir essa campanha em outra aba e salvar — o 400 do servidor aparece no campo Campanha ("Campanha não encontrada."); abrir `/npcs/new?campaignId=<id>` (campanha pré-selecionada); tentar sair com alterações (confirmação).
Expected: todos os comportamentos acima.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(web): add NPC create/edit form with optional stat block"
```

---

### Task 6: Visualização do NPC e stat block

**Files:**
- Create: `frontend/src/app/shared/dnd-format.ts`
- Create: `frontend/src/app/features/npcs/npc-view.page.ts` (+ html/scss)
- Create: `frontend/src/app/features/npcs/stat-block-view.component.ts` (+ html/scss)
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/shared/dnd-format.spec.ts`

**Interfaces:**
- Consumes: `NpcsApi.get/delete` (Task 4), `StatBlockDto`, `CREATURE_SIZES` (Task 1).
- Produces:
  - `formatModifier(mod: number): string` — `+3`, `-1`, `+0` (sinal de menos ASCII)
  - `formatAbility(score: number, mod: number): string` — `"16 (+3)"`
  - `formatAttackBonus(bonus: number | null): string | null` — `"+5 para acertar"` ou `null`
  - `sizeLabel(size: CreatureSize): string` — rótulo em português
  - `StatBlockViewComponent` — input `statBlock: StatBlockDto` e `name: string`; layout clássico de stat block 5e: nome em versalete vermelho-escuro, linha "tamanho tipo, alinhamento", régua afunilada, CA/PV (com dados)/Deslocamento, tabela de atributos com modificadores, linhas de detalhes só quando preenchidas, ND com bônus de proficiência, seções "Características", "Ações" e "Reações" só quando não vazias. Fundo pergaminho (`#fdf1dc`), títulos e réguas em `#922610`, fonte serifada do Google Fonts (ex.: "Libre Baskerville") só neste componente. Respeita a largura do celular (tabela de atributos em 3 colunas abaixo de 480 px).
  - Rota `/npcs/:id` → `NpcViewPage`: cabeçalho com nome, raça · profissão e link para a campanha (ou "Sem campanha"); seções Aparência, Personalidade, Histórico e "Anotações do mestre" (destacada como privada) só quando preenchidas; `StatBlockViewComponent` se houver ficha, ou um aviso "NPC narrativo — sem ficha de combate" com o botão "Adicionar ficha" (leva à edição com o toggle ligado via `?withStatBlock=1`); botões "Editar" e "Excluir" (confirmação; depois volta para `/npcs`). 404 → "NPC não encontrado".

- [ ] **Step 1: Escrever os testes que falham de `dnd-format`**

- `formatModifier`: `3 → "+3"`, `-1 → "-1"`, `0 → "+0"`
- `formatAbility(16, 3) → "16 (+3)"`, `formatAbility(8, -1) → "8 (-1)"`
- `formatAttackBonus(5) → "+5 para acertar"`, `formatAttackBonus(null) → null`
- `sizeLabel('Gargantuan') → "Imenso"`

- [ ] **Step 2: Rodar e ver falhar**

Run: `npx ng test --watch=false`
Expected: FAIL — `dnd-format` não existe.

- [ ] **Step 3: Implementar `dnd-format.ts`**

- [ ] **Step 4: Rodar e ver passar**

Run: `npx ng test --watch=false`
Expected: todos PASS.

- [ ] **Step 5: Implementar `StatBlockViewComponent`, `NpcViewPage` e a rota; no `NpcFormPage`, ler `?withStatBlock=1` para ligar o toggle**

- [ ] **Step 6: Verificar no navegador**

Ver um NPC narrativo (aviso + "Adicionar ficha" leva ao formulário com a ficha ligada); ver um NPC com ficha completa (comparar visualmente com um stat block oficial: ordem das seções, modificadores, ND); seções vazias não aparecem; excluir com confirmação; largura de celular (375 px) sem rolagem horizontal.
Expected: todos os comportamentos acima.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(web): add NPC view page with D&D-style stat block"
```

---

### Task 7: README e verificação de ponta a ponta

**Files:**
- Modify: `README.md` (seção "Como rodar" com o frontend: `cd frontend && npm install && npx ng serve`; stack com Angular; roadmap com a fase 2 riscada; testes do frontend com `npx ng test --watch=false`)

- [ ] **Step 1: Atualizar o README**

- [ ] **Step 2: Rodar todas as suítes**

Run: `dotnet test backend/NpcForge.slnx` e, em `frontend/`, `npx ng test --watch=false` e `npx ng build`
Expected: backend 102/102 PASS; testes do frontend PASS; build sem erros nem avisos de orçamento.

- [ ] **Step 3: Percorrer o fluxo completo no navegador**

Com banco limpo (`docker compose down -v && docker compose up -d`): cadastrar → criar campanha "Maldição de Strahd" → criar pela campanha um NPC narrativo e um inimigo com ficha → filtrar a lista de NPCs → abrir o inimigo → editar e remover a ficha → excluir a campanha (o NPC continua em "Sem campanha") → sair → entrar de novo.
Expected: todo o fluxo funciona sem erros no console do navegador.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "docs: document how to run the Angular frontend"
```
