import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';

// Configuração global: tudo que a aplicação inteira pode injetar (HTTP, roteador...).
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: parâmetros da rota (ex.: :id) chegam no componente como input().
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([])),
  ],
};
