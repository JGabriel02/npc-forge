import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

const api = environment.apiBaseUrl;
const future = () => new Date(Date.now() + 60 * 60 * 1000).toISOString();
const past = () => new Date(Date.now() - 60 * 1000).toISOString();
const me = { id: 'u1', email: 'mestre@x.com', displayName: 'Mestre' };

describe('AuthService', () => {
  let http: HttpTestingController;

  function setup(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('login stores token and expiresAt and loads the user', () => {
    const auth = setup();
    const expiresAt = future();

    auth.login('mestre@x.com', 'senha1234').subscribe();
    const login = http.expectOne(`${api}/auth/login`);
    expect(login.request.body).toEqual({ email: 'mestre@x.com', password: 'senha1234' });
    login.flush({ token: 'jwt-123', expiresAt });
    http.expectOne(`${api}/auth/me`).flush(me);

    expect(localStorage.getItem('npcforge.token')).toBe('jwt-123');
    expect(localStorage.getItem('npcforge.expiresAt')).toBe(expiresAt);
    expect(auth.user()).toEqual(me);
    expect(auth.isLoggedIn()).toBe(true);
    expect(auth.getToken()).toBe('jwt-123');
  });

  it('getToken returns null when the token is expired', () => {
    localStorage.setItem('npcforge.token', 'velho');
    localStorage.setItem('npcforge.expiresAt', past());

    const auth = setup();

    expect(auth.getToken()).toBeNull();
    expect(auth.isLoggedIn()).toBe(false);
  });

  it('register posts then logs in', () => {
    const auth = setup();

    auth.register('mestre@x.com', 'senha1234', 'Mestre').subscribe();
    const register = http.expectOne(`${api}/auth/register`);
    expect(register.request.body).toEqual({ email: 'mestre@x.com', password: 'senha1234', displayName: 'Mestre' });
    register.flush(me, { status: 201, statusText: 'Created' });
    http.expectOne(`${api}/auth/login`).flush({ token: 'jwt-1', expiresAt: future() });
    http.expectOne(`${api}/auth/me`).flush(me);

    expect(auth.isLoggedIn()).toBe(true);
  });

  it('logout navigates to /login and then clears storage and user', async () => {
    localStorage.setItem('npcforge.token', 'jwt-123');
    localStorage.setItem('npcforge.expiresAt', future());
    const auth = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    await auth.logout();

    expect(navigate).toHaveBeenCalledWith('/login');
    expect(localStorage.getItem('npcforge.token')).toBeNull();
    expect(localStorage.getItem('npcforge.expiresAt')).toBeNull();
    expect(auth.user()).toBeNull();
    expect(auth.isLoggedIn()).toBe(false);
  });

  it('logout keeps the session when the user cancels leaving a page with unsaved changes', async () => {
    localStorage.setItem('npcforge.token', 'jwt-123');
    localStorage.setItem('npcforge.expiresAt', future());
    const auth = setup();
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(false);

    await auth.logout();

    expect(localStorage.getItem('npcforge.token')).toBe('jwt-123');
    expect(auth.isLoggedIn()).toBe(true);
  });

  it('expireSession clears the session even before navigating', () => {
    localStorage.setItem('npcforge.token', 'jwt-123');
    localStorage.setItem('npcforge.expiresAt', future());
    const auth = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(false);

    auth.expireSession();

    expect(localStorage.getItem('npcforge.token')).toBeNull();
    expect(auth.isLoggedIn()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('loginAsDemo posts to /auth/demo, loads the user and marks the session as visitor', () => {
    const auth = setup();

    auth.loginAsDemo().subscribe();
    const demo = http.expectOne(`${api}/auth/demo`);
    expect(demo.request.method).toBe('POST');
    demo.flush({ token: 'jwt-demo', expiresAt: future() });
    http.expectOne(`${api}/auth/me`).flush({ ...me, email: 'visitante@npcforge.demo', isDemo: true });

    expect(auth.getToken()).toBe('jwt-demo');
    expect(auth.isDemo()).toBe(true);
  });

  it('isDemo is false for a regular user', () => {
    const auth = setup();

    auth.login('mestre@x.com', 'senha1234').subscribe();
    http.expectOne(`${api}/auth/login`).flush({ token: 'jwt-123', expiresAt: future() });
    http.expectOne(`${api}/auth/me`).flush({ ...me, isDemo: false });

    expect(auth.isDemo()).toBe(false);
  });

  it('leaveDemoToRegister ends the visitor session and opens the register page', async () => {
    localStorage.setItem('npcforge.token', 'jwt-demo');
    localStorage.setItem('npcforge.expiresAt', future());
    const auth = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    await auth.leaveDemoToRegister();

    expect(auth.isLoggedIn()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/register');
  });
});
