import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Rotas que exigem login: sem token válido, vai para /login. */
export const authGuard: CanActivateFn = () =>
  inject(AuthService).getToken() !== null || inject(Router).parseUrl('/login');

/** Login e cadastro: quem já está logado vai direto para as campanhas. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).getToken() === null || inject(Router).parseUrl('/campaigns');

/** Criar e editar NPC: o visitante (somente leitura) volta para a lista. */
export const writerGuard: CanActivateFn = () =>
  !inject(AuthService).isDemo() || inject(Router).parseUrl('/npcs');
