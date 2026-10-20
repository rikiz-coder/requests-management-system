import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, forkJoin } from 'rxjs';

import { RequestDetails, RequestStatus } from '../../../core/api/models';
import { RequestsApiService } from '../../../core/api/requests-api.service';
import { errorMessage, isNotFound, problemOf } from '../../../core/http/error-messages';
import { STATUS_LABELS } from '../../../core/i18n/labels';
import { ConflictDialogComponent, ConflictDialogData, ConflictDialogResult } from '../conflict-dialog.component';

/**
 * One request and its history, and changing its status (409 → conflict dialog, 422 → message + reload).
 * Shared by the side drawer and the full-page view: each provides its own instance and binds it to its id.
 */
@Injectable()
export class RequestDetailsState {
  private readonly api = inject(RequestsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  private readonly id = signal<() => number | null>(() => null);
  private readonly changedSubject = new Subject<void>();

  /** A change was saved, or the request turned out to be outdated – lists that show it should reload. */
  readonly changed$ = this.changedSubject.asObservable();
  readonly saving = signal(false);

  /** Details + history, reloaded whenever the id changes (a newer id cancels the older request). */
  readonly resource = rxResource({
    params: () => this.id()() ?? undefined,
    stream: ({ params: id }) => forkJoin({ request: this.api.get(id), history: this.api.history(id) }),
  });

  readonly notFound = computed(() => isNotFound(this.resource.error()));

  bind(id: () => number | null): void {
    this.id.set(id);
  }

  changeStatus(request: RequestDetails, newStatus: RequestStatus, version = request.version): void {
    this.saving.set(true);
    this.api.updateStatus(request.id, newStatus, version).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open(`הסטטוס עודכן ל${STATUS_LABELS[newStatus]}`, undefined, { duration: 3000 });
        this.resource.reload();
        this.changedSubject.next();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.onConflict(request, newStatus, error);
        } else {
          this.snackBar.open(errorMessage(error), 'סגירה', { duration: 6000 });
          if (error instanceof HttpErrorResponse && error.status === 422) {
            this.resource.reload();
          }
        }
      },
    });
  }

  private onConflict(request: RequestDetails, attempted: RequestStatus, error: HttpErrorResponse): void {
    const problem = problemOf(error) ?? {};
    this.changedSubject.next(); // the list is outdated too
    this.dialog
      .open<ConflictDialogComponent, ConflictDialogData, ConflictDialogResult>(ConflictDialogComponent, {
        data: { attempted, problem },
        width: '480px',
      })
      .afterClosed()
      .subscribe((result) => {
        const current = problem.currentState;
        if (result === 'apply' && current) {
          this.changeStatus(request, attempted, current.version);
        } else {
          this.resource.reload();
        }
      });
  }
}
