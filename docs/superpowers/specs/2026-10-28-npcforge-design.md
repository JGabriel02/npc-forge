# NpcForge — Design (v1)

**Data:** 2026-10-28
**Status:** aguardando revisão

## 1. Objetivo e contexto

NpcForge é uma aplicação web para mestres de RPG criarem e organizarem NPCs, com uma IA integrada que gera e sugere conteúdo.

O projeto tem dois objetivos de igual peso:

- **Portfólio:** código limpo, arquitetura explicável em entrevista, README caprichado.
- **Aprendizado:** o autor conhece o básico de C# e nunca usou Angular. A arquitetura deve ensinar boas práticas sem cerimônia excessiva.

**Restrições:**

- Custo zero: apenas ferramentas e provedores de IA gratuitos.
- A v1 roda localmente, mas já deve estar pronta para deploy (Docker, configuração por variáveis de ambiente).

**Critérios de sucesso da v1:**

1. Um usuário cria conta, faz login e gerencia campanhas e NPCs próprios.
2. A partir de um prompt curto, a IA gera um NPC completo (com ou sem ficha D&D 5e), que o usuário revisa, edita e salva.
3. No formulário de edição, cada campo relevante pode ser regerado individualmente pela IA (botão ✨).
4. Respostas inválidas da IA nunca são salvas; valores fora das regras são corrigidos e sinalizados ao usuário.
5. Cada usuário pode fazer até 5 gerações completas de NPC por dia; o app mostra quantas restam.
6. Um usuário pode tornar um NPC público; outros usuários encontram esse NPC numa galeria e adicionam uma cópia à própria campanha.
7. `docker compose up` + `ng serve` sobem o ambiente completo; os testes passam sem precisar de IA real.

## 2. Escopo

**Dentro da v1:**

- Autenticação (registro, login, JWT).
- CRUD de campanhas.
- CRUD de NPCs em dois modos: **narrativo** (sem ficha) e **com ficha D&D 5e** (para inimigos). A ficha pode ser adicionada ou removida depois.
- Geração completa de NPC por IA a partir de prompt.
- Sugestão por campo (✨) no formulário.
- Limite diário de 5 gerações completas de NPC por usuário.
- NPCs públicos: galeria de NPCs públicos e cópia de um NPC público para a própria conta/campanha.
- Provedores de IA: Ollama (local, padrão) e um provedor gratuito em nuvem, atrás da mesma interface.

**Fora da v1 (possíveis versões futuras):**

- Chat com o NPC / refinamento conversacional (com streaming via SignalR ou SSE).
- Imagens/retratos de NPCs.
- Outros sistemas de RPG além de D&D 5e.
- Compartilhamento de campanhas entre usuários (apenas NPCs individuais podem ser públicos).
- Moderação de NPCs públicos (denúncia, remoção por administrador), curtidas e comentários.
- Planos pagos ou limites diferentes por usuário.
- Cache de respostas da IA.
- Deploy público (a arquitetura já prevê, mas não é entrega da v1).

## 3. Arquitetura

API em camadas simples (monólito), escolhida em vez de Clean Architecture + CQRS (cerimônia demais para o nível atual) e de projeto único com Minimal APIs (organização fraca para portfólio).

```
NpcForge.Api            → controllers, autenticação, rate limiting, configuração, DI
NpcForge.Core           → entidades, regras de D&D, interfaces (IAiProvider, serviços), DTOs
NpcForge.Infrastructure → EF Core + PostgreSQL, OllamaProvider, provedor em nuvem
```

Fluxo de dependências: `Api → Core ← Infrastructure` (a Infrastructure implementa as interfaces definidas no Core).

**Stack:**

| Camada | Tecnologia |
|---|---|
| Backend | .NET (versão LTS mais recente), ASP.NET Core com controllers |
| Persistência | EF Core + PostgreSQL (Docker) |
| Autenticação | ASP.NET Core Identity + JWT |
| IA | Ollama (local) + um provedor com plano gratuito (ex.: Groq ou Google Gemini, a confirmar no momento da implementação, já que as condições desses planos mudam) |
| Frontend | Angular atual, standalone components, signals, Reactive Forms, Angular Material |
| Testes | xUnit, WebApplicationFactory, Testcontainers (Postgres) |
| Infra | Docker Compose |

## 4. Modelo de dados

```
User (Identity)
 └── 1:N Campaign
           └── 1:N Npc          (CampaignId opcional)
                     └── 1:0..1 StatBlock
```

