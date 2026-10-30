import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Funcionalidades opcionais ligadas nesta instalação (feature flags vindas da API).
 * Na dúvida (erro ao consultar), tudo fica desligado: o app funciona sem IA.
 */
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly http = inject(HttpClient);
  private readonly aiEnabled = signal(false);
  private readonly demoEnabled = signal(false);

  readonly ai = this.aiEnabled.asReadonly();
  /** A instalação tem conta de visitante (só na versão web). */
  readonly demo = this.demoEnabled.asReadonly();

  load(): Observable<void> {
    return this.http.get<{ ai: boolean; demo: boolean }>(`${environment.apiBaseUrl}/features`).pipe(
      map((features) => {
        this.aiEnabled.set(features.ai === true);
        this.demoEnabled.set(features.demo === true);
      }),
      catchError(() => of(undefined)),
    );
  }
}
