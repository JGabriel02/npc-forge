import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
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
