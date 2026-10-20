import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { StatusHistoryEntry } from '../../../core/api/models';
import { StatusBadgeComponent } from '../../../shared/status-badge.component';

/** Status history, newest first (presentational). */
@Component({
  selector: 'app-request-history',
  imports: [DatePipe, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h4>היסטוריית שינויים</h4>
    @for (h of history(); track h.id) {
      <div class="entry">
        <div><app-status-badge [status]="h.previousStatus" /> ← <app-status-badge [status]="h.newStatus" /></div>
        <div class="meta">{{ h.changedBy }} · {{ h.changedAt | date: 'dd/MM/yyyy HH:mm' }}</div>
      </div>
    } @empty {
      <p class="meta">עדיין אין שינויי סטטוס.</p>
    }
  `,
  styles: `
    :host { display: block; }
    h4 { margin: 0 0 4px; }
    .entry { padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .meta { color: var(--mat-sys-on-surface-variant); font-size: 13px; }
  `,
})
export class RequestHistoryComponent {
  readonly history = input.required<StatusHistoryEntry[]>();
}
