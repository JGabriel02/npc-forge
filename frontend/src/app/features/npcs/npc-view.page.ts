import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { filter, finalize, switchMap } from 'rxjs';
import { CampaignDto, NpcDto } from '../../core/api/api.models';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm.dialog';
import { exportErrorMessage, saveResponse } from '../../shared/download';
import { ignoreNotFound } from '../../shared/ignore-not-found';
import { CampaignsApi } from '../campaigns/campaigns.api';
import { NpcsApi } from './npcs.api';
import { StatBlockViewComponent } from './stat-block-view.component';

type LoadState = 'loading' | 'loaded' | 'not-found' | 'error';

@Component({
  selector: 'app-npc-view-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule, StatBlockViewComponent],
  templateUrl: './npc-view.page.html',
  styleUrl: './npc-view.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NpcViewPage {
  private readonly npcsApi = inject(NpcsApi);
  private readonly campaignsApi = inject(CampaignsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly id = input.required<string>();

  protected readonly state = signal<LoadState>('loading');
  protected readonly npc = signal<NpcDto | null>(null);
  protected readonly campaign = signal<CampaignDto | null>(null);

  protected readonly subtitle = computed(() => {
    const npc = this.npc();
    return npc ? [npc.race, npc.occupation].filter(Boolean).join(' · ') : '';
  });

  protected readonly sections = computed(() => {
    const npc = this.npc();
    if (!npc) return [];
    return [
      { title: 'Aparência', text: npc.appearance },
      { title: 'Personalidade', text: npc.personality },
      { title: 'Histórico', text: npc.background },
    ].filter((s) => !!s.text);
  });

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected readonly exporting = signal(false);

  protected exportNpc(): void {
    this.exporting.set(true); // evita baixar duas vezes num clique duplo
    this.npcsApi
      .exportOne(this.npc()!.id)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (response) => saveResponse(response, 'npc.json'),
        error: async (error: unknown) => this.snackBar.open(await exportErrorMessage(error), 'Fechar', { duration: 6000 }),
      });
  }

  protected remove(): void {
    const npc = this.npc()!;
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: { title: `Excluir "${npc.name}"?`, message: 'Esta ação não pode ser desfeita.', confirmLabel: 'Excluir' },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.npcsApi.delete(npc.id).pipe(ignoreNotFound())),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl('/npcs'),
        error: () => this.snackBar.open('Não foi possível excluir o NPC. Tente novamente.', 'Fechar', { duration: 6000 }),
      });
  }

  private load(id: string): void {
    this.state.set('loading');
    this.campaign.set(null);
    this.npcsApi.get(id).subscribe({
      next: (npc) => {
        this.npc.set(npc);
        this.state.set('loaded');
        if (npc.campaignId) {
          this.campaignsApi.get(npc.campaignId).subscribe((campaign) => this.campaign.set(campaign));
        }
      },
      error: (error: unknown) =>
        this.state.set(error instanceof HttpErrorResponse && error.status === 404 ? 'not-found' : 'error'),
    });
  }
}
