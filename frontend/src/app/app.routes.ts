import { Component } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';

// Provisório: substituído pela lista de campanhas na Task 3.
@Component({ template: '<p style="padding:24px">Campanhas (em construção)</p>' })
class CampaignsPlaceholder {}

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'campaigns' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  { path: 'campaigns', canActivate: [authGuard], component: CampaignsPlaceholder },
  { path: '**', redirectTo: 'campaigns' },
];
