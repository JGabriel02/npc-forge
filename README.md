# NpcForge

Aplicação web para mestres de RPG criarem e organizarem NPCs de D&D 5e, com ou sem ficha de combate, agrupados em campanhas, e com uma IA integrada que gera e sugere conteúdo.

> Projeto de portfólio e aprendizado em **.NET** e **Angular**.
>
> A IA é **opcional** e vem **desligada**: sem ela, o app funciona normalmente com criação manual de NPCs (é assim que roda a versão web). Para gerar NPCs com IA no seu computador, siga [Rodando com IA localmente](#rodando-com-ia-localmente).

## Stack

- **Backend:** .NET 10, ASP.NET Core (controllers), EF Core + PostgreSQL, ASP.NET Core Identity + JWT
- **Frontend:** Angular 22 (standalone, signals, zoneless), Angular Material, Reactive Forms
- **Testes:** xUnit, `WebApplicationFactory` e Testcontainers (Postgres real em Docker); Vitest no frontend
- **Infra:** Docker Compose

## Como rodar

Pré-requisitos: [Docker Desktop](https://www.docker.com/products/docker-desktop/) e [Node.js](https://nodejs.org/) 22.22+.

1. Suba o banco e a API:

   ```bash
   cp .env.example .env   # preencha POSTGRES_PASSWORD e JWT_KEY
   docker compose up -d --build
   ```

   A API sobe em `http://localhost:8080` e aplica as migrations do banco sozinha. O documento OpenAPI fica em `http://localhost:8080/openapi/v1.json`.

2. Suba o frontend:

   ```bash
   cd frontend
   npm install
   npx ng serve
   ```

   Abra `http://localhost:4200` e crie uma conta.

## Rodando com IA localmente

Com a IA ligada, a tela **Novo NPC** ganha o card **"✨ Gerar com IA"**: você descreve o NPC ("anão ferreiro rabugento, guarda um segredo") e a IA preenche o formulário, com ou sem ficha de combate. Nada é salvo até você revisar e clicar em Salvar. Não há limite de gerações: a IA roda na sua máquina, então o único custo é a placa de vídeo ocupada por alguns segundos a cada geração. Se quiser um teto diário por usuário, defina `AI_DAILY_LIMIT` no `.env`.

A IA roda **no seu computador** com o [Ollama](https://ollama.com), sem custo e sem enviar dados para fora.

1. **Instale o Ollama** pelo site oficial e confirme no terminal:

   ```bash
   ollama --version
   ```

2. **Baixe o modelo** (cerca de 9 GB; fica rápido com uma GPU de ~10 GB de VRAM ou mais):

   ```bash
   ollama pull qwen2.5:14b
   ```

3. **Ligue a IA** no `.env`:

   ```bash
   AI_ENABLED=true
   ```

4. **Suba o app de novo** e abra "Novo NPC":

   ```bash
   docker compose up -d --build
   ```

   Se `http://localhost:8080/api/features` mostrar `{"ai":true}`, está tudo certo.

   **No Linux:** o Ollama instalado só aceita conexões de `127.0.0.1`, e a API dentro do Docker não consegue alcançá-lo (você veria "A IA não está disponível agora." mesmo com `ollama list` funcionando). Faça o Ollama escutar em todas as interfaces e reinicie-o:

   ```bash
   sudo systemctl edit ollama   # adicione as duas linhas abaixo e salve
   # [Service]
   # Environment="OLLAMA_HOST=0.0.0.0"
   sudo systemctl restart ollama
   ```

   No Windows e no macOS (Docker Desktop) não é preciso fazer nada.

5. **Outros modelos:** qualquer modelo do Ollama funciona; basta baixá-lo com `ollama pull` e colocar o nome em `OLLAMA_MODEL`. O padrão, `qwen2.5:14b`, foi testado com este app (bom em português e em seguir o formato JSON). Modelos maiores costumam gerar NPCs melhores e mais lentos; modelos pequenos podem errar mais o formato — o app corrige o que der e avisa o que ajustou.

   **Sem GPU ou com pouca memória:** use um modelo menor e dê mais tempo para a resposta. No `.env`, `OLLAMA_MODEL=qwen2.5:7b` (ou `qwen2.5:3b`), e no `docker-compose.yml`, `Ai__TimeoutSeconds: 180` no serviço `api`. Não esqueça de rodar `ollama pull` do modelo escolhido.

6. **Sem o Ollama instalado:** dá para rodá-lo dentro do Docker (bem mais lento sem GPU):

   ```bash
   docker compose --profile ollama-docker up -d
   docker compose exec ollama ollama pull qwen2.5:14b
   ```

   e no `.env`: `OLLAMA_BASE_URL=http://ollama:11434`.

7. **Problemas comuns:**
   - *"A IA não está disponível agora."* — o Ollama não está rodando. Abra o aplicativo do Ollama (ou rode `ollama serve`) e confira com `ollama list`.
   - *"O modelo ... não está instalado no Ollama"* — rode o `ollama pull` do modelo que está no `.env`.
   - *"A IA demorou demais para responder."* — modelo pesado para a sua máquina; use um menor (passo 5).

### Rodando a API fora do Docker

Pré-requisito extra: .NET SDK 10. Suba só o banco (`docker compose up -d postgres`) e configure os segredos locais:

```bash
cd backend/src/NpcForge.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<32+ caracteres aleatórios>"
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=npcforge;Username=npcforge;Password=<sua senha>"
dotnet run
```

## Levando seus NPCs para a versão web

Os NPCs criados na versão local (com IA) podem ir para a versão web por um arquivo JSON:

1. **Exporte na versão local.** Na tela **NPCs**, o botão **Exportar** baixa todos os NPCs do filtro atual (todas as páginas, não só a visível). Na página de um NPC, **Exportar** baixa só ele. O arquivo leva o NPC, a ficha de combate e suas anotações; campanhas ficam de fora.
2. **Importe na versão web.** Em **NPCs**, clique em **Importar** e escolha o arquivo. Você vê a lista do que vai entrar antes de confirmar.
3. **Organize.** Os NPCs importados entram **sem campanha**; abra cada um e escolha a campanha depois.

Bom saber:
- Importar o mesmo arquivo de novo **cria cópias**, não atualiza os que já existem.
- É **tudo ou nada**: se um NPC do arquivo tiver erro (ex.: Força 31), nada é importado e a tela diz qual NPC corrigir.
- Limites: até 500 NPCs e 5 MB por arquivo.

## Testes

Backend, com o Docker rodando (os testes de integração sobem um Postgres temporário):

```bash
dotnet test backend/NpcForge.slnx
```

Frontend:

```bash
cd frontend
npx ng test --watch=false
```

## API (v1)

| Recurso | Rotas |
|---|---|
| Autenticação | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Campanhas | `GET/POST /api/campaigns`, `GET/PUT/DELETE /api/campaigns/{id}` |
| NPCs | `GET /api/npcs?campaignId=&search=&hasStatBlock=&page=&pageSize=`, `POST /api/npcs`, `GET/PUT/DELETE /api/npcs/{id}` |
| Ficha | `PUT/DELETE /api/npcs/{id}/statblock` |
| Exportar/importar | `GET /api/npcs/export` (mesmos filtros da lista), `GET /api/npcs/{id}/export`, `POST /api/npcs/import` |

Todas as rotas, exceto registro e login, exigem `Authorization: Bearer <token>`. Erros seguem o formato `ProblemDetails`.

## Arquitetura

```
backend/src/
  NpcForge.Api            → controllers, autenticação, configuração
  NpcForge.Core           → entidades, regras de D&D, DTOs, interfaces de serviço
  NpcForge.Infrastructure → EF Core + PostgreSQL, implementações dos serviços

frontend/src/app/
  core/      → autenticação (service, interceptor JWT, guards), layout, modelos da API
  features/  → auth, campaigns, npcs (cada tela com seu service de API)
  shared/    → diálogos e utilitários (formatação de D&D, mensagens de erro)
```

Decisões e o design completo: [docs/superpowers/specs](docs/superpowers/specs/2026-10-28-npcforge-design.md).

## Roadmap

1. ~~Fundação do backend~~
2. ~~Frontend Angular (sem IA)~~
3. ~~IA local (Ollama), com contador diário de gerações~~
   - ~~Exportar/importar NPCs em arquivo JSON (local → web)~~
4. Sugestões por campo ✨
5. NPCs públicos
6. Provedor de IA em nuvem, polimento e prints neste README
