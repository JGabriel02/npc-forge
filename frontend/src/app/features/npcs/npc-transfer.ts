import { EXPORT_FORMAT, EXPORT_VERSION, NpcExportFile, ProblemDetails } from '../../core/api/api.models';

export interface ImportPreviewItem {
  name: string;
  hasStatBlock: boolean;
}

export type ReadResult =
  | { ok: true; file: NpcExportFile; preview: ImportPreviewItem[] }
  | { ok: false; error: string };

// Mesmas mensagens da API, para o usuário ver o mesmo texto antes e depois de enviar.
const NOT_AN_EXPORT = 'Este arquivo não é uma exportação de NPCs do NpcForge.';
const NEWER_VERSION = 'Este arquivo foi exportado por uma versão mais nova do NpcForge.';
const NO_NPCS = 'O arquivo não tem NPCs.';

/** Lê o arquivo escolhido no navegador e prepara a prévia, sem chamar a API. */
export function readExportFile(text: string): ReadResult {
  let data: unknown;
  try {
    data = JSON.parse(text);
  } catch {
    return { ok: false, error: 'O arquivo não é um JSON válido.' };
  }

  if (typeof data !== 'object' || data === null || Array.isArray(data)) return { ok: false, error: NOT_AN_EXPORT };
  const file = data as Partial<NpcExportFile>;
  if (file.format !== EXPORT_FORMAT) return { ok: false, error: NOT_AN_EXPORT };
  if (typeof file.version !== 'number' || file.version < 1 || file.version > EXPORT_VERSION) {
    return { ok: false, error: NEWER_VERSION };
  }
  if (!Array.isArray(file.npcs) || file.npcs.length === 0) return { ok: false, error: NO_NPCS };

  return {
    ok: true,
    file: file as NpcExportFile,
    preview: file.npcs.map((npc) => ({
      name: typeof npc?.name === 'string' && npc.name.trim() ? npc.name : '(sem nome)',
      hasStatBlock: npc?.statBlock != null,
    })),
  };
}

/** "npcs[1].statBlock.str" → "NPC 2 (Rahadin): Deve ser entre 1 e 30." */
export function describeImportErrors(problem: ProblemDetails, file: NpcExportFile): string[] {
  const errors = problem.errors ?? {};
  if (Object.keys(errors).length === 0) return problem.title ? [problem.title] : [];

  return Object.entries(errors).flatMap(([key, messages]) => {
    const match = /^npcs\[(\d+)\]/i.exec(key);
    if (!match) return messages;

    const index = Number(match[1]);
    const name = file.npcs?.[index]?.name?.trim();
    const label = name ? `NPC ${index + 1} (${name})` : `NPC ${index + 1}`;
    return messages.map((message) => `${label}: ${message}`);
  });
}
