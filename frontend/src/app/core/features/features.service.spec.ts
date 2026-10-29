import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { FeaturesService } from './features.service';

describe('FeaturesService', () => {
  let http: HttpTestingController;
  let features: FeaturesService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    features = TestBed.inject(FeaturesService);
  });

  afterEach(() => http.verify());

  it('starts with ai off', () => {
    expect(features.ai()).toBe(false);
  });

  it('turns ai on when the API says so', () => {
    features.load().subscribe();
    http.expectOne(`${environment.apiBaseUrl}/features`).flush({ ai: true });

    expect(features.ai()).toBe(true);
  });

  it('keeps ai off when the request fails', () => {
    let failed = false;
    features.load().subscribe({ error: () => (failed = true) });
    http.expectOne(`${environment.apiBaseUrl}/features`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(features.ai()).toBe(false);
    expect(failed).toBe(false);
  });
});
