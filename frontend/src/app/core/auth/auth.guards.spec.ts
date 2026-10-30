import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { writerGuard } from './auth.guards';

describe('writerGuard', () => {
  function run(isDemo: boolean): boolean | UrlTree {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isDemo: signal(isDemo) } }],
    });
    return TestBed.runInInjectionContext(() => writerGuard({} as never, {} as never)) as boolean | UrlTree;
  }

  it('sends a visitor back to the NPC list', () => {
    const result = run(true);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/npcs');
  });

  it('lets a regular user through', () => {
    expect(run(false)).toBe(true);
  });
});
