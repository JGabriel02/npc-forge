import { HttpInterceptorFn } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { defer, finalize, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';

const SLOW_AFTER_MS = 4_000;
const GIVE_UP_AFTER_MS = 90_000;

/**
 * Quantas requisições à API estão demorando. O componente raiz mostra o aviso enquanto houver alguma.
 * (Um serviço simples, sem MatSnackBar: o snackbar puxaria ~160 KB para o carregamento inicial.)
 */
@Injectable({ providedIn: 'root' })
export class SlowServerNotice {
  private readonly slowRequests = signal(0);

  readonly visible = computed(() => this.slowRequests() > 0);

  start(): void {
    this.slowRequests.update((n) => n + 1);
  }

  stop(): void {
    this.slowRequests.update((n) => Math.max(0, n - 1));
  }
}

/**
 * No plano gratuito da Azure a API "dorme" quando fica parada, e a primeira chamada pode levar até
 * um minuto. Se uma requisição à API passar de 4 s, avisa o usuário (em vez de parecer travado);
 * depois de 90 s, desiste.
 */
export const slowServerInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const notice = inject(SlowServerNotice);
  return defer(() => {
    let slow = false;
    const timer = setTimeout(() => {
      slow = true;
      notice.start();
    }, SLOW_AFTER_MS);

    return next(req).pipe(
      timeout(GIVE_UP_AFTER_MS),
      finalize(() => {
        clearTimeout(timer);
        if (slow) notice.stop();
      }),
    );
  });
};
