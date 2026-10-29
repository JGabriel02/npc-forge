import { NpcExportFile } from '../../core/api/api.models';
import { describeImportErrors, readExportFile } from './npc-transfer';

const npc = (name: string, withSheet = false) => ({
  name, race: null, occupation: null, appearance: null, personality: null, background: null, notes: null,
  statBlock: withSheet ? { size: 'Medium', str: 10 } : null,
});
const fileText = (overrides: Record<string, unknown> = {}) =>
  JSON.stringify({ format: 'npcforge-npcs', version: 1, exportedAt: '2026-10-29T21:30:00Z', npcs: [npc('Borin', true), npc('Hilda')], ...overrides });

describe('readExportFile', () => {
  it('rejects text that is not JSON', () => {
    expect(readExportFile('isto não é json')).toEqual({ ok: false, error: 'O arquivo não é um JSON válido.' });
  });

  it('rejects JSON that is not an NpcForge export', () => {
    expect(readExportFile('{ "foo": 1 }')).toEqual({ ok: false, error: 'Este arquivo não é uma exportação de NPCs do NpcForge.' });
    expect(readExportFile('[1, 2]')).toEqual({ ok: false, error: 'Este arquivo não é uma exportação de NPCs do NpcForge.' });
  });

  it('rejects a newer version', () => {
    expect(readExportFile(fileText({ version: 2 })))
      .toEqual({ ok: false, error: 'Este arquivo foi exportado por uma versão mais nova do NpcForge.' });
  });

  it('rejects an empty list', () => {
    expect(readExportFile(fileText({ npcs: [] }))).toEqual({ ok: false, error: 'O arquivo não tem NPCs.' });
  });

  it('returns a preview of a valid file', () => {
    const result = readExportFile(fileText());

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.preview).toEqual([
        { name: 'Borin', hasStatBlock: true },
        { name: 'Hilda', hasStatBlock: false },
      ]);
    }
  });
});

describe('describeImportErrors', () => {
  const file = JSON.parse(fileText({ npcs: [npc('Borin'), npc('Rahadin', true), npc('')] })) as NpcExportFile;

  it('names the NPC by position and name', () => {
    const messages = describeImportErrors({ errors: { 'npcs[1].statBlock.str': ['Deve ser entre 1 e 30.'] } }, file);

    expect(messages).toEqual(['NPC 2 (Rahadin): Deve ser entre 1 e 30.']);
  });

  it('uses only the position when the NPC has no name', () => {
    expect(describeImportErrors({ errors: { 'Npcs[2].Name': ['Obrigatório.'] } }, file)).toEqual(['NPC 3: Obrigatório.']);
  });

  it('does not break with an index outside the file', () => {
    expect(describeImportErrors({ errors: { 'npcs[9].name': ['Obrigatório.'] } }, file)).toEqual(['NPC 10: Obrigatório.']);
  });

  it('keeps file-level messages as they are', () => {
    expect(describeImportErrors({ errors: { format: ['Este arquivo não é uma exportação de NPCs do NpcForge.'] } }, file))
      .toEqual(['Este arquivo não é uma exportação de NPCs do NpcForge.']);
  });

  it('falls back to the title when there are no field errors', () => {
    expect(describeImportErrors({ title: 'Erro qualquer.' }, file)).toEqual(['Erro qualquer.']);
  });
});
