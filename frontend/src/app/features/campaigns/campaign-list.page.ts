import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { CampaignDto } from '../../core/api/api.models';
import { CampaignFormDialog, CampaignFormDialogData } from './campaign-form.dialog';
import { CampaignsApi } from './campaigns.api';

@Component({
  selector: 'app-campaign-list-page',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './campaign-list.page.html',
  styleUrl: './campaign-list.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignListPage {
  private readonly api = inject(CampaignsApi);
  private readonly dialog = inject(MatDialog);

  /** null = carregando. */
  protected readonly campaigns = signal<CampaignDto[] | null>(null);
  protected readonly loadError = signal(false);

  constructor() {
    this.load();
  }

  protected create(): void {
    this.dialog
      .open<CampaignFormDialog, CampaignFormDialogData, CampaignDto>(CampaignFormDialog, { data: {}, width: '560px' })
      .afterClosed()
      .subscribe((saved) => saved && this.load());
  }

  private load(): void {
    this.loadError.set(false);
    this.api.list().subscribe({
      next: (campaigns) => this.campaigns.set(campaigns),
      error: () => this.loadError.set(true),
    });
  }
}
