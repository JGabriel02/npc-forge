import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { NpcDraft, NpcDto } from '../../core/api/api.models';
import {
  buildNpcForm, defaultStatBlockValue, draftToFormValue, emptyNpcFormValue, loadNpcForm, NpcFormValue, planSave,
  toCreateRequest, toFormValue, toStatBlockRequest, toUpdateRequest,
} from './npc-form.model';

const npcWithStatBlock: NpcDto = {
  id: 'n1', campaignId: 'c1', name: 'Borin', race: 'anão', occupation: 'ferreiro',
  appearance: null, personality: 'rabugento', background: null, notes: 'deve dinheiro',
  createdAt: '2026-10-29T00:00:00Z', updatedAt: '2026-10-29T00:00:00Z',
  statBlock: {
    size: 'Medium', creatureType: 'humanoide (anão)', alignment: 'leal e neutro', armorClass: 16,
    hitPoints: 26, hitDice: '4d8 + 8', speed: '7,5 m', str: 16, dex: 10, con: 14, int: 10, wis: 12, cha: 8,
    challengeRating: '1', savingThrows: null, skills: 'Atletismo +5', senses: null, languages: 'Comum, Anão',
    traits: [{ name: 'Resiliência Anã', description: 'Vantagem contra veneno.' }],
    actions: [
      { name: 'Martelo', description: 'Ataque corpo a corpo.', attackBonus: 5, damage: '1d8 + 3' },
      { name: 'Grito', description: 'Assusta.', attackBonus: null, damage: null },
    ],
    reactions: [],
    modifiers: { str: 3, dex: 0, con: 2, int: 0, wis: 1, cha: -1 },
    proficiencyBonus: 2,
  },
};

const narrative = (): NpcFormValue => ({ ...emptyNpcFormValue(), name: 'Hilda' });

