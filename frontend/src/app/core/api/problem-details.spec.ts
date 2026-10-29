import { HttpErrorResponse } from '@angular/common/http';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors, errorMessages } from './problem-details';

describe('errorMessages', () => {
  const form = () => new FormGroup({ email: new FormControl(''), password: new FormControl('') });
  const httpError = (status: number, body: unknown = null) =>
    new HttpErrorResponse({ status, error: body, url: 'http://localhost:8080/api/x' });

  it('explains a network failure', () => {
    expect(errorMessages(form(), httpError(0))).toEqual(['Não foi possível conectar à API.']);
  });

  it('uses a Portuguese message for server errors instead of the English title', () => {
    expect(errorMessages(form(), httpError(500, { title: 'An error occurred while processing your request.', status: 500 })))
      .toEqual(['Erro no servidor. Tente novamente em instantes.']);
  });

  it('uses a Portuguese message for 404 without field errors', () => {
    expect(errorMessages(form(), httpError(404, { title: 'Not Found', status: 404 })))
      .toEqual(['Não encontrado. Talvez tenha sido excluído.']);
  });

  it('keeps Portuguese titles from the API', () => {
    expect(errorMessages(form(), httpError(401, { title: 'E-mail ou senha inválidos.', status: 401 })))
      .toEqual(['E-mail ou senha inválidos.']);
  });

  it('translates Identity password errors', () => {
    const problem = { status: 400, errors: {
      PasswordRequiresDigit: ["Passwords must have at least one digit ('0'-'9')."],
      PasswordRequiresLower: ["Passwords must have at least one lowercase ('a'-'z')."],
    } };

    expect(errorMessages(form(), httpError(400, problem))).toEqual([
      'A senha precisa ter pelo menos um número.',
      'A senha precisa ter pelo menos uma letra minúscula.',
    ]);
  });

  it('shows a duplicated e-mail only once', () => {
    const problem = { status: 400, errors: {
      DuplicateUserName: ["Username 'a@x.com' is already taken."],
      DuplicateEmail: ["Email 'a@x.com' is already taken."],
    } };

    expect(errorMessages(form(), httpError(400, problem))).toEqual(['Este e-mail já está cadastrado.']);
  });
});

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
