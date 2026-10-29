import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors } from './problem-details';

describe('applyServerErrors', () => {
  const buildForm = () =>
    new FormGroup({
      email: new FormControl(''),
      statBlock: new FormGroup({ str: new FormControl(10), hitDice: new FormControl('') }),
      traits: new FormArray([]),
    });

  it('maps nested PascalCase keys to camelCase controls', () => {
    const form = buildForm();

    const unmatched = applyServerErrors(form, { errors: { 'StatBlock.Str': ['Deve ser entre 1 e 30.'] } });

    expect(form.get('statBlock.str')?.errors?.['server']).toBe('Deve ser entre 1 e 30.');
    expect(form.get('statBlock.str')?.touched).toBe(true);
    expect(unmatched).toEqual([]);
  });

  it('maps System.Text.Json "$." paths', () => {
    const form = buildForm();

    applyServerErrors(form, { errors: { '$.email': ['E-mail inválido.'] } });

    expect(form.get('email')?.errors?.['server']).toBe('E-mail inválido.');
  });

  it('joins several messages for the same control', () => {
    const form = buildForm();

    applyServerErrors(form, { errors: { 'StatBlock.HitDice': ['Obrigatório.', 'Formato inválido.'] } });

    expect(form.get('statBlock.hitDice')?.errors?.['server']).toBe('Obrigatório. Formato inválido.');
  });

  it('returns messages without a matching control', () => {
    const form = buildForm();

    const unmatched = applyServerErrors(form, { errors: { DuplicateEmail: ['Este e-mail já está cadastrado.'] } });

    expect(unmatched).toEqual(['Este e-mail já está cadastrado.']);
  });

  it('returns the title when there are no field errors', () => {
    const unmatched = applyServerErrors(buildForm(), { status: 401, title: 'E-mail ou senha inválidos.' });

    expect(unmatched).toEqual(['E-mail ou senha inválidos.']);
  });

  it('returns [] for a null problem', () => {
    expect(applyServerErrors(buildForm(), null)).toEqual([]);
  });
});
