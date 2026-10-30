# Publicando a versão web (Azure + Neon)

Passo a passo para pôr o NpcForge no ar **sem custo**:
- **Frontend:** Azure Static Web Apps (Free).
- **API:** Azure App Service F1 (Free).
- **Banco:** Neon (Free).

Depois de configurado, cada push no `main` publica sozinho, pelo workflow `.github/workflows/deploy.yml`.

> Os planos gratuitos mudam com o tempo. Se algum passo não bater com o que você vê no portal, confira a página oficial do serviço.

## O que você vai precisar

- Conta na Azure (pede cartão, mesmo nos planos gratuitos) e conta no Neon.
- Acesso de administrador ao repositório no GitHub (para cadastrar os *secrets*).
- Um nome único para a API, por exemplo `npcforge-api-jg`. Ele vira o endereço `https://npcforge-api-jg.azurewebsites.net`. Neste guia ele aparece como `npcforge-api-<sufixo>`.

## 1. Alerta de orçamento (Azure)

Portal da Azure → **Gerenciamento de Custos** → **Orçamentos** → **Adicionar**.
- Escopo: a sua assinatura.
- Valor: R$ 1 por mês.
- Alerta por e-mail em 100% do valor.

Assim, se algo começar a cobrar, você fica sabendo no mesmo dia.

## 2. Banco no Neon

1. No console do Neon: **New Project**.
   - Nome `npcforge`, Postgres 17.
   - Região **AWS US East 1 (N. Virginia)**. O Neon não aceita mais projetos novos na Azure; essa região fica perto da API.
2. Em **Connect**, escolha o banco `neondb` (ou crie `npcforge`) e **desligue "Connection pooling"**. Use a conexão direta, que funciona com o EF Core sem ajustes.
3. O Neon mostra uma URL assim:
   ```
   postgresql://usuario:senha@ep-xxxx.us-east-1.aws.neon.tech/neondb?sslmode=require
   ```
   Converta para o formato do .NET (Npgsql):
   ```
   Host=ep-xxxx.us-east-1.aws.neon.tech;Database=neondb;Username=usuario;Password=senha;SSL Mode=Require
   ```
   Guarde essa string: ela é a **connection string do Neon** usada nos passos 4 e 7. Não a coloque em nenhum arquivo do repositório.

## 3. Grupo de recursos (Azure)

Portal → **Grupos de recursos** → **Criar**: nome `npcforge-rg`, região **East US**.

## 4. API no App Service

1. Portal → **Serviços de Aplicativos** → **Criar** → **Aplicativo Web**:
   - Grupo de recursos: `npcforge-rg`. Nome: `npcforge-api-<sufixo>`.
   - Publicar: **Código**. Pilha de runtime: **.NET 10**. Sistema operacional: **Linux**. Região: **East US**.
   - Plano: crie um novo e escolha **Gratuito F1**.

   > Se **.NET 10** não aparecer na lista de pilhas para Linux, pare aqui: o plano tem uma alternativa (rodar a imagem Docker da API) que muda o workflow.
2. Depois de criado, abra o app → **Configurações** → **Variáveis de ambiente** → **Configurações de aplicativo** e adicione:

   | Nome | Valor |
   |---|---|
   | `ConnectionStrings__Default` | a connection string do Neon (passo 2) |
   | `Jwt__Key` | uma chave aleatória longa (veja abaixo) |
   | `Cors__AllowedOrigins__0` | o endereço do site (preenchido no passo 5) |
   | `Ai__Enabled` | `false` |
   | `Demo__Enabled` | `true` |
   | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `false` |

   Sobre a última linha: a API já lê o IP real do visitante sozinha. Se essa variável ficar `true` (algumas imagens Linux da Azure ligam por padrão), o cabeçalho seria lido duas vezes, e um visitante poderia forjar o IP para escapar do limite de tentativas.

   Para gerar a `Jwt__Key` (no Git Bash): `openssl rand -base64 48`. Guarde a chave só ali, nas configurações da Azure.
