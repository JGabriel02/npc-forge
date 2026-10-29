import { FormGroup, NonNullableFormBuilder, Validators } from '@angular/forms';
import {
  CreateNpcRequest, CreatureSize, NpcDto, StatBlockActionDto, StatBlockFeatureDto, StatBlockRequest, UpdateNpcRequest,
} from '../../core/api/api.models';

// Mesmo formato aceito pelo backend: "2d8+2", "2d8 + 2", "1d4 – 1" (hífen, travessão ou sinal de menos).
export const HIT_DICE_PATTERN = /^\s*\d+\s*[dD]\s*\d+\s*([+\-–−]\s*\d+)?\s*$/;

export function buildFeatureGroup(fb: NonNullableFormBuilder, value?: StatBlockFeatureDto) {
  return fb.group({
    name: [value?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [value?.description ?? '', Validators.maxLength(2000)],
  });
}

export function buildActionGroup(fb: NonNullableFormBuilder, value?: StatBlockActionDto) {
  return fb.group({
    name: [value?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [value?.description ?? '', Validators.maxLength(2000)],
    attackBonus: fb.control<number | null>(value?.attackBonus ?? null),
    damage: [value?.damage ?? '', Validators.maxLength(500)],
  });
}

export type FeatureGroup = ReturnType<typeof buildFeatureGroup>;
export type ActionGroup = ReturnType<typeof buildActionGroup>;

function buildStatBlockGroup(fb: NonNullableFormBuilder) {
  const ability = () => fb.control(10, [Validators.required, Validators.min(1), Validators.max(30)]);

  return fb.group({
    size: fb.control<CreatureSize>('Medium'),
    creatureType: ['humanoide', [Validators.required, Validators.maxLength(100)]],
    alignment: ['neutro', [Validators.required, Validators.maxLength(100)]],
    armorClass: [10, [Validators.required, Validators.min(1), Validators.max(30)]],
    hitPoints: [4, [Validators.required, Validators.min(1)]],
    hitDice: ['1d8', [Validators.required, Validators.pattern(HIT_DICE_PATTERN), Validators.maxLength(100)]],
    speed: ['9 m', [Validators.required, Validators.maxLength(100)]],
    str: ability(),
    dex: ability(),
    con: ability(),
    int: ability(),
    wis: ability(),
    cha: ability(),
    challengeRating: ['0', Validators.required],
    savingThrows: ['', Validators.maxLength(500)],
    skills: ['', Validators.maxLength(500)],
    senses: ['', Validators.maxLength(500)],
    languages: ['', Validators.maxLength(500)],
    traits: fb.array<FeatureGroup>([]),
    actions: fb.array<ActionGroup>([]),
    reactions: fb.array<FeatureGroup>([]),
  });
}

/** Formulário do NPC. O grupo da ficha só fica habilitado (e só valida) com "Incluir ficha" ligado. */
export function buildNpcForm(fb: NonNullableFormBuilder) {
  const form = fb.group({
    campaignId: [''], // '' = sem campanha
    name: ['', [Validators.required, Validators.maxLength(100)]],
    race: ['', Validators.maxLength(100)],
    occupation: ['', Validators.maxLength(100)],
    appearance: ['', Validators.maxLength(2000)],
    personality: ['', Validators.maxLength(2000)],
    background: ['', Validators.maxLength(4000)],
    notes: ['', Validators.maxLength(4000)],
    includeStatBlock: [false],
    statBlock: buildStatBlockGroup(fb),
  });

  form.controls.statBlock.disable();
  form.controls.includeStatBlock.valueChanges.subscribe((include) =>
    include ? form.controls.statBlock.enable() : form.controls.statBlock.disable(),
  );
  return form;
}

export type NpcForm = ReturnType<typeof buildNpcForm>;
export type NpcFormValue = ReturnType<NpcForm['getRawValue']>;
export type StatBlockFormValue = NpcFormValue['statBlock'];

export function defaultStatBlockValue(): StatBlockFormValue {
  return {
    size: 'Medium', creatureType: 'humanoide', alignment: 'neutro', armorClass: 10, hitPoints: 4,
    hitDice: '1d8', speed: '9 m', str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10, challengeRating: '0',
    savingThrows: '', skills: '', senses: '', languages: '', traits: [], actions: [], reactions: [],
  };
}

export function emptyNpcFormValue(): NpcFormValue {
  return {
    campaignId: '', name: '', race: '', occupation: '', appearance: '', personality: '', background: '',
    notes: '', includeStatBlock: false, statBlock: defaultStatBlockValue(),
  };
}

/** Preenche o formulário, recriando as listas da ficha com o tamanho certo antes de atribuir os valores. */
export function loadNpcForm(fb: NonNullableFormBuilder, form: NpcForm, value: NpcFormValue): void {
  const statBlock = form.controls.statBlock.controls;
  statBlock.traits.clear();
  statBlock.actions.clear();
  statBlock.reactions.clear();
  value.statBlock.traits.forEach((t) => statBlock.traits.push(buildFeatureGroup(fb, t)));
  value.statBlock.actions.forEach((a) => statBlock.actions.push(buildActionGroup(fb, a)));
  value.statBlock.reactions.forEach((r) => statBlock.reactions.push(buildFeatureGroup(fb, r)));

  form.setValue(value);
}

export function toFormValue(npc: NpcDto): NpcFormValue {
  const s = npc.statBlock;
  return {
    campaignId: npc.campaignId ?? '',
    name: npc.name,
    race: npc.race ?? '',
    occupation: npc.occupation ?? '',
    appearance: npc.appearance ?? '',
    personality: npc.personality ?? '',
    background: npc.background ?? '',
    notes: npc.notes ?? '',
    includeStatBlock: s !== null,
    statBlock: s
      ? {
          size: s.size, creatureType: s.creatureType, alignment: s.alignment, armorClass: s.armorClass,
          hitPoints: s.hitPoints, hitDice: s.hitDice, speed: s.speed,
          str: s.str, dex: s.dex, con: s.con, int: s.int, wis: s.wis, cha: s.cha,
          challengeRating: s.challengeRating,
          savingThrows: s.savingThrows ?? '', skills: s.skills ?? '', senses: s.senses ?? '',
          languages: s.languages ?? '',
          traits: s.traits.map((t) => ({ ...t })),
          actions: s.actions.map((a) => ({ ...a, damage: a.damage ?? '' })),
          reactions: s.reactions.map((r) => ({ ...r })),
        }
      : defaultStatBlockValue(),
  };
}

export function toUpdateRequest(value: NpcFormValue): UpdateNpcRequest {
  return {
    campaignId: value.campaignId || null,
    name: value.name.trim(),
    race: blankToNull(value.race),
    occupation: blankToNull(value.occupation),
    appearance: blankToNull(value.appearance),
    personality: blankToNull(value.personality),
    background: blankToNull(value.background),
    notes: blankToNull(value.notes),
  };
}

export function toCreateRequest(value: NpcFormValue): CreateNpcRequest {
  return {
    ...toUpdateRequest(value),
    statBlock: value.includeStatBlock ? toStatBlockRequest(value.statBlock) : null,
  };
}

export function toStatBlockRequest(value: StatBlockFormValue): StatBlockRequest {
  const feature = (f: { name: string; description: string }): StatBlockFeatureDto => ({
    name: f.name.trim(),
    description: f.description.trim(),
  });

  return {
    size: value.size,
    creatureType: value.creatureType.trim(),
    alignment: value.alignment.trim(),
    armorClass: value.armorClass,
    hitPoints: value.hitPoints,
    hitDice: value.hitDice.trim(),
    speed: value.speed.trim(),
    str: value.str, dex: value.dex, con: value.con, int: value.int, wis: value.wis, cha: value.cha,
    challengeRating: value.challengeRating,
    savingThrows: blankToNull(value.savingThrows),
    skills: blankToNull(value.skills),
    senses: blankToNull(value.senses),
    languages: blankToNull(value.languages),
    traits: value.traits.map(feature),
    actions: value.actions.map((a) => ({
      ...feature(a),
      // Campo numérico vazio chega como null (ou NaN); nunca deve virar 0.
      attackBonus: typeof a.attackBonus === 'number' && !Number.isNaN(a.attackBonus) ? a.attackBonus : null,
      damage: blankToNull(a.damage),
    })),
    reactions: value.reactions.map(feature),
  };
}

export type SaveStep = { kind: 'create' } | { kind: 'update' } | { kind: 'setStatBlock' } | { kind: 'removeStatBlock' };

/** Quais chamadas à API salvar exige: criar é uma chamada só; editar pode mexer na ficha separadamente. */
export function planSave(original: NpcDto | null, value: NpcFormValue): SaveStep[] {
  if (!original) return [{ kind: 'create' }];

  const steps: SaveStep[] = [{ kind: 'update' }];
  if (value.includeStatBlock) steps.push({ kind: 'setStatBlock' });
  else if (original.statBlock) steps.push({ kind: 'removeStatBlock' });
  return steps;
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/** Usado pelo canDeactivate: há alterações não salvas? */
export function isDirty(form: FormGroup): boolean {
  return form.dirty;
}
