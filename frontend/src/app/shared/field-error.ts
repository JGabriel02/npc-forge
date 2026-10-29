import { AbstractControl } from '@angular/forms';

/** Mensagem de erro em português para um campo (prioriza o erro vindo do servidor). */
export function fieldError(control: AbstractControl): string | null {
  const errors = control.errors;
  if (!errors) return null;

  if (errors['server']) return errors['server'];
  if (errors['required']) return 'Obrigatório.';
  if (errors['email']) return 'E-mail inválido.';
  if (errors['maxlength']) return `Máximo de ${errors['maxlength'].requiredLength} caracteres.`;
  if (errors['min']) return `Mínimo ${errors['min'].min}.`;
  if (errors['max']) return `Máximo ${errors['max'].max}.`;
  if (errors['pattern']) return 'Formato inválido.';
  return 'Valor inválido.';
}
