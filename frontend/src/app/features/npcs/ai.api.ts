import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiUsageDto, GenerateNpcRequest, GenerateNpcResponse } from '../../core/api/api.models';

/** Chamadas da IA. Só usadas quando FeaturesService.ai() é true. */
@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/ai`;

  generateNpc(request: GenerateNpcRequest): Observable<GenerateNpcResponse> {
    return this.http.post<GenerateNpcResponse>(`${this.url}/generate-npc`, request);
  }

  usage(): Observable<AiUsageDto> {
    return this.http.get<AiUsageDto>(`${this.url}/usage`);
  }
}
