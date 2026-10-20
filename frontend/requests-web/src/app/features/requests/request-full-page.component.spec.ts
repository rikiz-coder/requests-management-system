import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Params, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { RequestDetails } from '../../core/api/models';
import { RequestFullPageComponent } from './request-full-page.component';

const request: RequestDetails = {
  id: 5, title: 'בקשה', organizationName: 'אורן לוגיסטיקה', status: 'InProgress', priority: 'High', assignedTo: 'דוד',
  createdAt: '2026-01-01T08:00:00Z', updatedAt: '2026-01-02T08:00:00Z', version: 'AAAAAAAAB9E=',
  allowedNextStatuses: ['Waiting', 'Completed'],
};

function setup(id: string, query: Params = {}) {
  TestBed.configureTestingModule({
    imports: [RequestFullPageComponent],
    providers: [
      provideRouter([]),
      provideHttpClient(), provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id })), queryParamMap: of(convertToParamMap(query)) } },
      { provide: MatDialog, useValue: { open: vi.fn() } },
      { provide: MatSnackBar, useValue: { open: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(RequestFullPageComponent);
  const http = TestBed.inject(HttpTestingController);
  // HTTP calls keep the app "unstable", so flush them first and only then wait for stability.
  fixture.detectChanges();
  TestBed.tick();
  return { fixture, http };
}

const links = (root: HTMLElement) =>
  [...root.querySelectorAll('a')].map((a) => ({ text: a.textContent!.trim(), href: decodeURIComponent(a.getAttribute('href') ?? '') }));

describe('RequestFullPageComponent', () => {
  it('"back to the list" restores the list query; the related-request links filter the list', async () => {
    const { fixture, http } = setup('5', { status: 'Waiting', page: '2', sortBy: 'title', sortDirection: 'asc' });
    http.expectOne('/api/requests/5').flush(request);
    http.expectOne('/api/requests/5/history').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const found = links(fixture.nativeElement);
    expect(found).toContainEqual({ text: expect.stringContaining('חזרה לרשימה'), href: '/?status=Waiting&sortBy=title&sortDirection=asc&page=2' });
    expect(found).toContainEqual({ text: 'פניות נוספות של הארגון', href: '/?organizationName=אורן לוגיסטיקה' });
    expect(found).toContainEqual({ text: 'פניות נוספות של המטפל', href: '/?assignedTo=דוד' });
    expect(fixture.nativeElement.textContent).toContain('שינויי סטטוס');
  });

  it('shows "not found" for a request that does not exist', async () => {
    const { fixture, http } = setup('999999');
    http.expectOne('/api/requests/999999').flush({ status: 404 }, { status: 404, statusText: 'Not Found' });
    http.match('/api/requests/999999/history').forEach((r) => r.flush([]));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('הפנייה לא נמצאה');
  });
});
