# NpcForge — Exportar e importar NPCs (design)

**Data:** 2026-10-29
**Status:** aguardando revisão
**Complementa:** `docs/superpowers/specs/2026-10-28-npcforge-design.md`

## 1. Objetivo

Levar NPCs criados na **versão local** (onde a IA está disponível) para a **versão web** (sem IA), usando um arquivo JSON: exporta-se numa instalação e importa-se na outra.

**Critérios de sucesso:**

1. Na versão local, o usuário exporta os NPCs da lista (com os filtros atuais) ou um NPC específico, e o navegador baixa um arquivo `.json`.
2. Na versão web, o usuário importa esse arquivo e os NPCs aparecem com os mesmos textos, ficha, ações e anotações, **sem campanha**, prontos para serem atribuídos a uma campanha.
3. Um arquivo com qualquer problema não importa nada e mostra exatamente o que está errado.

## 2. Decisões

| Tema | Decisão |
|---|---|
| Direção | O fluxo pensado é local → web. As funcionalidades existem nas duas versões (código único, sem chave de configuração); o README descreve o fluxo. |
| O que vai no arquivo | Só NPCs e suas fichas, incluindo as anotações do mestre (quem exporta e quem importa é a mesma pessoa). **Sem** campanhas, ids, datas ou dados de conta. |
| O que exportar | A lista de NPCs com os filtros atuais (busca, campanha, tipo), todas as páginas; ou um NPC pela página dele. |
| Campanha na importação | Todos os NPCs importados entram em "Sem campanha"; o usuário atribui depois pela edição normal. |
| Importar o mesmo arquivo de novo | Cria cópias novas (duplicados são apagados à mão). O arquivo não carrega identificador de origem. |
| Arquivo com erro | Tudo ou nada: valida o arquivo inteiro antes de gravar; com qualquer erro, nada é importado e a resposta lista os problemas por NPC. |
| Abordagem | Exportação e importação pela API (endpoints novos), não pelo navegador: o "tudo ou nada" é garantido por uma transação do banco e o formato fica definido só no backend. |

**Fora deste design:** modo local sem login (projeto separado, a ser desenhado depois como outra chave de configuração), exportar/importar campanhas, sincronização automática, NPCs públicos (já previstos na spec principal).

## 3. Formato do arquivo (versão 1)

```json
{
  "format": "npcforge-npcs",
  "version": 1,
  "exportedAt": "2026-10-29T21:30:00Z",
  "npcs": [
    {
      "name": "Rahadin",
      "race": "elfo das sombras",
      "occupation": "camareiro do castelo",
      "appearance": "…",
      "personality": "…",
      "background": "…",
      "notes": "…",
      "statBlock": {
        "size": "Medium", "creatureType": "humanoide (elfo)", "alignment": "leal e mau",
        "armorClass": 18, "hitPoints": 135, "hitDice": "18d8 + 54", "speed": "10,5 m",
        "str": 14, "dex": 22, "con": 17, "int": 15, "wis": 16, "cha": 18,
        "challengeRating": "10",
        "savingThrows": "Con +7, Sab +7", "skills": "…", "senses": "…", "languages": "Comum, Élfico",
        "traits": [{ "name": "…", "description": "…" }],
        "actions": [{ "name": "Cimitarra", "description": "…", "attackBonus": 9, "damage": "1d6 + 6 Cortante" }],
        "reactions": []
      }
    }
  ]
}
```

- `format` identifica o arquivo; `version` permite evoluir o formato (o app lê versões iguais ou anteriores à que conhece).
- Cada NPC tem os mesmos campos e regras de `CreateNpcRequest`, exceto `campaignId`. `statBlock` é `null` para NPCs narrativos e tem o mesmo formato de `StatBlockRequest` (sem valores derivados como modificadores).
- Campos desconhecidos são ignorados na importação (compatibilidade com versões futuras que acrescentem campos).
- Nome do arquivo baixado: `npcs-AAAA-MM-DD.json` (lista) ou `npc-<nome-em-slug>.json` (um NPC).

## 4. Backend

### Endpoints

| Método | Rota | Resposta |
|---|---|---|
| GET | `/api/npcs/export?search=&campaignId=&hasStatBlock=` | 200, arquivo JSON (`Content-Disposition: attachment`), com todos os NPCs do usuário que batem com os filtros (mesma semântica de `GET /api/npcs`, sem paginação) |
| GET | `/api/npcs/{id}/export` | 200, arquivo com um NPC; 404 se não for do usuário |
| POST | `/api/npcs/import` | 201 `{ "imported": N }`; 400 `ValidationProblem` |

### Validação da importação (tudo antes de gravar)

