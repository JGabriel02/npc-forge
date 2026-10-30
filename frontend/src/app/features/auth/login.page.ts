import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { errorMessages } from '../../core/api/problem-details';
import { fieldError } from '../../shared/field-error';
import { AuthService } from '../../core/auth/auth.service';
import { FeaturesService } from '../../core/features/features.service';

@Component({
  selector: 'app-login-page',
  imports: [
    ReactiveFormsModule, RouterLink, MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatProgressSpinnerModule,
  ],
  templateUrl: './login.page.html',
  styleUrl: './auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly submitting = signal(false);
  protected readonly messages = signal<string[]>([]);
  protected readonly fieldError = fieldError;
  protected readonly demoAvailable = inject(FeaturesService).demo;

  /** Conta de visitante: entra sem cadastro, só para ver o app funcionando. */
  protected enterAsVisitor(): void {
    this.submitting.set(true);
    this.messages.set([]);
    this.auth.loginAsDemo().subscribe({
      next: () => void this.router.navigateByUrl('/npcs'),
      error: (error: unknown) => {
        this.messages.set(errorMessages(this.form, error));
        this.submitting.set(false);
      },
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.messages.set([]);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl('/campaigns'),
      error: (error: unknown) => {
        this.messages.set(errorMessages(this.form, error));
        this.submitting.set(false);
      },
    });
  }
}
