import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { jwtInterceptor } from './jwt.interceptor';

const api = environment.apiBaseUrl;

describe('jwtInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let auth: AuthService;

  function login(expiresInMs = 60 * 60 * 1000) {
    localStorage.setItem('npcforge.token', 'jwt-123');
    localStorage.setItem('npcforge.expiresAt', new Date(Date.now() + expiresInMs).toISOString());
  }

  function setup() {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    auth = TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('adds the bearer header to API requests when logged in', () => {
    login();
    setup();

    client.get(`${api}/campaigns`).subscribe();

    expect(http.expectOne(`${api}/campaigns`).request.headers.get('Authorization')).toBe('Bearer jwt-123');
  });

  it('does not add the header to other origins', () => {
    login();
    setup();

    client.get('https://example.com/x').subscribe();

    expect(http.expectOne('https://example.com/x').request.headers.has('Authorization')).toBe(false);
  });

  it('does not add the header when the token is expired', () => {
    login(-1000);
    setup();

    client.get(`${api}/campaigns`).subscribe();

    expect(http.expectOne(`${api}/campaigns`).request.headers.has('Authorization')).toBe(false);
  });

  it('logs out on 401 from an API call', () => {
    login();
    setup();
    const logout = vi.spyOn(auth, 'logout').mockImplementation(() => {});

    client.get(`${api}/campaigns`).subscribe({ error: () => {} });
    http.expectOne(`${api}/campaigns`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(logout).toHaveBeenCalled();
  });

  it('does not log out on 401 from /auth/login', () => {
    setup();
    const logout = vi.spyOn(auth, 'logout').mockImplementation(() => {});

    client.post(`${api}/auth/login`, {}).subscribe({ error: () => {} });
    http.expectOne(`${api}/auth/login`).flush(
      { title: 'E-mail ou senha inválidos.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(logout).not.toHaveBeenCalled();
  });
});
