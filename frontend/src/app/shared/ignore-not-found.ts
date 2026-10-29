import { HttpErrorResponse } from '@angular/common/http';
import { MonoTypeOperatorFunction, Observable, catchError, of, throwError } from 'rxjs';

/** Para exclusões: um 404 significa "já foi excluído" (ex.: em outra aba), então conta como sucesso. */
export function ignoreNotFound<T>(): MonoTypeOperatorFunction<T | undefined> {
  return (source: Observable<T | undefined>) =>
    source.pipe(
      catchError((error: unknown) =>
        error instanceof HttpErrorResponse && error.status === 404 ? of(undefined) : throwError(() => error),
      ),
    );
}
