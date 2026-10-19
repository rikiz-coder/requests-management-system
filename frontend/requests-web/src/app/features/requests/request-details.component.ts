import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';

import { RequestDetails, RequestStatus } from '../../core/api/models';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { errorMessage, problemOf } from '../../core/http/error-messages';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../core/i18n/labels';
import { CurrentUserService } from '../../core/user/current-user.service';
import { StatusBadgeComponent } from '../../shared/status-badge.component';
import { ConflictDialogComponent, ConflictDialogData, ConflictDialogResult } from './conflict-dialog.component';

/**
 * One request in the side drawer: details, status change (only the transitions the server allows) and history.
 * The id comes from the URL, so a refresh or a shared link reopens the same request.
 */
@Component({
  selector: 'app-request-details',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <h2>פנייה {{ requestId() }}</h2>
      <button mat-icon-button (click)="close.emit()" aria-label="סגירה"><mat-icon>close</mat-icon></button>
    </header>

    @if (resource.isLoading() || saving()) { <mat-progress-bar mode="indeterminate" /> }

    @if (resource.error(); as error) {
      <p class="error" role="alert">{{ message(error) }}</p>
      <button mat-stroked-button (click)="resource.reload()">נסו שוב</button>
    } @else if (resource.value(); as v) {
      <section class="details">
        <h3>{{ v.request.title }}</h3>
        <dl>
          <dt>ארגון</dt><dd>{{ v.request.organizationName }}</dd>
          <dt>סטטוס</dt><dd><app-status-badge [status]="v.request.status" /></dd>
          <dt>עדיפות</dt><dd>{{ priorityLabels[v.request.priority] }}</dd>
          <dt>מטפל</dt><dd>{{ v.request.assignedTo ?? 'לא שויך' }}</dd>
          <dt>נוצרה</dt><dd>{{ v.request.createdAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
          <dt>עודכנה</dt><dd>{{ v.request.updatedAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
        </dl>
      </section>

      <section class="actions">
        <h4>העברה לסטטוס</h4>
        @if (!user.name()) { <p class="hint">בחרו משתמש נוכחי בראש המסך כדי לעדכן.</p> }
        <div class="buttons">
          @for (s of v.request.allowedNextStatuses; track s) {
            <button mat-flat-button [disabled]="saving() || !user.name()" (click)="changeStatus(v.request, s)">
              {{ statusLabels[s] }}
            </button>
          }
        </div>
      </section>

      <section class="history">
        <h4>היסטוריית שינויים</h4>
        @for (h of v.history; track h.id) {
          <div class="entry">
            <div><app-status-badge [status]="h.previousStatus" /> ← <app-status-badge [status]="h.newStatus" /></div>
            <div class="meta">{{ h.changedBy }} · {{ h.changedAt | date: 'dd/MM/yyyy HH:mm' }}</div>
          </div>
        } @empty {
          <p class="hint">עדיין אין שינויי סטטוס.</p>
        }
      </section>
    }
  `,
  styles: `
    :host { display: block; padding: 0 20px 20px; }
    .head { display: flex; align-items: center; justify-content: space-between; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: 6px 16px; margin: 0; }
    dt { color: var(--mat-sys-on-surface-variant); }
    dd { margin: 0; }
    .buttons { display: flex; gap: 8px; flex-wrap: wrap; }
    .entry { padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .meta, .hint { color: var(--mat-sys-on-surface-variant); font-size: 13px; }
    .error { color: #8c1d18; }
  `,
})
export class RequestDetailsComponent {
  private readonly api = inject(RequestsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly user = inject(CurrentUserService);

  readonly requestId = input.required<number>();
  readonly close = output<void>();
  /** Emitted after a change was saved, so the list and the summary reload. */
  readonly changed = output<void>();

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;
  protected readonly saving = signal(false);

  /** Details + history, reloaded whenever the id changes (a newer id cancels the older request). */
  protected readonly resource = rxResource({
    params: () => this.requestId(),
    stream: ({ params: id }) => forkJoin({ request: this.api.get(id), history: this.api.history(id) }),
  });

  protected message(error: unknown): string {
    return errorMessage(error);
  }

  protected changeStatus(request: RequestDetails, newStatus: RequestStatus, version = request.version): void {
    this.saving.set(true);
    this.api.updateStatus(request.id, newStatus, version).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open(`הסטטוס עודכן ל${STATUS_LABELS[newStatus]}`, undefined, { duration: 3000 });
        this.resource.reload();
        this.changed.emit();
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
    this.changed.emit(); // the list is outdated too
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