### Campaign

| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | |
| UserId | string | dono |
| Name | string | obrigatório, máx. 100 |
| Description | string? | |
| Setting | string? | contexto enviado à IA (tom, cenário) |
| CreatedAt, UpdatedAt | DateTime | UTC |

### Npc

| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | |
| UserId | string | dono (permite NPCs sem campanha) |
| CampaignId | Guid? | `null` = "Sem campanha" |
| Name | string | obrigatório, máx. 100 |
| Race, Occupation | string? | |
| Appearance, Personality, Background, Notes | string? | `Notes` = anotações do mestre |
| StatBlock | StatBlock? | `null` = NPC narrativo |
| IsPublic | bool | padrão `false` |
| PublishedAt | DateTime? | quando ficou público pela última vez; ordena a galeria |
| SourceNpcId | Guid? | NPC público de onde esta cópia veio; sem FK (o original pode ser apagado) |
| SourceAuthorName | string? | nome de exibição do autor original, gravado no momento da cópia (crédito) |
| CreatedAt, UpdatedAt | DateTime | UTC |

### StatBlock (D&D 5e)

| Campo | Tipo | Observação |
|---|---|---|
| Size | enum | Tiny, Small, Medium, Large, Huge, Gargantuan |
| CreatureType | string | ex.: "humanoid (dwarf)" |
| Alignment | string | |
| ArmorClass | int | 1–30 |
| HitPoints | int | > 0 |
| HitDice | string | formato `NdM(+/-K)`, ex.: "4d8+4" |
| Speed | string | ex.: "30 ft." |
| Str, Dex, Con, Int, Wis, Cha | int | 1–30 |
| ChallengeRating | string | um de: 0, 1/8, 1/4, 1/2, 1–30 |
| SavingThrows, Skills, Senses, Languages | string? | texto livre |
| Traits | jsonb | lista de `{ name, description }` |
| Actions | jsonb | lista de `{ name, description, attackBonus?, damage? }` |
| Reactions | jsonb | lista de `{ name, description }` |

### AiUsage

| Campo | Tipo | Observação |
|---|---|---|
| UserId | string | |
| Date | DateOnly | dia em UTC |
| GenerationCount | int | gerações completas consumidas no dia |

Chave primária composta `(UserId, Date)`.

**Regras:**

- StatBlock é armazenado em tabela própria com relação 1:0..1 com Npc e exclusão em cascata.
- **Valores derivados não são persistidos:** modificador de atributo = `floor((score - 10) / 2)`; bônus de proficiência derivado do ND (ND 0–4 → +2, 5–8 → +3, 9–12 → +4, 13–16 → +5, 17–20 → +6, 21–24 → +7, 25–28 → +8, 29–30 → +9).
- Todas as consultas são filtradas pelo `UserId` do usuário autenticado. Acesso a recurso de outro usuário retorna **404** (não revela a existência do recurso).
- Excluir uma campanha **não** exclui seus NPCs: o `CampaignId` deles passa a `null`.

### Regras de NPCs públicos

- Somente o dono edita, exclui ou muda a visibilidade de um NPC. Tornar público ou privado é feito pelo campo `isPublic` no `PUT /npcs/{id}`.
- Qualquer usuário autenticado vê NPCs públicos de outros usuários, somente leitura, pelos endpoints `/public/npcs`. O nome de exibição do autor aparece junto.
- **`Notes` (anotações do mestre) nunca é exposto** na visão pública nem copiado: costuma conter segredos da mesa.
- **Copiar** cria um NPC novo e independente do usuário que copiou: copia os campos narrativos (exceto `Notes`) e a ficha, se houver; define `CampaignId` (opcional; campanha de outro usuário → 404), `IsPublic = false`, `SourceNpcId` e `SourceAuthorName`.
- Mudanças posteriores no original (edição, exclusão ou volta a privado) **não afetam** as cópias.
- Um NPC privado de outro usuário retorna 404 em qualquer endpoint, inclusive em `/public/npcs/{id}` e na cópia.
- Uma cópia pode ser tornada pública por quem a copiou; o crédito ao autor original (`SourceAuthorName`) continua aparecendo.

## 5. API

Base `/api`. Todas as rotas, exceto `/auth/register` e `/auth/login`, exigem JWT.