describe('npc-form.model', () => {
  it('toCreateRequest turns blank optional strings into null', () => {
    const request = toCreateRequest({ ...narrative(), race: '   ', occupation: ' taverneira ' });

    expect(request.race).toBeNull();
    expect(request.occupation).toBe('taverneira');
    expect(request.campaignId).toBeNull();
  });

  it('toCreateRequest omits the stat block when includeStatBlock is false', () => {
    const request = toCreateRequest({ ...narrative(), includeStatBlock: false, statBlock: defaultStatBlockValue() });

    expect(request.statBlock).toBeNull();
  });

  it('toUpdateRequest never carries a stat block', () => {
    const request = toUpdateRequest({ ...narrative(), includeStatBlock: true, statBlock: defaultStatBlockValue() });

    expect('statBlock' in request).toBe(false);
  });

  it('toStatBlockRequest maps empty attackBonus and damage to null', () => {
    const value = defaultStatBlockValue();
    value.actions = [{ name: 'Grito', description: 'Assusta.', attackBonus: null, damage: '  ' }];

    const request = toStatBlockRequest(value);

    expect(request.actions[0].attackBonus).toBeNull();
    expect(request.actions[0].damage).toBeNull();
    expect(request.savingThrows).toBeNull();
  });

  it('toFormValue round-trips an NPC with stat block', () => {
    const request = toCreateRequest(toFormValue(npcWithStatBlock));

    expect(request.name).toBe('Borin');
    expect(request.campaignId).toBe('c1');
    expect(request.notes).toBe('deve dinheiro');
    expect(request.statBlock?.hitDice).toBe('4d8 + 8');
    expect(request.statBlock?.actions).toEqual(npcWithStatBlock.statBlock!.actions);
    expect(request.statBlock?.traits).toEqual(npcWithStatBlock.statBlock!.traits);
  });

  it('planSave on create is just create', () => {
    expect(planSave(null, { ...narrative(), includeStatBlock: true })).toEqual([{ kind: 'create' }]);
  });

  it('planSave on edit adds removeStatBlock when the toggle is turned off', () => {
    const value = { ...toFormValue(npcWithStatBlock), includeStatBlock: false };

    expect(planSave(npcWithStatBlock, value)).toEqual([{ kind: 'update' }, { kind: 'removeStatBlock' }]);
  });

  it('planSave on edit adds setStatBlock when the toggle is on', () => {
    const original = { ...npcWithStatBlock, statBlock: null };

    expect(planSave(original, { ...narrative(), includeStatBlock: true })).toEqual([
      { kind: 'update' },
      { kind: 'setStatBlock' },
    ]);
  });

  it('planSave on edit of a narrative NPC with toggle off is just update', () => {
    const original = { ...npcWithStatBlock, statBlock: null };

    expect(planSave(original, narrative())).toEqual([{ kind: 'update' }]);
  });

  describe('draftToFormValue', () => {
    const draft: NpcDraft = {
      name: 'Vasilka', race: 'humana', occupation: null, appearance: 'Olhos de vidro.',
      personality: null, background: 'Montada por um cientista.', statBlock: null,
    };

    it('preserves campaign and notes from the current form', () => {
      const current = { ...emptyNpcFormValue(), name: 'Antigo', campaignId: 'c1', notes: 'segredo da mesa' };

      const value = draftToFormValue(draft, current);

      expect(value.name).toBe('Vasilka');
      expect(value.campaignId).toBe('c1');
      expect(value.notes).toBe('segredo da mesa');
    });

    it('maps null optional texts to empty strings', () => {
      const value = draftToFormValue(draft, emptyNpcFormValue());

      expect(value.occupation).toBe('');
      expect(value.personality).toBe('');
      expect(value.appearance).toBe('Olhos de vidro.');
    });

    it('without stat block turns the toggle off and resets the sheet to defaults', () => {
      const current = { ...toFormValue(npcWithStatBlock), includeStatBlock: true };

      const value = draftToFormValue(draft, current);

      expect(value.includeStatBlock).toBe(false);
      expect(value.statBlock).toEqual(defaultStatBlockValue());
    });

    it('with stat block turns the toggle on and fills the sheet', () => {
      const { modifiers, proficiencyBonus, ...sheet } = npcWithStatBlock.statBlock!;

      const value = draftToFormValue({ ...draft, statBlock: sheet }, emptyNpcFormValue());

      expect(value.includeStatBlock).toBe(true);
      expect(value.statBlock.hitDice).toBe('4d8 + 8');
      expect(value.statBlock.actions[0].attackBonus).toBe(5);
      expect(value.statBlock.savingThrows).toBe('');
    });
  });

  describe('form', () => {
    const build = () => buildNpcForm(TestBed.inject(NonNullableFormBuilder));

    it('stat block group is disabled when includeStatBlock is false', () => {
      const form = build();
      form.controls.name.setValue('Hilda');
      form.controls.statBlock.controls.str.setValue(99);

      expect(form.controls.statBlock.disabled).toBe(true);
      expect(form.valid).toBe(true);
    });

    it('stat block group validates when includeStatBlock is true', () => {
      const form = build();
      form.controls.name.setValue('Borin');
      form.controls.includeStatBlock.setValue(true);
      form.controls.statBlock.controls.str.setValue(31);

      expect(form.controls.statBlock.enabled).toBe(true);
      expect(form.valid).toBe(false);
    });

    it('accepts hit dice in the book format and rejects invalid notation', () => {
      const form = build();
      form.controls.includeStatBlock.setValue(true); // campo desabilitado nunca é "valid"
      const hitDice = form.controls.statBlock.controls.hitDice;

      for (const valid of ['2d8+2', '2d8 + 2', '1d4 – 1', '3d8']) {
        hitDice.setValue(valid);
        expect(hitDice.valid).toBe(true);
      }
      for (const invalid of ['4d', 'd8', '2d8 * 2']) {
        hitDice.setValue(invalid);
        expect(hitDice.valid).toBe(false);
      }
    });

    it('loadNpcForm sizes the lists and fills the form', () => {
      const form = build();

      loadNpcForm(TestBed.inject(NonNullableFormBuilder), form, toFormValue(npcWithStatBlock));

      expect(form.controls.includeStatBlock.value).toBe(true);
      expect(form.controls.statBlock.enabled).toBe(true);
      expect(form.controls.statBlock.controls.actions.length).toBe(2);
      expect(form.controls.statBlock.controls.actions.at(0).controls.attackBonus.value).toBe(5);
      expect(form.getRawValue().name).toBe('Borin');
    });
  });
});
