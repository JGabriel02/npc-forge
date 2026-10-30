import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { CampaignsApi } from '../campaigns/campaigns.api';
import { NpcListPage } from './npc-list.page';
import { NpcsApi } from './npcs.api';

describe('NpcListPage', () => {
  const page = {
    items: [{ id: 'n1', name: 'Marta', race: null, occupation: null, campaignId: null, campaignName: null,
      hasStatBlock: false, challengeRating: null, updatedAt: '2026-10-29T00:00:00Z' }],
    page: 1, pageSize: 20, totalCount: 1,
  };

  async function render(isDemo: boolean) {
    TestBed.configureTestingModule({
      imports: [NpcListPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isDemo: signal(isDemo) } },
        { provide: NpcsApi, useValue: { list: () => of(page) } },
        { provide: CampaignsApi, useValue: { listOrEmpty: () => of([]) } },
      ],
    });
    const fixture = TestBed.createComponent(NpcListPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const buttons = (el: HTMLElement) => [...el.querySelectorAll('button, a')].map((b) => b.textContent ?? '');

  it('hides import and new NPC for a visitor but keeps export', async () => {
    const labels = buttons(await render(true));

    expect(labels.some((l) => l.includes('Importar'))).toBe(false);
    expect(labels.some((l) => l.includes('Novo NPC'))).toBe(false);
    expect(labels.some((l) => l.includes('Exportar'))).toBe(true);
  });

  it('shows import and new NPC for a regular user', async () => {
    const labels = buttons(await render(false));

    expect(labels.some((l) => l.includes('Importar'))).toBe(true);
    expect(labels.some((l) => l.includes('Novo NPC'))).toBe(true);
  });
});