### Autenticação

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/register` | `{ email, password, displayName }` → 201 |
| POST | `/auth/login` | `{ email, password }` → `{ token, expiresAt }` |
| GET | `/auth/me` | dados do usuário logado |

### Campanhas

| Método | Rota | Descrição |
|---|---|---|
| GET | `/campaigns` | lista do usuário, com contagem de NPCs |
| POST | `/campaigns` | cria |
| GET | `/campaigns/{id}` | detalhe |
| PUT | `/campaigns/{id}` | edita |
| DELETE | `/campaigns/{id}` | exclui (NPCs vão para "Sem campanha") |

### NPCs

| Método | Rota | Descrição |
|---|---|---|
| GET | `/npcs?campaignId=&search=&hasStatBlock=&page=&pageSize=` | lista paginada; `search` busca em Name, Race e Occupation; `campaignId=none` filtra os sem campanha |
| POST | `/npcs` | cria (com ou sem `statBlock`) |
| GET | `/npcs/{id}` | detalhe, com valores derivados calculados |
| PUT | `/npcs/{id}` | edita os campos narrativos, o `campaignId` e o `isPublic` |
| DELETE | `/npcs/{id}` | exclui |
| PUT | `/npcs/{id}/statblock` | adiciona ou substitui a ficha |
| DELETE | `/npcs/{id}/statblock` | remove a ficha |

### NPCs públicos

| Método | Rota | Descrição |
|---|---|---|
| GET | `/public/npcs?search=&hasStatBlock=&page=&pageSize=` | galeria paginada de NPCs públicos de **outros** usuários, mais recentes primeiro (`PublishedAt`) |
| GET | `/public/npcs/{id}` | detalhe somente leitura, sem `Notes`, com o nome do autor |
| POST | `/public/npcs/{id}/copy` | `{ campaignId? }` → 201 com o NPC novo do usuário |

### IA (não persistem NPCs)

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| POST | `/ai/generate-npc` | `{ prompt, campaignId?, withStatBlock, challengeRating? }` | `{ draft: NpcDraft, warnings: string[], usage }` |
| POST | `/ai/suggest-field` | `{ field, npcDraft, campaignId?, instruction? }` | `{ field, value, warnings: string[] }` |
| GET | `/ai/usage` | — | `{ used, limit, resetsAt }` |
| GET | `/features` | — | `{ ai: boolean }` — público (sem login); diz ao frontend se a IA está ligada nesta instalação |

Campos aceitos em `suggest-field`: `name`, `race`, `occupation`, `appearance`, `personality`, `background`, `traits`, `actions`, `reactions`, `abilityScores`.

**Convenções:**

- Erros no formato `ProblemDetails`: 400 (validação), 401, 404, 429 (limite atingido), 502 (falha da IA), 504 (timeout da IA).
- Limites de uso da IA (detalhes na seção 6, "Limite diário de gerações"):
  - `generate-npc`: **5 por dia** por usuário (configurável em `Ai:DailyGenerationLimit`), persistido no banco.
  - `suggest-field`: sem limite diário; apenas limite por minuto contra abuso (middleware nativo de rate limiting do ASP.NET Core, padrão de 10 por minuto, configurável).
- O rascunho gerado pela IA só é persistido quando o usuário salva via `POST /npcs`, que reaplica todas as validações.

## 6. Integração com IA

```
AiController → NpcGenerationService → PromptBuilder
                       │
                       ├─► IAiProvider (Ollama | Nuvem) → JSON bruto
                       │
                       └─► NpcDraftValidator (parse, validação, correção) → draft + warnings
