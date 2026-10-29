import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NpcSummaryDto, PagedResult } from '../../core/api/api.models';

export interface NpcQuery {
  campaignId?: string;
  search?: string;
  hasStatBlock?: boolean;
  page: number;
  pageSize: number;
}

/** Chamadas HTTP de NPCs. */
@Injectable({ providedIn: 'root' })
export class NpcsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/npcs`;

  list(query: NpcQuery): Observable<PagedResult<NpcSummaryDto>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.campaignId) params = params.set('campaignId', query.campaignId);
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    if (query.hasStatBlock !== undefined) params = params.set('hasStatBlock', query.hasStatBlock);
    return this.http.get<PagedResult<NpcSummaryDto>>(this.url, { params });
  }
}
