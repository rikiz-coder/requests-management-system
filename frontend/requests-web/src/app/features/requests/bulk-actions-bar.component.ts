import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { RequestStatus, STATUSES } from '../../core/api/models';
import { STATUS_LABELS } from '../../core/i18n/labels';

/** Appears when requests are selected: choose a status and update all of them (up to 100). */
@Component({
  selector: 'app-bulk-actions-bar',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bar" role="region" aria-label="עדכון מרוכז">
      <span><strong>{{ count() }}</strong> פניות נבחרו</span>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>סטטוס חדש</mat-label>
        <mat-select [value]="target()" (selectionChange)="target.set($event.value)">
          @for (s of statuses; track s) { <mat-option [value]="s">{{ labels[s] }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <button mat-flat-button [disabled]="!target() || busy() || count() > max || !canSubmit()"
              (click)="submit.emit(target()!)">עדכון כל הנבחרות</button>
      <button mat-button (click)="clear.emit()">ביטול הבחירה</button>
      @if (count() > max) { <span class="warn">אפשר לעדכן עד {{ max }} פניות בבת אחת.</span> }
      @if (!canSubmit()) { <span class="warn">בחרו משתמש נוכחי בראש המסך.</span> }
    </div>
  `,
  styles: `
    .bar { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; padding: 8px 16px; border-radius: 12px;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    mat-form-field { width: 160px; }
    .warn { color: #8c1d18; }
  `,
})
export class BulkActionsBarComponent {
  readonly count = input.required<number>();
  readonly busy = input(false);
  readonly canSubmit = input(true);
  readonly submit = output<RequestStatus>();
  readonly clear = output<void>();

  protected readonly max = 100;
  protected readonly statuses = STATUSES;
  protected readonly labels = STATUS_LABELS;
  protected readonly target = signal<RequestStatus | null>(null);
}
