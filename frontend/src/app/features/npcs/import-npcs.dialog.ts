import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { errorMessages, problemFrom } from '../../core/api/problem-details';
import { describeImportErrors, readExportFile, ReadResult } from './npc-transfer';
import { NpcsApi } from './npcs.api';
import { EXPORT_MAX_BYTES } from '../../core/api/api.models';

const TOO_LARGE = `O arquivo é grande demais (máximo de ${EXPORT_MAX_BYTES / 1024 / 1024} MB).`;

/** Importa um arquivo de exportação. Fecha devolvendo quantos NPCs foram importados. */
@Component({
  selector: 'app-import-npcs-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './import-npcs.dialog.html',
  styleUrl: './import-npcs.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportNpcsDialog {
  private readonly api = inject(NpcsApi);
  private readonly dialogRef = inject<MatDialogRef<ImportNpcsDialog, number>>(MatDialogRef);

  protected readonly fileName = signal('');
  protected readonly read = signal<ReadResult | null>(null);
  protected readonly importing = signal(false);
  protected readonly errors = signal<string[]>([]);

  protected readonly preview = computed(() => {
    const result = this.read();
    return result?.ok ? result.preview : [];
  });
  protected readonly readError = computed(() => {
    const result = this.read();
    return result && !result.ok ? result.error : null;
  });

  /** Lê o arquivo no navegador e monta a prévia, sem chamar a API. */
  protected async onFileChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permite escolher o mesmo arquivo de novo depois de corrigi-lo
    if (!file) return;

    this.errors.set([]);
    this.fileName.set(file.name);
    // Um arquivo enorme travaria a aba só para ser lido; a API recusaria de qualquer forma.
    if (file.size > EXPORT_MAX_BYTES) {
      this.read.set({ ok: false, error: TOO_LARGE });
      return;
    }

    const chosen = ++this.picks;
    const text = await file.text();
    if (chosen === this.picks) this.read.set(readExportFile(text)); // ignora a leitura de um arquivo já trocado
  }

  private picks = 0;

  protected import(): void {
    const result = this.read();
    if (!result?.ok) return;

    this.importing.set(true);
    this.errors.set([]);
    this.dialogRef.disableClose = true;

    this.api.importFile(result.file).subscribe({
      next: (response) => this.dialogRef.close(response.imported),
      error: (error: unknown) => {
        this.errors.set(this.describe(error, result));
        this.importing.set(false);
        this.dialogRef.disableClose = false;
      },
    });
  }

  private describe(error: unknown, result: Extract<ReadResult, { ok: true }>): string[] {
    if (error instanceof HttpErrorResponse && error.status === 413) {
      return [TOO_LARGE];
    }
    const problem = problemFrom(error);
    if (error instanceof HttpErrorResponse && error.status === 400 && problem) {
      return describeImportErrors(problem, result.file);
    }
    return errorMessages(new FormGroup({}), error);
  }
}
