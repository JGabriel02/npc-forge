import { CREATURE_SIZES, CreatureSize } from '../core/api/api.models';

/** "+3", "-1", "+0". */
export function formatModifier(mod: number): string {
  return mod >= 0 ? `+${mod}` : `${mod}`;
}

/** "16 (+3)". */
export function formatAbility(score: number, mod: number): string {
  return `${score} (${formatModifier(mod)})`;
}

/** "+5 para acertar", ou null quando a ação não é um ataque. */
export function formatAttackBonus(bonus: number | null): string | null {
  return bonus === null ? null : `${formatModifier(bonus)} para acertar`;
}

export function sizeLabel(size: CreatureSize): string {
  return CREATURE_SIZES.find((s) => s.value === size)?.label ?? size;
}