3. **Configurações** → **Configuração** → **Configurações gerais**: ligue **Somente HTTPS**.

## 5. Site no Static Web Apps

1. Portal → **Aplicativos Web Estáticos** → **Criar**:
   - Grupo de recursos `npcforge-rg`, nome `npcforge-web`, plano **Gratuito**.
   - Origem da implantação: **Outro**. O deploy vem do nosso workflow, não do assistente da Azure.
2. Na página do app, copie a **URL** (algo como `https://xxxx.azurestaticapps.net`) e coloque em `Cors__AllowedOrigins__0` da API (passo 4), **sem barra no final**.
3. Em **Gerenciar token de implantação**, copie o token: ele vai para o GitHub no passo 7.

## 6. Login do GitHub na Azure (OIDC, sem senha)

Em vez de guardar uma senha da Azure no GitHub, a Azure passa a confiar nos workflows deste repositório.

1. Portal → **Microsoft Entra ID** → **Registros de aplicativo** → **Novo registro**: nome `npcforge-github` (o resto padrão).
2. Na página do registro, anote **ID do aplicativo (cliente)** e **ID do diretório (locatário)**.
3. **Certificados e segredos** → **Credenciais federadas** → **Adicionar credencial**:
   - Cenário: **GitHub Actions implantando recursos do Azure**.
   - Organização `JGabriel02`, repositório `npc-forge`, entidade **Branch**, branch `main`.
4. Grupo de recursos `npcforge-rg` → **Controle de acesso (IAM)** → **Adicionar atribuição de função** → função **Colaborador** → membro: `npcforge-github`.
5. Anote o **ID da assinatura** (Portal → **Assinaturas**).

## 7. Secrets do GitHub

Repositório → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret | Valor |
|---|---|
| `NEON_CONNECTION_STRING` | a connection string do Neon (passo 2) |
| `AZURE_CLIENT_ID` | ID do aplicativo (passo 6) |
| `AZURE_TENANT_ID` | ID do locatário (passo 6) |
| `AZURE_SUBSCRIPTION_ID` | ID da assinatura (passo 6) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | token de implantação (passo 5) |

Os três IDs não são senhas, mas ficam como secrets para não aparecerem nos logs.

## 8. Recriar a conta de visitante

A conta demo é criada no primeiro clique em **Entrar como visitante**, com o conteúdo de `backend/src/NpcForge.Infrastructure/Demo/demo-npcs.json`. Para trocar o conteúdo depois de alterar esse arquivo e publicar:

1. No Neon, abra o **SQL Editor** e rode:
   ```sql
   DELETE FROM "AspNetUsers" WHERE "Email" = 'visitante@npcforge.demo';
   ```
   Campanhas, NPCs e fichas da conta saem junto (exclusão em cascata).
2. O próximo "Entrar como visitante" recria tudo.

## 9. O que esperar dos planos gratuitos

- **App Service F1:** cerca de 60 minutos de CPU por dia. Sem "Always On": a API dorme quando fica parada, e a primeira visita pode levar até um minuto (o site mostra "Acordando o servidor…").
- **Neon Free:** o banco suspende depois de 5 minutos parado e acorda na próxima consulta; 1 GB por projeto.
- **Static Web Apps Free:** 100 GB de tráfego por mês.

## Endereços desta instalação

Nada secreto aqui:

- Site: `https://calm-mushroom-02327b90f.5.azurestaticapps.net`
- API: `https://npcforge-api-dxbwdeethba8dyar.westus3-01.azurewebsites.net` (App Service `npcforge-api`, região West US 3)

> Apps novos do App Service ganham um **domínio padrão único** (nome + código + região), não `<nome>.azurewebsites.net`. Copie o endereço do campo **Domínio padrão** na Visão geral do app; é ele que vai em `frontend/src/environments/environment.ts` e em `API_URL` no workflow.
