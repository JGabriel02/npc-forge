import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CanDeactivateFn, Router, RouterLink } from '@angular/router';
import { Observable, catchError, concatMap, of, throwError } from 'rxjs';
import { CampaignDto, NpcDto } from '../../core/api/api.models';
import { applyServerErrors, problemFrom } from '../../core/api/problem-details';
import { fieldError } from '../../shared/field-error';
import { CampaignsApi } from '../campaigns/campaigns.api';
import {
  buildNpcForm, loadNpcForm, planSave, SaveStep, toCreateRequest, toFormValue, toStatBlockRequest, toUpdateRequest,
} from './npc-form.model';
import { NpcsApi } from './npcs.api';
import { StatBlockFormComponent } from './stat-block-form.component';

type PageState = 'loading' | 'ready' | 'not-found' | 'error';

/** Erro de um passo do salvamento + o pedaço do formulário onde mostrar as mensagens. */
interface StepError {
  error: unknown;
  target: AbstractControl;
}

@Component({
  selector: 'app-npc-form-page',
  imports: [
    ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, MatSlideToggleModule, StatBlockFormComponent,
  ],
  templateUrl: './npc-form.page.html',
  styleUrl: './npc-form.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NpcFormPage {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly npcsApi = inject(NpcsApi);
  private readonly router = inject(Router);

  /** Parâmetro :id (só na edição). */
  readonly id = input<string>();
  /** Query param ?campaignId= (pré-seleciona a campanha ao criar). */
  readonly campaignId = input<string>();
  /** Query param ?withStatBlock=1 (vem do botão "Adicionar ficha"): já abre com a ficha ligada. */
  readonly withStatBlock = input<string>();

  protected readonly form = buildNpcForm(this.fb);
  protected readonly fieldError = fieldError;
  protected readonly campaigns = toSignal(inject(CampaignsApi).list(), { initialValue: [] as CampaignDto[] });

  protected readonly state = signal<PageState>('loading');
  protected readonly original = signal<NpcDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly messages = signal<string[]>([]);
  private saved = false;

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) {
        this.load(id);
      } else {
        this.form.controls.campaignId.setValue(this.campaignId() ?? '');
        this.form.markAsPristine();
        this.state.set('ready');
      }
    });
  }

  /** Usado pelo canDeactivate: pede confirmação se houver alterações não salvas. */
  canLeave(): boolean {
    return this.saved || !this.form.dirty || confirm('Descartar as alterações não salvas?');
  }

  protected cancelLink(): string[] {
    const id = this.id();
    return id ? ['/npcs', id] : ['/npcs'];
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.messages.set(['Corrija os campos destacados.']);
      window.scrollTo({ top: 0, behavior: 'smooth' });
      return;
    }

    const value = this.form.getRawValue();
    const original = this.original();
    const steps = planSave(original, value);

    const runStep = (step: SaveStep, npcId: string | undefined): Observable<NpcDto> => {
      const request$: Observable<NpcDto> = {
        create: () => this.npcsApi.create(toCreateRequest(value)),
        update: () => this.npcsApi.update(npcId!, toUpdateRequest(value)),
        setStatBlock: () => this.npcsApi.setStatBlock(npcId!, toStatBlockRequest(value.statBlock)),
        removeStatBlock: () => this.npcsApi.removeStatBlock(npcId!),
      }[step.kind]();
      // Erros do PUT /statblock vêm com chaves da ficha ("Str"), então vão para o grupo da ficha.
      const target = step.kind === 'setStatBlock' ? this.form.controls.statBlock : this.form;
      return request$.pipe(catchError((error: unknown) => throwError(() => ({ error, target }) satisfies StepError)));
    };

    this.saving.set(true);
    this.messages.set([]);
    steps
      .reduce<Observable<NpcDto | null>>(
        (chain$, step) => chain$.pipe(concatMap((npc) => runStep(step, npc?.id ?? original?.id))),
        of(original),
      )
      .subscribe({
        next: (npc) => {
          this.saved = true;
          void this.router.navigate(['/npcs', npc!.id]);
        },
        error: ({ error, target }: StepError) => {
          const problem = problemFrom(error);
          this.messages.set(problem ? applyServerErrors(target, problem) : ['Não foi possível salvar.']);
          if (problem?.errors && this.messages().length === 0) this.messages.set(['Corrija os campos destacados.']);
          this.saving.set(false);
          // O botão Salvar fica no rodapé; sobe para o usuário ver as mensagens.
          window.scrollTo({ top: 0, behavior: 'smooth' });
        },
      });
  }

  private load(id: string): void {
    this.state.set('loading');
    this.npcsApi.get(id).subscribe({
      next: (npc) => {
        this.original.set(npc);
        loadNpcForm(this.fb, this.form, toFormValue(npc));
        this.form.markAsPristine();
        if (this.withStatBlock() === '1' && !npc.statBlock) {
          this.form.controls.includeStatBlock.setValue(true);
          this.form.controls.includeStatBlock.markAsDirty();
        }
        this.state.set('ready');
      },
      error: (error: unknown) =>
        this.state.set(error instanceof HttpErrorResponse && error.status === 404 ? 'not-found' : 'error'),
    });
  }
}

export const confirmLeaveNpcForm: CanDeactivateFn<NpcFormPage> = (page) => page.canLeave();
