import { formatAbility, formatAttackBonus, formatModifier, sizeLabel } from './dnd-format';

describe('dnd-format', () => {
  it('formatModifier adds the sign', () => {
    expect(formatModifier(3)).toBe('+3');
    expect(formatModifier(-1)).toBe('-1');
    expect(formatModifier(0)).toBe('+0');
  });

  it('formatAbility shows score and modifier', () => {
    expect(formatAbility(16, 3)).toBe('16 (+3)');
    expect(formatAbility(8, -1)).toBe('8 (-1)');
  });

  it('formatAttackBonus describes the attack roll', () => {
    expect(formatAttackBonus(5)).toBe('+5 para acertar');
    expect(formatAttackBonus(-1)).toBe('-1 para acertar');
    expect(formatAttackBonus(null)).toBeNull();
  });

  it('sizeLabel translates creature sizes', () => {
    expect(sizeLabel('Gargantuan')).toBe('Imenso');
    expect(sizeLabel('Medium')).toBe('Médio');
  });
});
