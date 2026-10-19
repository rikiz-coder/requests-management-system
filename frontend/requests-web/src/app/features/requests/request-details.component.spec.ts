import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';

import { RequestDetails } from '../../core/api/models';
import { RequestDetailsComponent } from './request-details.component';
import { ConflictDialogComponent } from './conflict-dialog.component';

const request: RequestDetails = {
  id: 5, title: 'בקשה', organizationName: 'ארגון', status: 'InProgress', priority: 'High', assignedTo: 'דוד',
  createdAt: '2026-01-01T08:00:00Z', updatedAt: '2026-01-02T08:00:00Z', version: 'AAAAAAAAB9E=',
  allowedNextStatuses: ['Waiting', 'Completed'],
};
const current: RequestDetails = { ...request, status: 'Waiting', version: 'AAAAAAAAB9I=', allowedNextStatuses: ['InProgress', 'Completed'] };
const conflict = {
  status: 409, title: 'Concurrency conflict', currentState: current,
  lastChange: { id: 9, previousStatus: 'InProgress', newStatus: 'Waiting', changedAt: '2026-01-03T08:00:00Z', changedBy: 'דנה' },
};

async function setup(dialogResult: 'reload' | 'apply') {
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(dialogResult) })) };
  TestBed.configureTestingModule({
    imports: [RequestDetailsComponent],
    providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: MatDialog, useValue: dialog },
      { provide: MatSnackBar, useValue: { open: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(RequestDetailsComponent);
  fixture.componentRef.setInput('requestId', 5);
  const http = TestBed.inject(HttpTestingController);
  // HTTP calls keep the app "unstable", so flush them first and only then wait for stability.
  fixture.detectChanges();
  TestBed.tick();
  http.expectOne('/api/requests/5').flush(request);
  http.expectOne('/api/requests/5/history').flush([]);
  await fixture.whenStable();
  const component = fixture.componentInstance as unknown as {
    changeStatus(r: RequestDetails, s: string): void; changed: { subscribe(fn: () => void): void };
  };
  return { fixture, http, dialog, component };
}

describe('RequestDetailsComponent – 409 handling', () => {
  it('sends the version in If-Match and, on 409, opens the conflict dialog with who changed it', async () => {
    const { http, dialog, component } = await setup('reload');

    component.changeStatus(request, 'Completed');
    const patch = http.expectOne('/api/requests/5/status');
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.headers.get('If-Match')).toBe('"AAAAAAAAB9E="');
    expect(patch.request.body).toEqual({ newStatus: 'Completed' });
    patch.flush(conflict, { status: 409, statusText: 'Conflict' });

    expect(dialog.open).toHaveBeenCalledWith(ConflictDialogComponent, expect.objectContaining({
      data: expect.objectContaining({ attempted: 'Completed', problem: expect.objectContaining({ lastChange: conflict.lastChange }) }),
    }));
    // "show the current state" reloads the request – nothing is sent again
    TestBed.tick();
    http.expectOne('/api/requests/5').flush(current);
    http.expectOne('/api/requests/5/history').flush([]);
    http.expectNone('/api/requests/5/status');
  });

  it('"apply anyway" re-sends the same change against the new version', async () => {
    const { http, component } = await setup('apply');

    component.changeStatus(request, 'Completed');
    http.expectOne('/api/requests/5/status').flush(conflict, { status: 409, statusText: 'Conflict' });

    const retry = http.expectOne('/api/requests/5/status');
    expect(retry.request.headers.get('If-Match')).toBe('"AAAAAAAAB9I="');
    expect(retry.request.body).toEqual({ newStatus: 'Completed' });
  });
});
