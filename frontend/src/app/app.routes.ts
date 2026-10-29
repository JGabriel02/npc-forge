import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';

// loadComponent carrega o código de cada tela só quando ela é aberta (lazy loading).
export const routes: Routes = [
  {
    path: 'login',
    title: 'Entrar · NpcForge',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: 'Criar conta · NpcForge',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  {
    // Telas logadas: todas dentro do shell (barra de navegação).
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'campaigns' },
      {
        path: 'campaigns',
        title: 'Campanhas · NpcForge',
        loadComponent: () => import('./features/campaigns/campaign-list.page').then((m) => m.CampaignListPage),
      },
      {
        path: 'campaigns/:id',
        title: 'Campanha · NpcForge',
        loadComponent: () => import('./features/campaigns/campaign-detail.page').then((m) => m.CampaignDetailPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