```

### IA ligada ou desligada (feature flag)

A mesma base de código serve a duas formas de rodar o app, controladas pela chave `Ai:Enabled` (padrão **`false`**):

- **Versão web (deploy público):** `Ai:Enabled = false`. Os endpoints `/ai/*` respondem **404** com o título "A IA está desativada nesta instalação.", nada de IA é registrado na injeção de dependências (nenhuma configuração de provedor é exigida) e o frontend esconde todos os elementos de IA. Criar e editar NPCs manualmente funciona normalmente.
- **Versão local com IA:** `Ai:Enabled = true` + Ollama instalado. O README traz um tutorial passo a passo ("Rodando com IA localmente").
- O frontend descobre o modo pelo `GET /features`; se a chamada falhar, assume IA desligada.
- No futuro (provedor em nuvem), a versão web pode ligar a IA só mudando a configuração, sem branch ou código separado.

Decisão: uma chave de configuração em vez de dois branches permanentes, para que correções e testes valham para as duas versões.

### Componentes

- **`IAiProvider`** (Core): `Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken ct)`. Sem conhecimento de RPG. As implementações ficam na Infrastructure. A seleção é feita por configuração: `Ai:Provider` = `Ollama` | `Cloud`.
- **`PromptBuilder`** (Core): monta o system prompt (assistente de mestre de D&D 5e, resposta somente em JSON no schema dado) e o user prompt (pedido do usuário + `Setting` da campanha + rascunho atual, no caso de `suggest-field`). Os templates de texto ficam em arquivos de recurso, separados da lógica.
- **`NpcDraftValidator`** (Core): código C# puro, sem IA. Faz o parse do JSON e aplica correções.
- **`NpcGenerationService`** (Core): orquestra o fluxo, as novas tentativas e o timeout.

### Tratamento de respostas ruins

1. Os provedores são chamados em modo de saída estruturada (JSON schema) sempre que suportado.
2. Se o parse falhar, o serviço faz **1 nova tentativa**, incluindo no prompt o erro de parse. Se falhar de novo → 502 com mensagem amigável.
3. JSON válido com valores fora das regras é **corrigido**, não rejeitado. Cada correção gera uma mensagem em `warnings`:
   - atributos limitados ao intervalo 1–30;
   - CA limitada ao intervalo 1–30;
   - PV ≤ 0 → recalculado a partir de `HitDice` (média), ou 1 se `HitDice` for inválido;
   - ND fora da lista → ND válido mais próximo;
   - `HitDice` em formato inválido → removido, com aviso;
   - campos de texto acima do limite → truncados.
4. Timeout configurável (`Ai:TimeoutSeconds`, padrão 60) → 504. Se o cliente cancelar a requisição, o `CancellationToken` é propagado até o provedor.

### Limite diário de gerações

- Implementado por um **`AiUsageService`** (Core, persistência via Infrastructure), e não pelo middleware de rate limiting, porque o contador precisa sobreviver a reinícios da API.
- O dia é o dia em UTC; o contador zera à meia-noite UTC. `resetsAt` informa esse horário ao front.
- **Só gerações bem-sucedidas contam.** Fluxo:
  1. Antes de chamar a IA, o serviço **reserva** uma geração com um UPDATE atômico condicional (`GenerationCount < limite`), o que impede que requisições simultâneas passem do limite.
  2. Se não houver reserva disponível → 429 com mensagem amigável e `resetsAt`, sem chamar a IA.
  3. Se a geração falhar (502, 504 ou cancelamento) → a reserva é devolvida (decremento).
- O horário atual vem de `TimeProvider` (injetado), para os testes conseguirem simular a virada do dia.

## 7. Frontend (Angular)

### Rotas

| Rota | Tela |
|---|---|
| `/login`, `/register` | autenticação |
| `/campaigns` | cards de campanhas + "Nova campanha" |
| `/campaigns/:id` | detalhe da campanha + seus NPCs |
| `/npcs` | todos os NPCs, com busca e filtros (campanha, com ficha / só narrativo) |
| `/npcs/new` | criação, iniciando pela caixa de prompt da IA |
| `/npcs/:id` | visualização, com a ficha no estilo do stat block clássico de D&D |
| `/npcs/:id/edit` | edição com botões ✨ por campo e toggle "Público" |
| `/explore` | galeria de NPCs públicos de outros usuários, com busca e filtro "com ficha" |
| `/explore/:id` | NPC público somente leitura + botão "Adicionar às minhas campanhas" |

Todas as rotas, exceto login e registro, são protegidas por `authGuard`.

### Fluxo de criação

1. O usuário informa o prompt, a campanha (opcional), o toggle "Incluir ficha de combate" e o ND (visível apenas com o toggle ligado).
2. "✨ Gerar com IA" → estado de carregamento → `form.patchValue(draft)` → `warnings` exibidos como avisos.
3. O usuário edita e pode regerar campos individuais com ✨.
4. "Salvar" → `POST /npcs`. O passo 1 é opcional: o usuário pode preencher tudo manualmente.

A tela mostra o contador "X de 5 gerações hoje" (via `GET /ai/usage`). Quando o limite acaba, o botão "✨ Gerar com IA" fica desabilitado com o horário de renovação, mas o preenchimento manual e os botões ✨ por campo continuam funcionando.

### Fluxo de NPC público

- No formulário do NPC, o toggle "Público" avisa que as anotações do mestre continuam privadas.
- Na visualização de um NPC copiado aparece "Baseado em um NPC de {SourceAuthorName}".
- Em `/explore/:id`, "Adicionar às minhas campanhas" abre um diálogo para escolher a campanha (ou "Sem campanha") e, após a cópia, leva ao NPC novo em `/npcs/:id`.

### Estrutura

```
src/app/
  core/       auth.service, jwt.interceptor, auth.guard, configuração da API
  features/
    auth/       login, register
    campaigns/  list, detail, form
    npcs/       list, view, form, stat-block-view, ai-prompt
    explore/    public-list, public-view, copy-dialog
  shared/     ai-suggest-button, confirm-dialog, loading
```

**Decisões:**

- Standalone components e signals para estado local; services para acesso à API. Sem NgRx.
- Reactive Forms para os formulários de NPC e campanha.
- Angular Material para os componentes de UI. O `stat-block-view` tem CSS próprio, com visual de pergaminho no estilo D&D.
- O `jwtInterceptor` anexa o token a todas as requisições; uma resposta 401 limpa a sessão e redireciona para `/login`.
- O token é guardado em `localStorage` (trade-off aceito na v1 pela simplicidade; documentado no README).
- Responsivo básico; o foco é desktop.

## 8. Testes

- **Unitários (xUnit):**
  - regras de D&D (modificador, bônus de proficiência por ND);
  - `NpcDraftValidator` com JSONs inválidos e valores absurdos fixos;
  - `PromptBuilder` (inclusão do setting da campanha e do rascunho);
  - `NpcGenerationService` com `FakeAiProvider` (sucesso, nova tentativa após parse inválido, falha após a nova tentativa, timeout).
  - `AiUsageService` com `TimeProvider` falso (virada do dia em UTC, cálculo de `resetsAt`).
- **Integração (WebApplicationFactory + Testcontainers/Postgres + `FakeAiProvider`):**
  - registro/login e acesso protegido;
  - CRUD de campanhas e NPCs, incluindo adicionar e remover a ficha;
  - isolamento entre usuários (usuário A recebe 404 ao acessar recurso do usuário B);
  - exclusão de campanha mantém os NPCs com `CampaignId = null`;
  - endpoints de IA retornando draft + warnings;
  - limite diário: a 6ª geração no dia retorna 429; geração que falha não consome o limite; no dia seguinte o limite volta; `suggest-field` não consome o limite diário;
  - NPCs públicos: NPC privado não aparece na galeria e retorna 404 para outros usuários; `Notes` não aparece na visão pública nem na cópia; a cópia é independente (editar ou apagar o original não altera a cópia); copiar para campanha de outro usuário retorna 404; outro usuário não consegue editar um NPC público.
- **Frontend:** testes dos services `auth.service` e `jwtInterceptor`. Testes de componentes ficam para depois.
- Nenhum teste depende de IA real ou de rede externa.

## 9. Repositório e infraestrutura

```
npc-forge/
  backend/
    NpcForge.sln
    src/NpcForge.Api, NpcForge.Core, NpcForge.Infrastructure
    tests/NpcForge.UnitTests, NpcForge.IntegrationTests
  frontend/
  docs/
  docker-compose.yml
  .env.example
  README.md
```

- `docker compose up` sobe Postgres, Ollama e a API. O Angular roda com `ng serve` em desenvolvimento.
- Migrations do EF Core aplicadas automaticamente no startup apenas no ambiente de desenvolvimento.
- Configuração por variáveis de ambiente: connection string, `Ai__Enabled`, `Ai__Provider`, `Ai__TimeoutSeconds`, `Ai__DailyGenerationLimit`, chave do provedor em nuvem, segredo e expiração do JWT. O `.env.example` fica versionado, e nenhum segredo é commitado.
- CORS liberado apenas para a origem do front configurada.
- Preparação para deploy (fora da v1): Dockerfile do front (build + nginx) e troca de provedor via configuração.

## 10. Ordem de construção

1. Backend: solução, entidades, EF Core + Postgres, auth, CRUD de campanhas e NPCs, testes.
2. Angular: setup, login/registro, listas e formulários (sem IA).
3. IA: `IAiProvider`, `OllamaProvider`, `PromptBuilder`, validador, `generate-npc` com limite diário (`AiUsageService`) e a tela de criação com prompt e contador.
4. `suggest-field` + botões ✨ + exibição de warnings.
5. NPCs públicos: `isPublic`, galeria `/explore`, visualização pública e cópia para campanha.
6. Provedor em nuvem, limite por minuto no `suggest-field`, polimento visual (stat block) e README com prints/GIF.
