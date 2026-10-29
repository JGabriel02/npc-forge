import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, finalize, switchMap } from 'rxjs';
import { CampaignDto, NpcSummaryDto, PagedResult } from '../../core/api/api.models';
import { PtBrPaginatorIntl } from '../../shared/paginator-intl';
import { CampaignsApi } from '../campaigns/campaigns.api';
import { NpcsApi } from './npcs.api';

type NpcType = 'all' | 'withStatBlock' | 'narrative';

interface Filters {
  search: string;
  campaignId: string;
  type: NpcType;
  page: number;
  pageSize: number;
}

const PAGE_SIZES = [10, 20, 50];

@Component({
  selector: 'app-npc-list-page',
  imports: [
    ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatPaginatorModule, MatProgressBarModule, MatSelectModule, MatTableModule,
  ],
  templateUrl: './npc-list.page.html',
  styleUrl: './npc-list.page.scss',
  // Registrado aqui (e não no app.config) para o paginador não entrar no carregamento inicial do app.
  providers: [{ provide: MatPaginatorIntl, useClass: PtBrPaginatorIntl }],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NpcListPage {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly npcsApi = inject(NpcsApi);

  protected readonly pageSizes = PAGE_SIZES;
  protected readonly columns = ['name', 'race', 'occupation', 'campaign', 'sheet'];

  // A URL é a fonte da verdade dos filtros: recarregar ou voltar no navegador preserva o estado.
  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });
  protected readonly filters = computed<Filters>(() => {
    const p = this.queryParams();
    return {
      search: p.get('search') ?? '',
      campaignId: p.get('campaignId') ?? '',
      type: (['withStatBlock', 'narrative'].includes(p.get('type') ?? '') ? p.get('type') : 'all') as NpcType,
      page: positiveInt(p.get('page'), 1),
      pageSize: PAGE_SIZES.includes(positiveInt(p.get('pageSize'), 20)) ? positiveInt(p.get('pageSize'), 20) : 20,
    };
  });
  protected readonly hasActiveFilters = computed(() => {
    const f = this.filters();
    return f.search !== '' || f.campaignId !== '' || f.type !== 'all';
  });

  protected readonly campaigns = toSignal(inject(CampaignsApi).list(), { initialValue: [] as CampaignDto[] });
  protected readonly result = signal<PagedResult<NpcSummaryDto> | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  protected readonly searchControl = new FormControl(this.filters().search, { nonNullable: true });

  constructor() {
    // Busca só depois de 300 ms sem digitar.
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((search) => this.updateFilters({ search: search.trim() || null, page: null }, true));

    // Toda mudança de filtro recarrega; switchMap cancela a requisição anterior se ainda estiver em andamento.
    toObservable(this.filters)
      .pipe(
        switchMap((f) => {
          this.loading.set(true);
          this.loadError.set(false);
          return this.npcsApi
            .list({
              search: f.search,
              campaignId: f.campaignId || undefined,
              hasStatBlock: f.type === 'all' ? undefined : f.type === 'withStatBlock',
              page: f.page,
              pageSize: f.pageSize,
            })
            .pipe(
              catchError(() => {
                this.loadError.set(true);
                return EMPTY;
              }),
              finalize(() => this.loading.set(false)),
            );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => this.result.set(result));
  }

  protected setCampaign(campaignId: string): void {
    this.updateFilters({ campaignId: campaignId || null, page: null });
  }

  protected setType(type: NpcType): void {
    this.updateFilters({ type: type === 'all' ? null : type, page: null });
  }

  protected changePage(event: PageEvent): void {
    this.updateFilters({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.updateFilters({ search: null, campaignId: null, type: null, page: null });
  }

  protected open(npc: NpcSummaryDto): void {
    void this.router.navigate(['/npcs', npc.id]);
  }

  protected subtitle(npc: NpcSummaryDto): string {
    return [npc.race, npc.occupation].filter(Boolean).join(' · ') || '—';
  }

  // null remove o parâmetro da URL; replaceUrl evita encher o histórico enquanto digita.
  private updateFilters(changes: Record<string, string | number | null>, replaceUrl = false): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: changes,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }
}

function positiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
