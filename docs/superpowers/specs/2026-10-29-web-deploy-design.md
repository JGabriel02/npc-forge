# NpcForge: versão web no ar (design)

**Data:** 2026-10-29
**Status:** aguardando revisão
**Complementa:** `docs/superpowers/specs/2026-10-28-npcforge-design.md`

## 1. Objetivo

Colocar a versão web do NpcForge (sem IA) num endereço público, com custo zero, para servir de portfólio e de destino dos NPCs exportados da versão local.

**Critérios de sucesso:**

1. Um link público abre o NpcForge. Qualquer pessoa cria conta, cria NPCs e importa arquivos de exportação.
2. Um visitante clica em **"Entrar como visitante"** e vê NPCs e campanhas de exemplo sem criar conta, mas não consegue alterar nada.
3. Cada push no `main` roda os testes e, se passarem, publica API e frontend sozinho; com qualquer teste falhando, nada é publicado.
4. Nenhum segredo (connection string, chave JWT) fica no repositório.
5. A primeira visita depois do servidor "dormir" mostra um aviso amigável, não um erro.

## 2. Decisões

| Tema | Decisão |
|---|---|
| Frontend | Azure Static Web Apps, plano Free. |
| API | Azure App Service, plano F1 (Linux, .NET 10), recebendo o código publicado (sem Docker). Se o F1 Linux não suportar .NET 10 na hora do plano, a mesma App Service roda a imagem do nosso Dockerfile. |
| Banco | PostgreSQL no Neon, plano Free, `SSL Mode=Require`, na região mais próxima da API. |
| Endereços | Os gratuitos da Azure (`*.azurewebsites.net`, `*.azurestaticapps.net`). Sem domínio próprio. |
| Acesso público | Cadastro aberto + limite de tentativas por IP + conta de visitante somente leitura. |
| Deploy | GitHub Actions, automático a cada push no `main`, só com todos os testes passando. |
| Migrations | Aplicadas pelo pipeline com um *migration bundle* do EF, **antes** de publicar a API. Se falharem, o deploy para e a versão anterior continua no ar. |
| Login do GitHub na Azure | OIDC (credencial federada): a Azure confia no repositório; nenhuma senha da Azure fica no GitHub. |
| Criação dos recursos | Manual, uma vez, guiada por `docs/deploy.md` (portal ou Azure CLI). Sem infraestrutura como código por enquanto. |

**Fora deste design:** domínio próprio, Bicep/Terraform, Application Insights, ambiente de homologação, IA na nuvem, NPCs públicos, modo local sem login.

## 3. Arquitetura

```
Visitante → https://<nome>.azurestaticapps.net   (Angular, Static Web Apps Free)
          → https://<nome>.azurewebsites.net/api (API .NET, App Service F1)
          → PostgreSQL (Neon Free)
```

**Frontend.**
- `environment.ts` (build de produção) passa a apontar para `https://<nome>.azurewebsites.net/api`; `environment.development.ts` continua em `localhost:8080`.
- `staticwebapp.config.json` com `navigationFallback` para `index.html`, para que recarregar uma rota interna (ex.: `/npcs/123`) não dê 404.

**API: configuração de produção nas App Settings da Azure** (variáveis de ambiente; `__` separa seções):

