import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, switchMap, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginResponse, MeResponse } from '../api/api.models';

const TOKEN_KEY = 'npcforge.token';
const EXPIRES_KEY = 'npcforge.expiresAt';

interface Session {
  token: string;
  expiresAt: string;
}

/** Sessão do usuário: guarda o JWT no localStorage e expõe "quem está logado" como signals. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = environment.apiBaseUrl;

  private readonly session = signal<Session | null>(readStoredSession());
  private readonly currentUser = signal<MeResponse | null>(null);

  /** Usuário logado (null enquanto não carregou ou se deslogado). */
  readonly user = this.currentUser.asReadonly();

  /** true só se houver token ainda não expirado. */
  readonly isLoggedIn = computed(() => isValid(this.session()));

  /** Token para mandar à API, ou null se ausente/expirado. */
  getToken(): string | null {
    const session = this.session();
    return isValid(session) ? session!.token : null;
  }

  login(email: string, password: string): Observable<void> {
    return this.http.post<LoginResponse>(`${this.api}/auth/login`, { email, password }).pipe(
      tap((response) => this.saveSession(response)),
      switchMap(() => this.loadUser()),
      map(() => undefined),
    );
  }

  register(email: string, password: string, displayName: string): Observable<void> {
    return this.http
      .post<MeResponse>(`${this.api}/auth/register`, { email, password, displayName })
      .pipe(switchMap(() => this.login(email, password)));
  }

  loadUser(): Observable<MeResponse> {
    return this.http.get<MeResponse>(`${this.api}/auth/me`).pipe(tap((me) => this.currentUser.set(me)));
  }

  /**
   * "Sair" pelo usuário: navega primeiro e só encerra a sessão se a navegação acontecer.
   * Se ele cancelar ("Descartar as alterações?"), continua logado na página onde estava.
   */
  async logout(): Promise<void> {
    if (await this.router.navigateByUrl('/login')) {
      this.clearSession();
    }
  }

  /** Sessão inválida (401 da API): encerra na hora, mesmo que a navegação seja interrompida. */
  expireSession(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  private clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRES_KEY);
    this.session.set(null);
    this.currentUser.set(null);
  }

  private saveSession({ token, expiresAt }: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(EXPIRES_KEY, expiresAt);
    this.session.set({ token, expiresAt });
  }
}

function readStoredSession(): Session | null {
  const token = localStorage.getItem(TOKEN_KEY);
  const expiresAt = localStorage.getItem(EXPIRES_KEY);
  return token && expiresAt ? { token, expiresAt } : null;
}

function isValid(session: Session | null): boolean {
  return session !== null && new Date(session.expiresAt).getTime() > Date.now();
}
