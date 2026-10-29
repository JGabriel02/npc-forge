# NpcForge

Aplicação web para mestres de RPG criarem e organizarem NPCs de D&D 5e, com ou sem ficha de combate, agrupados em campanhas, e com uma IA integrada que gera e sugere conteúdo.

> Projeto de portfólio e aprendizado em **.NET** e **Angular**. A IA chega nas próximas fases (veja [Roadmap](#roadmap)).

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

### Rodando a API fora do Docker

Pré-requisito extra: .NET SDK 10. Suba só o banco (`docker compose up -d postgres`) e configure os segredos locais:

```bash
cd backend/src/NpcForge.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<32+ caracteres aleatórios>"
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=npcforge;Username=npcforge;Password=<sua senha>"
dotnet run
```

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
3. IA (Ollama) + limite diário de gerações
4. Sugestões por campo ✨
5. NPCs públicos
6. Provedor de IA em nuvem, polimento e prints neste README
