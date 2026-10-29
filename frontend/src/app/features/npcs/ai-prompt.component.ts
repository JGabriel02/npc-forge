import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subscription, catchError, of } from 'rxjs';
import { AiUsageDto, CHALLENGE_RATINGS, NpcDraft } from '../../core/api/api.models';
import { errorMessages } from '../../core/api/problem-details';
import { fieldError } from '../../shared/field-error';
import { AiApi } from './ai.api';

export interface GeneratedDraft {
  draft: NpcDraft;
  warnings: string[];
}

/** Card "Gerar com IA" da tela de criação: descreve o NPC, a IA devolve um rascunho para o formulário. */
@Component({
  selector: 'app-ai-prompt',
  imports: [
    ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSlideToggleModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
  ],
  templateUrl: './ai-prompt.component.html',
  styleUrl: './ai-prompt.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiPromptComponent {
  private readonly ai = inject(AiApi);

  /** Campanha escolhida no formulário ('' = sem campanha): o cenário dela vai para a IA. */
  readonly campaignId = input('');
  /** Espelha o toggle "Incluir ficha" do formulário. */
  readonly withStatBlock = input(false);

  readonly withStatBlockChange = output<boolean>();
  readonly generated = output<GeneratedDraft>();

  protected readonly challengeRatings = CHALLENGE_RATINGS;
  protected readonly fieldError = fieldError;
  protected readonly form = new FormGroup({
    prompt: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    challengeRating: new FormControl('', { nonNullable: true }), // '' = a IA escolhe
  });

  protected readonly usage = signal<AiUsageDto | null>(null);
  protected readonly generating = signal(false);
  protected readonly messages = signal<string[]>([]);

  protected readonly exhausted = computed(() => {
    const u = this.usage();
    return u !== null && u.used >= u.limit;
  });
  protected readonly resetsAtLabel = computed(() => {
    const u = this.usage();
    return u ? new Date(u.resetsAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '';
  });

  private request?: Subscription;

  constructor() {
    this.refreshUsage();
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  protected generate(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { prompt, challengeRating } = this.form.getRawValue();
    this.generating.set(true);
    this.messages.set([]);

    this.request = this.ai
      .generateNpc({
        prompt: prompt.trim(),
        campaignId: this.campaignId() || null,
        withStatBlock: this.withStatBlock(),
        challengeRating: this.withStatBlock() && challengeRating ? challengeRating : null,
      })
      .subscribe({
        next: (response) => {
          this.usage.set(response.usage);
          this.generating.set(false);
          this.generated.emit({ draft: response.draft, warnings: response.warnings });
        },
        error: (error: unknown) => {
          this.messages.set(errorMessages(this.form, error));
          this.generating.set(false);
          if (error instanceof HttpErrorResponse && error.status === 429) this.refreshUsage();
        },
      });
  }

  /** Cancelar a assinatura aborta a requisição; o servidor devolve a geração do dia. */
  protected cancel(): void {
    this.request?.unsubscribe();
    this.generating.set(false);
  }

  private refreshUsage(): void {
    this.ai.usage().pipe(catchError(() => of(null))).subscribe((usage) => this.usage.set(usage));
  }
}
