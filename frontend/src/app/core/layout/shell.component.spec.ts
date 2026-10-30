import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  const leaveDemoToRegister = vi.fn();

  function render(isDemo: boolean) {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { isDemo: signal(isDemo), user: signal({ displayName: 'Visitante' }), logout: vi.fn(), leaveDemoToRegister },
        },
      ],
    });
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the visitor banner with a link to create an account', () => {
    const el = render(true);

    expect(el.textContent).toContain('Você está como visitante');
    [...el.querySelectorAll('button, a')].find((b) => b.textContent?.includes('Criar conta'))!.dispatchEvent(new Event('click'));
    expect(leaveDemoToRegister).toHaveBeenCalled();
  });

  it('shows no banner for a regular user', () => {
    expect(render(false).textContent).not.toContain('Você está como visitante');
  });
});
