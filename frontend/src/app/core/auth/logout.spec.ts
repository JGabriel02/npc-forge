import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, Routes } from '@angular/router';
import { routes as appRoutes } from '../../app.routes';
import { AuthService } from './auth.service';
import { guestGuard, logoutGuard } from './auth.guards';

@Component({ template: '' })
class Blank {}

const future = () => new Date(Date.now() + 60 * 60 * 1000).toISOString();

// Com o roteador de verdade: o bug só aparecia porque o guestGuard de /login mandava quem ainda
// tinha token de volta para /campaigns. Os testes antigos simulavam o navigateByUrl e não viam isso.
describe('Sair (logout)', () => {
  let leaveAllowed = true;

  async function loggedInAt(url: string) {
    localStorage.setItem('npcforge.token', 'jwt-123');
    localStorage.setItem('npcforge.expiresAt', future());
    const routes: Routes = [
      { path: 'login', canActivate: [guestGuard], component: Blank },
      { path: 'logout', canActivate: [logoutGuard], component: Blank },
      { path: 'campaigns', component: Blank },
      { path: 'npcs/new', component: Blank, canDeactivate: [() => leaveAllowed] },
    ];
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    const router = TestBed.inject(Router);
    await router.navigateByUrl(url);
    return { router, auth: TestBed.inject(AuthService) };
  }

  beforeEach(() => {
    localStorage.clear();
    leaveAllowed = true;
  });

  it('ends the session and lands on the login page', async () => {
    const { router, auth } = await loggedInAt('/campaigns');

    await auth.logout();

    expect(router.url).toBe('/login');
    expect(auth.isLoggedIn()).toBe(false);
    expect(localStorage.getItem('npcforge.token')).toBeNull();
  });

  it('keeps the session when the user cancels leaving a page with unsaved changes', async () => {
    const { router, auth } = await loggedInAt('/npcs/new');
    leaveAllowed = false;

    await auth.logout();

    expect(router.url).toBe('/npcs/new');
    expect(auth.isLoggedIn()).toBe(true);
  });

  it('the app has the /logout route with the logout guard', () => {
    const logout = appRoutes.find((r) => r.path === 'logout');

    expect(logout).toBeDefined();
    expect(logout!.canActivate).toEqual([logoutGuard]);
  });
});
