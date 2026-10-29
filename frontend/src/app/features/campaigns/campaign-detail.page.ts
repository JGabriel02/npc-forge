import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { CampaignDto, NpcSummaryDto } from '../../core/api/api.models';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm.dialog';
import { ignoreNotFound } from '../../shared/ignore-not-found';
import { NpcsApi } from '../npcs/npcs.api';
import { CampaignFormDialog, CampaignFormDialogData } from './campaign-form.dialog';
import { CampaignsApi } from './campaigns.api';

type LoadState = 'loading' | 'loaded' | 'not-found' | 'error';

@Component({
  selector: 'app-campaign-detail-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatListModule, MatProgressSpinnerModule],
  templateUrl: './campaign-detail.page.html',
  styleUrl: './campaign-detail.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignDetailPage {
  private readonly campaignsApi = inject(CampaignsApi);
  private readonly npcsApi = inject(NpcsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  /** Vem do parâmetro :id da rota. */
  readonly id = input.required<string>();

  protected readonly state = signal<LoadState>('loading');
  protected readonly campaign = signal<CampaignDto | null>(null);
  protected readonly npcs = signal<NpcSummaryDto[]>([]);

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected subtitle(npc: NpcSummaryDto): string {
    const who = [npc.race, npc.occupation].filter(Boolean).join(' · ') || '—';
    return `${who} · ${npc.hasStatBlock ? `ND ${npc.challengeRating}` : 'Narrativo'}`;
  }

  protected edit(): void {
    this.dialog
      .open<CampaignFormDialog, CampaignFormDialogData, CampaignDto>(CampaignFormDialog, {
        data: { campaign: this.campaign()! },
        width: '560px',
      })
      .afterClosed()
      .subscribe((saved) => saved && this.campaign.set(saved));
  }

  protected remove(): void {
    const campaign = this.campaign()!;
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: {
          title: `Excluir "${campaign.name}"?`,
          message: 'Os NPCs desta campanha não serão excluídos; eles ficarão sem campanha.',
          confirmLabel: 'Excluir',
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.campaignsApi.delete(campaign.id).pipe(ignoreNotFound())),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl('/campaigns'),
        error: () => this.snackBar.open('Não foi possível excluir a campanha. Tente novamente.', 'Fechar', { duration: 6000 }),
      });
  }

  private load(id: string): void {
    this.state.set('loading');
    this.campaignsApi.get(id).subscribe({
      next: (campaign) => {
        this.campaign.set(campaign);
        this.state.set('loaded');
        this.npcsApi
          .list({ campaignId: id, page: 1, pageSize: 100 })
          .subscribe((page) => this.npcs.set(page.items));
      },
      error: (error: unknown) =>
        this.state.set(error instanceof HttpErrorResponse && error.status === 404 ? 'not-found' : 'error'),
    });
  }
}
