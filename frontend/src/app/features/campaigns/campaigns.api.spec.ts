import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { CampaignsApi } from './campaigns.api';

describe('CampaignsApi.listOrEmpty', () => {
  it('emits an empty list instead of erroring, so selects keep working', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const results: unknown[] = [];
    let failed = false;

    TestBed.inject(CampaignsApi).listOrEmpty().subscribe({ next: (r) => results.push(r), error: () => (failed = true) });
    http.expectOne(`${environment.apiBaseUrl}/campaigns`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(failed).toBe(false);
    expect(results).toEqual([[]]);
    http.verify();
  });
});
