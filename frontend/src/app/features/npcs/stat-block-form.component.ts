import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { map, merge, startWith, switchMap } from 'rxjs';
import { ABILITIES, CHALLENGE_RATINGS, CREATURE_SIZES, STAT_BLOCK_LIST_MAX } from '../../core/api/api.models';
import { fieldError } from '../../shared/field-error';
import { buildActionGroup, buildFeatureGroup, NpcForm } from './npc-form.model';

type StatBlockGroup = NpcForm['controls']['statBlock'];
type FeatureListKey = 'traits' | 'reactions';

/** Campos da ficha D&D 5e. Recebe o grupo do formulário pai por input. */
@Component({
  selector: 'app-stat-block-form',
  imports: [
    ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
  ],
  templateUrl: './stat-block-form.component.html',
  styleUrl: './stat-block-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatBlockFormComponent {
  private readonly fb = inject(NonNullableFormBuilder);

  readonly group = input.required<StatBlockGroup>();

  protected readonly sizes = CREATURE_SIZES;
  protected readonly challengeRatings = CHALLENGE_RATINGS;
  protected readonly abilities = ABILITIES;
  protected readonly listMax = STAT_BLOCK_LIST_MAX;
  protected readonly fieldError = fieldError;

  // Valor atual da ficha como signal: com OnPush e zoneless, é isso que faz a tela
  // acompanhar mudanças feitas por código (carregar um NPC, adicionar/remover itens).
  protected readonly value = toSignal(
    toObservable(this.group).pipe(
      switchMap((g) => merge(g.valueChanges, g.statusChanges).pipe(startWith(null), map(() => g.getRawValue()))),
    ),
    { requireSync: false },
  );

  // Cópias novas das listas a cada mudança, para o @for perceber itens adicionados/removidos.
  protected readonly traits = computed(() => (this.value(), [...this.group().controls.traits.controls]));
  protected readonly actions = computed(() => (this.value(), [...this.group().controls.actions.controls]));
  protected readonly reactions = computed(() => (this.value(), [...this.group().controls.reactions.controls]));

  protected modifier(score: number | null | undefined): string {
    if (typeof score !== 'number' || Number.isNaN(score)) return '';
    const mod = Math.floor((score - 10) / 2);
    return mod >= 0 ? `+${mod}` : `${mod}`;
  }

  protected addFeature(list: FeatureListKey): void {
    this.group().controls[list].push(buildFeatureGroup(this.fb));
  }

  protected addAction(): void {
    this.group().controls.actions.push(buildActionGroup(this.fb));
  }

  protected removeAt(list: FormArray, index: number): void {
    list.removeAt(index);
    list.markAsDirty();
  }
}
