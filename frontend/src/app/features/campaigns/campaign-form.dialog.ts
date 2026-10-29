import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CampaignDto, CampaignRequest } from '../../core/api/api.models';
import { errorMessages } from '../../core/api/problem-details';
import { fieldError } from '../../shared/field-error';
import { CampaignsApi } from './campaigns.api';

export interface CampaignFormDialogData {
  campaign?: CampaignDto;
}

/** Criar ou editar uma campanha. Fecha devolvendo a campanha salva (ou nada, se cancelar). */
@Component({
  selector: 'app-campaign-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './campaign-form.dialog.html',
  styles: `
    mat-dialog-content { display: flex; flex-direction: column; gap: 4px; padding-top: 8px; }
    .messages { margin: 0 0 8px; color: var(--mat-sys-error); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignFormDialog {
  private readonly api = inject(CampaignsApi);
  private readonly dialogRef = inject<MatDialogRef<CampaignFormDialog, CampaignDto>>(MatDialogRef);
  private readonly data = inject<CampaignFormDialogData>(MAT_DIALOG_DATA);

  protected readonly editing = !!this.data.campaign;
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.campaign?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data.campaign?.description ?? '', Validators.maxLength(2000)],
    setting: [this.data.campaign?.setting ?? '', Validators.maxLength(2000)],
  });
  protected readonly saving = signal(false);
  protected readonly messages = signal<string[]>([]);
  protected readonly fieldError = fieldError;

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: CampaignRequest = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      setting: value.setting.trim() || null,
    };
    const campaign = this.data.campaign;
    const save$ = campaign ? this.api.update(campaign.id, request) : this.api.create(request);

    this.saving.set(true);
    save$.subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error: unknown) => {
        this.messages.set(errorMessages(this.form, error));
        this.saving.set(false);
      },
    });
  }
}