1. Corpo maior que 5 MB → 400 (limite de tamanho da requisição no endpoint).
2. `format` diferente de `"npcforge-npcs"` → 400 "Este arquivo não é uma exportação de NPCs do NpcForge."
3. `version` maior que a suportada (1) ou menor que 1 → 400 "Este arquivo foi exportado por uma versão mais nova do NpcForge." (ou "Versão de arquivo inválida.").
4. `npcs` ausente ou vazio → 400 "O arquivo não tem NPCs."; mais de 500 → 400 "O arquivo tem mais de 500 NPCs."
5. Cada NPC validado com as mesmas anotações do cadastro (`[Required]`, `[MaxLength]`, `[Range]`, `[ChallengeRating]`, `[HitDice]`, `[NoNullElements]`, limite de 30 itens por lista, tamanho válido). Erros com a posição: `npcs[2].statBlock.str`.

### Gravação

- Todos os NPCs criados com ids novos, `UserId` do usuário logado, `CampaignId = null`, `CreatedAt/UpdatedAt` de agora, num único `SaveChanges` (transação implícita do EF: ou entram todos, ou nenhum).
- Dados de vida normalizados como no cadastro (`NormalizeHitDice`).

### Componentes

- **Core:** `NpcExportFile` (formato, versão, data, lista), `ExportedNpc` (campos do NPC + `StatBlockRequest?`), `INpcTransferService`.
- **Infrastructure:** `NpcTransferService` — `ExportAsync(userId, NpcQuery filtros)`, `ExportOneAsync(userId, id)`, `ImportAsync(userId, NpcExportFile)`; reaproveita `NpcMapping.ToEntity` e a mesma lógica de filtros do `NpcService` (extraída para um método compartilhado, sem duplicar a consulta).
- **Api:** ações novas no `NpcsController`.

## 5. Frontend

### Exportar

- **Lista de NPCs:** botão "Exportar" no cabeçalho, ao lado de "Novo NPC"; exporta todos os NPCs dos filtros atuais (todas as páginas). Dica do botão: "Exportar N NPCs". Desabilitado com a lista vazia.
- **Página do NPC:** botão "Exportar" ao lado de "Editar" e "Excluir".
- O download usa o arquivo e o nome devolvidos pela API (`Content-Disposition`).

### Importar (`ImportNpcsDialog`)

Botão "Importar" na lista de NPCs abre um diálogo:

1. **Escolher arquivo** (`.json`). O navegador lê o arquivo localmente e mostra uma prévia: "N NPCs encontrados", com nome e selo "com ficha"/"narrativo". JSON inválido ou `format` diferente → aviso no próprio diálogo, sem chamar a API.
2. **Confirmar:** "Os NPCs entram sem campanha. Importar o mesmo arquivo de novo cria cópias."
3. **Resultado:**
   - Sucesso: o diálogo fecha, aviso "N NPCs importados" e a lista abre filtrada em "Sem campanha".
   - Erro de validação (400): nada foi importado; o diálogo mostra a lista de problemas por NPC ("NPC 3 (Rahadin): Nível de desafio inválido.") e permite escolher outro arquivo.

### Componentes

- `NpcsApi`: `exportList(filtros)`, `exportOne(id)`, `importFile(conteúdo)`.
- `npc-transfer.ts` (funções puras): `readExportFile(texto)` (parse + checagem de `format`/`version` + resumo para a prévia) e `describeImportErrors(problema, arquivo)` (transforma `npcs[2].statBlock.str` em "NPC 3 (Rahadin): Força: …").
- `ImportNpcsDialog` no padrão dos diálogos existentes.

## 6. Testes

**Backend (integração, Postgres real):**
- Exportar com filtros traz só os NPCs que batem e todas as páginas.
- Exportar não inclui ids, campanha, datas nem NPCs de outro usuário; exportar um NPC de outro usuário → 404.
- Ida e volta: exportar → importar com outro usuário → NPCs iguais (textos, ficha, ações, anotações), sem campanha e com ids novos.
- Importar o mesmo arquivo duas vezes cria duplicados.
- Tudo ou nada: 3 NPCs com o 2º inválido (Força 31) → 400 com erro em `npcs[1].statBlock.str` e nenhum NPC criado.
- Formato errado, versão mais nova, lista vazia, mais de 500 NPCs → 400 com mensagem em português.
- Campos desconhecidos ignorados; dados de vida normalizados.

**Frontend (unitários):** `readExportFile` (JSON inválido, formato errado, versão mais nova, prévia correta) e `describeImportErrors`.

**Navegador:** exportar um NPC gerado com IA na versão local, importar com outra conta, conferir a ficha sem campanha, atribuir uma campanha; importar um arquivo com erro (nada entra); importar o mesmo arquivo duas vezes (cópias).

## 7. Documentação

README: seção "Levando seus NPCs para a versão web" — exportar na versão local, importar na web, NPCs entram sem campanha, importar de novo cria cópias.
