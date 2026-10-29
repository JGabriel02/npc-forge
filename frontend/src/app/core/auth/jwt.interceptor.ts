import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

// Nessas rotas um 401 significa "credenciais erradas", não "sessão expirada".
const AUTH_ENDPOINTS = ['/auth/login', '/auth/register'];

/**
 * Roda em toda requisição HTTP: anexa o JWT nas chamadas à nossa API
 * e, se a API responder 401 (token inválido/expirado), encerra a sessão.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const token = auth.getToken();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.endsWith(path));

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthEndpoint) {
        auth.logout();
      }
      return throwError(() => error);
    }),
  );
};
