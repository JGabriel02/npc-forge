import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { ProblemDetails } from './api.models';

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
