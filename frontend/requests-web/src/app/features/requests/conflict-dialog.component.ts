import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

import { ProblemDetails, RequestStatus } from '../../core/api/models';
import { STATUS_LABELS } from '../../core/i18n/labels';

export interface ConflictDialogData {
  attempted: RequestStatus;
  problem: ProblemDetails;
}

/** 'reload' – show the current state; 'apply' – send the change again against the new version. */
export type ConflictDialogResult = 'reload' | 'apply';

/**
 * Shown on 409: what the request looks like now, who changed it and when, and what the user tried to save
 * (which was NOT saved). The system does not decide who is right – the user does.
 */
@Component({
  selector: 'app-conflict-dialog',
  imports: [MatDialogModule, MatButtonModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>הפנייה עודכנה בינתיים על ידי משתמש אחר</h2>
    <mat-dialog-content>
      <p>השינוי שלך ל<strong>{{ labels[data.attempted] }}</strong>&nbsp;<strong>לא נשמר</strong>, כדי לא לדרוס את העדכון של המשתמש האחר.</p>
      @if (current; as c) {
        <p>הסטטוס עכשיו: <strong>{{ labels[c.status] }}</strong></p>
      }
      @if (data.problem.lastChange; as last) {
        <p>
          <strong>{{ last.changedBy }}</strong> שינה מ{{ labels[last.previousStatus] }} ל{{ labels[last.newStatus] }}
          ב־{{ last.changedAt | date: 'dd/MM/yyyy HH:mm:ss' }}.
        </p>
      }
      @if (!canApply) {
        <p class="note">מהסטטוס הנוכחי אי אפשר לעבור ל{{ labels[data.attempted] }}.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="'reload'">הצגת המצב העדכני</button>
      @if (canApply) {
        <button mat-flat-button [mat-dialog-close]="'apply'">להעביר בכל זאת ל{{ labels[data.attempted] }}</button>
      }
    </mat-dialog-actions>
  `,
  styles: `.note { color: var(--mat-sys-on-surface-variant); }`,
})
export class ConflictDialogComponent {
  protected readonly data = inject<ConflictDialogData>(MAT_DIALOG_DATA);
  protected readonly labels = STATUS_LABELS;
  protected readonly current = this.data.problem.currentState ?? null;

  /** "Apply anyway" is offered only if the change is still allowed from the new state. */
  protected readonly canApply = !!this.current && this.current.allowedNextStatuses.includes(this.data.attempted);
}
