import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { applyServerErrors, problemFrom } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register-page',
  imports: [
    ReactiveFormsModule, RouterLink, MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatProgressSpinnerModule,
  ],
  templateUrl: './register.page.html',
  styleUrl: './auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });
  protected readonly submitting = signal(false);
  protected readonly messages = signal<string[]>([]);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.messages.set([]);
    const { email, password, displayName } = this.form.getRawValue();

    this.auth.register(email, password, displayName).subscribe({
      next: () => void this.router.navigateByUrl('/campaigns'),
      error: (error: unknown) => {
        const problem = problemFrom(error);
        // Erros de senha do Identity vêm com chaves como "PasswordRequiresDigit": não casam com campo, viram mensagem geral.
        this.messages.set(problem ? applyServerErrors(this.form, problem) : ['Não foi possível conectar à API.']);
        this.submitting.set(false);
      },
    });
  }
}
