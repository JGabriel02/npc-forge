import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { catchError, of } from 'rxjs';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { jwtInterceptor } from './core/auth/jwt.interceptor';

// Configuração global: tudo que a aplicação inteira pode injetar (HTTP, roteador...).
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: parâmetros da rota (ex.: :id) chegam no componente como input().
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([jwtInterceptor])),
    // Ao abrir o app com um token salvo, carrega o usuário; se a API recusar, encerra a sessão.
    // Também faz o <mat-icon> usar a fonte Material Symbols (carregada no index.html).
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined');
      const auth = inject(AuthService);
      if (!auth.getToken()) return;
      return auth.loadUser().pipe(catchError(() => of(auth.logout())));
    }),
  ],
};
