import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CampaignDto, CampaignRequest } from '../../core/api/api.models';

/** Chamadas HTTP de campanhas (o token é anexado pelo jwtInterceptor). */
@Injectable({ providedIn: 'root' })
export class CampaignsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/campaigns`;

  list(): Observable<CampaignDto[]> {
    return this.http.get<CampaignDto[]>(this.url);
  }

  /** Para selects de campanha: se a lista falhar, a tela segue funcionando com "Sem campanha". */
  listOrEmpty(): Observable<CampaignDto[]> {
    return this.list().pipe(catchError(() => of([])));
  }

  get(id: string): Observable<CampaignDto> {
    return this.http.get<CampaignDto>(`${this.url}/${id}`);
  }

  create(request: CampaignRequest): Observable<CampaignDto> {
    return this.http.post<CampaignDto>(this.url, request);
  }

  update(id: string, request: CampaignRequest): Observable<CampaignDto> {
    return this.http.put<CampaignDto>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
