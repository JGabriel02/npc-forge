import { FormControl, Validators } from '@angular/forms';
import { fieldError } from './field-error';

describe('fieldError', () => {
  it('shows the server message even when the template has no specific mat-error for it', () => {
    const control = new FormControl('a@x', Validators.email);
    control.setErrors({ email: true, server: 'E-mail inválido.' });

    expect(fieldError(control)).toBe('E-mail inválido.');
  });

  it('translates the e-mail validator', () => {
    const control = new FormControl('abc', Validators.email);

    expect(fieldError(control)).toBe('E-mail inválido.');
  });

  it('returns null for a valid control', () => {
    expect(fieldError(new FormControl('ok'))).toBeNull();
  });
});
