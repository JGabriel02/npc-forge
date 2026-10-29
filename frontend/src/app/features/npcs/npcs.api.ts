import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateNpcRequest, NpcDto, NpcSummaryDto, PagedResult, StatBlockRequest, UpdateNpcRequest,
} from '../../core/api/api.models';

export interface NpcQuery {
  /** Id da campanha, ou 'none' para NPCs sem campanha. */
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

  get(id: string): Observable<NpcDto> {
    return this.http.get<NpcDto>(`${this.url}/${id}`);
  }

  create(request: CreateNpcRequest): Observable<NpcDto> {
    return this.http.post<NpcDto>(this.url, request);
  }

  update(id: string, request: UpdateNpcRequest): Observable<NpcDto> {
    return this.http.put<NpcDto>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  setStatBlock(id: string, request: StatBlockRequest): Observable<NpcDto> {
    return this.http.put<NpcDto>(`${this.url}/${id}/statblock`, request);
  }

  removeStatBlock(id: string): Observable<NpcDto> {
    return this.http.delete<NpcDto>(`${this.url}/${id}/statblock`);
  }
}
