import { fileNameFrom } from './download';

describe('fileNameFrom', () => {
  it('reads a quoted filename', () => {
    expect(fileNameFrom('attachment; filename="npc-rahadin.json"', 'x.json')).toBe('npc-rahadin.json');
  });

  it('reads an unquoted filename', () => {
    expect(fileNameFrom('attachment; filename=npcs-2026-10-29.json', 'x.json')).toBe('npcs-2026-10-29.json');
  });

  it('prefers the encoded filename*', () => {
    expect(fileNameFrom("attachment; filename=x.json; filename*=UTF-8''npcs-2026-10-29.json", 'y.json'))
      .toBe('npcs-2026-10-29.json');
  });

  it('falls back when there is no header', () => {
    expect(fileNameFrom(null, 'npcs.json')).toBe('npcs.json');
  });
});
