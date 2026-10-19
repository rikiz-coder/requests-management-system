import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

import { BulkUpdateResult, RequestStatus } from '../../core/api/models';
import { OUTCOME_LABELS, STATUS_LABELS } from '../../core/i18n/labels';

export interface BulkResultDialogData {
  result: BulkUpdateResult;
  newStatus: RequestStatus;
}

/** Partial success, explained: how many were updated, and for every other request – why not. */
@Component({
  selector: 'app-bulk-result-dialog',
  imports: [MatDialogModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>תוצאת העדכון המרוכז</h2>
    <mat-dialog-content>
      <p>
        עודכנו <strong>{{ data.result.succeeded }}</strong> פניות ל{{ statusLabels[data.newStatus] }}.
        @if (data.result.failed > 0) { לא עודכנו <strong>{{ data.result.failed }}</strong>: }
      </p>
      @if (failures().length) {
        <ul>
          @for (f of failures(); track f.id) {
            <li>
              <button mat-button type="button" (click)="dialogRef.close(f.id)">פנייה {{ f.id }}</button>
              – {{ outcomeLabels[f.outcome] }}
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button mat-dialog-close>סגירה</button>
    </mat-dialog-actions>
  `,
})
export class BulkResultDialogComponent {
  protected readonly data = inject<BulkResultDialogData>(MAT_DIALOG_DATA);
  /** Closes with a request id when the user clicks one of the failures, to open it. */
  protected readonly dialogRef = inject<MatDialogRef<BulkResultDialogComponent, number>>(MatDialogRef);
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly outcomeLabels = OUTCOME_LABELS;
  protected readonly failures = computed(() => this.data.result.results.filter((r) => r.outcome !== 'Updated'));
}
