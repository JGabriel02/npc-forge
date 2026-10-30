import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { FeaturesService } from '../../core/features/features.service';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  const auth = { login: vi.fn(), loginAsDemo: vi.fn() };

  function render(demo: boolean) {
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: FeaturesService, useValue: { demo: signal(demo), ai: signal(false) } },
      ],
    });
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    return fixture;
  }

  const visitorButton = (el: HTMLElement) =>
    [...el.querySelectorAll('button')].find((b) => b.textContent?.includes('Entrar como visitante'));

  beforeEach(() => vi.resetAllMocks());

  it('shows the visitor button when the installation has a demo account', () => {
    expect(visitorButton(render(true).nativeElement)).toBeTruthy();
  });

  it('hides the visitor button without a demo account', () => {
    expect(visitorButton(render(false).nativeElement)).toBeUndefined();
  });

  it('enters as visitor and opens the NPC list', async () => {
    auth.loginAsDemo.mockReturnValue(of(undefined));
    const fixture = render(true);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    visitorButton(fixture.nativeElement)!.click();

    expect(auth.loginAsDemo).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/npcs');
  });

  it('shows the rate limit message on 429', () => {
    auth.loginAsDemo.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 429, error: { status: 429, title: 'Muitas tentativas. Aguarde um minuto e tente de novo.' },
    })));
    const fixture = render(true);

    visitorButton(fixture.nativeElement)!.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Muitas tentativas. Aguarde um minuto e tente de novo.');
  });
});
