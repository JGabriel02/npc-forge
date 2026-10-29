import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { ProblemDetails } from './api.models';

// Erros do ASP.NET Identity (cadastro) chegam em inglês; estes são os que o app pode provocar.
const IDENTITY_MESSAGES: Record<string, string> = {
  PasswordRequiresDigit: 'A senha precisa ter pelo menos um número.',
  PasswordRequiresLower: 'A senha precisa ter pelo menos uma letra minúscula.',
  PasswordTooShort: 'A senha precisa ter pelo menos 8 caracteres.',
  DuplicateUserName: 'Este e-mail já está cadastrado.',
  DuplicateEmail: 'Este e-mail já está cadastrado.',
  InvalidEmail: 'E-mail inválido.',
};

/**
 * Converte qualquer erro de uma chamada à API em mensagens em português:
 * erros de campo vão para o formulário; o resto volta para exibir no topo.
 */
export function errorMessages(form: AbstractControl, error: unknown): string[] {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return ['Não foi possível conectar à API.'];
    if (error.status >= 500) return ['Erro no servidor. Tente novamente em instantes.'];
  }

  const problem = problemFrom(error);
  if (!problem) return ['Não foi possível concluir a operação.'];
  if (problem.status === 404 && !problem.errors) return ['Não encontrado. Talvez tenha sido excluído.'];

  const translated = translateIdentityErrors(problem);
  return [...new Set(applyServerErrors(form, translated))];
}

function translateIdentityErrors(problem: ProblemDetails): ProblemDetails {
  if (!problem.errors) return problem;
  const errors = Object.fromEntries(
    Object.entries(problem.errors).map(([key, messages]) => [key, IDENTITY_MESSAGES[key] ? [IDENTITY_MESSAGES[key]] : messages]),
  );
  return { ...problem, errors };
}

/** Extrai o corpo ProblemDetails de um erro HTTP (ou null se não for um). */
export function problemFrom(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/**
 * Coloca os erros de validação do servidor nos campos do formulário (erro `server`).
 * Devolve as mensagens que não têm campo correspondente, para exibir no topo do formulário.
 */
export function applyServerErrors(form: AbstractControl, problem: ProblemDetails | null | undefined): string[] {
  if (!problem) return [];

  const errors = problem.errors ?? {};
  if (Object.keys(errors).length === 0) {
    return problem.title ? [problem.title] : [];
  }

  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(errors)) {
    const control = findControl(form, key);
    if (control) {
      control.setErrors({ ...control.errors, server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

// "StatBlock.Str" → statBlock → str; "$.email" → email. Compara cada trecho sem diferenciar maiúsculas.
function findControl(root: AbstractControl, key: string): AbstractControl | null {
  const segments = key.replace(/^\$\.?/, '').split('.').filter(Boolean);
  if (segments.length === 0) return null;

  let current: AbstractControl | null = root;
  for (const segment of segments) {
    current = child(current, segment);
    if (!current) return null;
  }
  return current;
}

function child(parent: AbstractControl, segment: string): AbstractControl | null {
  if (parent instanceof FormArray) {
    return parent.at(Number(segment)) ?? null;
  }
  if (parent instanceof FormGroup) {
    const name = Object.keys(parent.controls).find((n) => n.toLowerCase() === segment.toLowerCase());
    return name ? parent.controls[name] : null;
  }
  return null;
}
