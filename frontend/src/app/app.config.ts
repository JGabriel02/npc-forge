import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { forkJoin } from 'rxjs';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { FeaturesService } from './core/features/features.service';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { slowServerInterceptor } from './core/api/slow-server.interceptor';

// Configuração global: tudo que a aplicação inteira pode injetar (HTTP, roteador...).
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: parâmetros da rota (ex.: :id) chegam no componente como input().
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([jwtInterceptor, slowServerInterceptor])),
    // Ao abrir o app: descobre se a IA está ligada e, com um token salvo, carrega o usuário
    // (só um 401 encerra a sessão; servidor acordando não desloga). Também faz o <mat-icon> usar a fonte Material Symbols.
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined');
      return forkJoin([inject(FeaturesService).load(), inject(AuthService).restoreSession()]);
    }),
  ],
};
