import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ABILITIES, StatBlockDto } from '../../core/api/api.models';
import { formatAbility, formatAttackBonus, formatModifier, sizeLabel } from '../../shared/dnd-format';

/** Ficha no layout clássico de stat block de D&D 5e. */
@Component({
  selector: 'app-stat-block-view',
  templateUrl: './stat-block-view.component.html',
  styleUrl: './stat-block-view.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatBlockViewComponent {
  readonly name = input.required<string>();
  readonly statBlock = input.required<StatBlockDto>();

  protected readonly formatAttackBonus = formatAttackBonus;

  protected readonly typeLine = computed(() => {
    const s = this.statBlock();
    return `${sizeLabel(s.size)}, ${s.creatureType}, ${s.alignment}`;
  });

  protected readonly abilities = computed(() =>
    ABILITIES.map((a) => ({
      short: a.short,
      label: a.label,
      value: formatAbility(this.statBlock()[a.key], this.statBlock().modifiers[a.key]),
    })),
  );

  protected readonly details = computed(() => {
    const s = this.statBlock();
    return [
      { label: 'Salvaguardas', value: s.savingThrows },
      { label: 'Perícias', value: s.skills },
      { label: 'Sentidos', value: s.senses },
      { label: 'Idiomas', value: s.languages },
    ].filter((d) => !!d.value);
  });

  protected readonly challenge = computed(() => {
    const s = this.statBlock();
    return `${s.challengeRating} (Bônus de Proficiência ${formatModifier(s.proficiencyBonus)})`;
  });
}
