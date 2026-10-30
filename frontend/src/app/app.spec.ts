import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { SlowServerNotice } from './core/api/slow-server.interceptor';

describe('App', () => {
  it('should create the app with a router outlet', () => {
    TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('router-outlet')).not.toBeNull();
  });

  it('shows the wake-up notice while the server is slow', () => {
    TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(App);
    const notice = TestBed.inject(SlowServerNotice);

    notice.start();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Acordando o servidor… a primeira visita do dia pode levar até um minuto.');

    notice.stop();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Acordando o servidor');
  });
});