| Chave | Valor |
|---|---|
| `ConnectionStrings__Default` | Connection string do Neon, formato Npgsql (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`) |
| `Jwt__Key` | Chave aleatória de 32+ caracteres, gerada uma vez |
| `Cors__AllowedOrigins__0` | `https://<nome>.azurestaticapps.net` |
| `Ai__Enabled` | `false` |
| `Demo__Enabled` | `true` |

As migrations deixam de rodar sozinhas fora de Development (já é assim hoje): em produção, só o pipeline migra.

## 4. Conta de visitante

**Chave `Demo:Enabled`** (padrão `false`; ligada só na versão web). Com ela desligada, nada abaixo existe: a rota responde 404 e o seeder não roda.

**`POST /api/auth/demo`** (anônima): devolve o mesmo formato do login (token + usuário) para a conta demo. O token leva a claim `demo` = `true`. A conta demo não tem senha utilizável: só se entra por esta rota.

**Somente leitura, garantido no backend.** Um filtro global recusa com **403** qualquer requisição `POST`, `PUT` ou `DELETE` feita com token demo, com a mensagem:

> A conta de visitante é só para leitura. Crie uma conta para salvar seus NPCs.

`GET` continua liberado, inclusive a exportação. A importação (`POST`) fica bloqueada. As rotas de autenticação anônimas (cadastro, login, visitante) não são afetadas, porque não usam o token.

**No Angular:**
- Botão **"Entrar como visitante"** na tela de login.
- Faixa no topo para o visitante: "Você está como visitante · Criar conta" (o link sai da conta demo e abre o cadastro).
- Para o visitante, ficam escondidos: Novo NPC, Editar, Excluir, Importar e as ações de escrita de campanhas. Se mesmo assim chegar um 403, a mensagem acima aparece como nas outras falhas.

**Conteúdo da demo.**
- Arquivo `demo-npcs.json` versionado no repositório, no formato de exportação (`npcforge-npcs`, versão 1), com um campo opcional `"campaign"` por NPC (nome da campanha) lido só pelo seeder.
- Personagens **originais** (nada de livros publicados): de 6 a 10 NPCs, entre narrativos e com ficha, em uma ou duas campanhas e alguns sem campanha.
- Na subida da API, com `Demo:Enabled`, o seeder cria a conta demo, as campanhas e os NPCs **somente se a conta demo ainda não existir**. Reiniciar não duplica; mudar o arquivo depois exige apagar a conta demo à mão (fica documentado em `docs/deploy.md`).
- O NPC é validado pelas mesmas regras da importação; um arquivo de demo inválido derruba a subida com erro claro no log (e o teste do seeder pega isso antes do deploy).

## 5. Limite de tentativas

- *Rate limiter* do ASP.NET, janela fixa por IP, aplicado a cadastro, login e visitante: **10 requisições por minuto por IP** (configurável, `RateLimit:AuthPerMinute`).
- Acima do limite: **429** com `ProblemDetails`: "Muitas tentativas. Aguarde um minuto e tente de novo." O Angular mostra essa mensagem nas telas de login e cadastro.
- **Forwarded headers:** na Azure, a requisição chega por um proxy. A API usa `UseForwardedHeaders` (lendo `X-Forwarded-For` e `X-Forwarded-Proto`) para enxergar o IP real; sem isso, todos os visitantes dividiriam o mesmo limite.

## 6. Rota de saúde e "servidor dormindo"

- **`GET /api/health`** (anônima): 200 se a API está de pé e consegue consultar o banco; 503 se o banco não responde.
- No Angular, um interceptor HTTP: se uma requisição passar de **4 segundos**, mostra "Acordando o servidor… a primeira visita do dia pode levar até um minuto." até ela terminar. Nenhuma requisição é cancelada antes de **90 segundos**.

## 7. Pipeline (`.github/workflows/deploy.yml`)

**Em todo push e pull request:**
1. Backend: `dotnet test backend/NpcForge.slnx` (o runner `ubuntu-latest` tem Docker, então Testcontainers funciona).
2. Frontend: `npm ci`, `ng test --watch=false`, `ng build`.

**Só em push no `main`, depois dos testes:**
3. **API:**
   1. `dotnet publish`;
   2. gerar o *migration bundle* (`dotnet ef migrations bundle`);
   3. rodá-lo contra o Neon (connection string vinda de um secret do GitHub);
   4. login na Azure por OIDC;
   5. publicar no App Service.
4. **Frontend:** publicar a pasta do build no Static Web Apps (token de deploy num secret do GitHub).
5. **Smoke test:** `GET /api/health` e `GET /api/features` respondem 200 (com espera e novas tentativas, por causa do "sono").

**Secrets do GitHub:**
- `NEON_CONNECTION_STRING`;
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (IDs do OIDC, não são senhas);
- `AZURE_STATIC_WEB_APPS_API_TOKEN`.

O nome do App Service e as URLs ficam como variáveis do workflow, não como secrets.

## 8. Testes

**Backend:**
- **Visitante:** `POST /api/auth/demo` devolve o token com a claim `demo` quando `Demo:Enabled=true`, e 404 quando é `false`.
- **Somente leitura:**
  - com token demo, criar/editar/excluir NPC, a ficha, campanhas e a importação dão 403 com a mensagem;
  - listar, ver e exportar dão 200;
  - um usuário comum continua podendo alterar.
- **Seeder:**
  - rodar duas vezes não duplica;
  - o `demo-npcs.json` real do repositório é válido;
  - as campanhas e os vínculos são criados.
- **Limite de tentativas:**
  - acima do limite (configurado baixo no teste) dá 429 com a mensagem;
  - IPs diferentes em `X-Forwarded-For` contam separadamente.
- **Saúde:** `/api/health` responde 200 com o banco no ar.

**Frontend:**
- botão de visitante;
- faixa de visitante;
- botões de escrita escondidos para o visitante;
- mensagem de "acordando" depois de 4 s (com relógio falso);
- mensagem de 429 no login.

**No ar** (depois do primeiro deploy, no navegador):
- cadastro e login;
- visitante (ver, exportar, tentar alterar);
- importar um arquivo;
- recarregar uma rota interna;
- limite de tentativas;
- primeira visita com o servidor dormindo.

## 9. Documentação

- **`docs/deploy.md`**, um passo a passo:
  - criar o projeto no Neon e converter a connection string;
  - criar o App Service F1 e as App Settings;
  - criar o Static Web Apps;
  - configurar o OIDC (registro de aplicativo, credencial federada, permissão no grupo de recursos);
  - cadastrar os secrets do GitHub;
  - gerar a chave JWT;
  - recriar a conta demo;
  - configurar um alerta de orçamento na Azure.
- **README:** link do site, prints das telas, aviso sobre o "sono" do plano gratuito e link para `docs/deploy.md`.

## 10. A confirmar ao escrever o plano

Regras dos provedores mudam; o plano confere nas páginas oficiais:
- se o F1 Linux suporta .NET 10;
- o limite de planos gratuitos por assinatura e região;
- as regiões do Neon;
- os limites atuais dos planos gratuitos.
