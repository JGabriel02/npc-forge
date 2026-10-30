import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TimeoutError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SlowServerNotice, slowServerInterceptor } from './slow-server.interceptor';

describe('slowServerInterceptor', () => {
  let notice: SlowServerNotice;
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([slowServerInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    notice = TestBed.inject(SlowServerNotice);
  });

  afterEach(() => vi.useRealTimers());

  const api = (path: string) => `${environment.apiBaseUrl}${path}`;

  it('does not show the notice for a request that answers within 4 s', () => {
    http.get(api('/npcs')).subscribe();
    vi.advanceTimersByTime(3000);
    backend.expectOne(api('/npcs')).flush({});
    vi.advanceTimersByTime(5000);

    expect(notice.visible()).toBe(false);
  });

  it('shows the notice at 4 s and hides it when the request finishes', () => {
    http.get(api('/npcs')).subscribe();
    vi.advanceTimersByTime(4000);

    expect(notice.visible()).toBe(true);

    backend.expectOne(api('/npcs')).flush({});
    expect(notice.visible()).toBe(false);
  });

  it('shows one notice for two slow requests and hides it after the last one', () => {
    http.get(api('/npcs')).subscribe();
    http.get(api('/campaigns')).subscribe();
    vi.advanceTimersByTime(4000);

    expect(notice.visible()).toBe(true);
    backend.expectOne(api('/npcs')).flush({});
    expect(notice.visible()).toBe(true);
    backend.expectOne(api('/campaigns')).flush([]);
    expect(notice.visible()).toBe(false);
  });

  it('gives up after 90 s', () => {
    let error: unknown;
    http.get(api('/npcs')).subscribe({ error: (e) => (error = e) });
    backend.expectOne(api('/npcs'));

    vi.advanceTimersByTime(90_000);

    expect(error).toBeInstanceOf(TimeoutError);
  });

  it('ignores requests that are not to the API', () => {
    http.get('https://fonts.example/x.css').subscribe();
    vi.advanceTimersByTime(5000);

    expect(notice.visible()).toBe(false);
    backend.expectOne('https://fonts.example/x.css').flush('');
  });
});
