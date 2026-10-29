import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, throwError } from 'rxjs';
import { ignoreNotFound } from './ignore-not-found';

describe('ignoreNotFound', () => {
  it('treats a 404 as success (already deleted elsewhere)', async () => {
    const result = await firstValueFrom(throwError(() => new HttpErrorResponse({ status: 404 })).pipe(ignoreNotFound()));

    expect(result).toBeUndefined();
  });

  it('rethrows other errors', async () => {
    const error = new HttpErrorResponse({ status: 500 });

    await expect(firstValueFrom(throwError(() => error).pipe(ignoreNotFound()))).rejects.toBe(error);
  });
});
