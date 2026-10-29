import { HttpErrorResponse } from '@angular/common/http';
import { exportErrorMessage, fileNameFrom } from './download';

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

describe('exportErrorMessage', () => {
  const generic = 'Não foi possível exportar. Tente novamente.';

  it('reads the detail of a problem sent as a blob', async () => {
    const body = new Blob([JSON.stringify({ status: 400, detail: 'Esse filtro tem 501 NPCs.' })], {
      type: 'application/problem+json',
    });
    const error = new HttpErrorResponse({ status: 400, error: body });

    expect(await exportErrorMessage(error)).toBe('Esse filtro tem 501 NPCs.');
  });

  it('falls back to the generic message on other errors', async () => {
    expect(await exportErrorMessage(new HttpErrorResponse({ status: 500, error: new Blob(['oops']) }))).toBe(generic);
    expect(await exportErrorMessage(new HttpErrorResponse({ status: 0 }))).toBe(generic);
  });
});
