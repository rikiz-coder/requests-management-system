import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { PRIORITIES, RequestStatus, RequestSummary, STATUSES } from '../../core/api/models';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../core/i18n/labels';
import { LoadState } from './requests-list.store';

/** Aggregations for the current filters; clicking a status filters the list by it. */
@Component({
  selector: 'app-summary-panel',
  imports: [DecimalPipe, MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="summary" aria-label="נתונים מסכמים">
      <div class="total">
        <span class="value">{{ (data()?.total ?? 0) | number }}</span>
        <span class="label">פניות</span>
      </div>
      <div class="group">
        @for (s of statuses; track s) {
          <button type="button" class="tile status-{{ s }}" [class.active]="activeStatuses().includes(s)"
                  (click)="statusClick.emit(s)" [attr.aria-pressed]="activeStatuses().includes(s)">
            <span class="value">{{ statusCount(s) | number }}</span>
            <span class="label">{{ statusLabels[s] }}</span>
          </button>
        }
      </div>
      <div class="group priorities">
        @for (p of priorities; track p) {
          <div class="tile small">
            <span class="value">{{ priorityCount(p) | number }}</span>
            <span class="label">עדיפות {{ priorityLabels[p] }}</span>
          </div>
        }
      </div>
    </section>
    @if (state().loading) { <mat-progress-bar mode="indeterminate" class="bar" /> }
  `,
  styles: `
    .summary { display: flex; flex-wrap: wrap; gap: 16px; align-items: stretch; }
    .group { display: flex; gap: 8px; flex-wrap: wrap; }
    .total, .tile { display: flex; flex-direction: column; align-items: center; justify-content: center;
      min-width: 92px; padding: 8px 12px; border-radius: 12px; background: var(--mat-sys-surface-container); border: 2px solid transparent; }
    .tile { cursor: pointer; font: inherit; color: inherit; }
    .tile.small { cursor: default; min-width: 80px; background: var(--mat-sys-surface-container-low); }
    .tile.active { border-color: var(--mat-sys-primary); }
    .value { font-size: 22px; font-weight: 600; }
    .label { font-size: 13px; color: var(--mat-sys-on-surface-variant); }
    .bar { margin-top: 4px; }
  `,
})
export class SummaryPanelComponent {
  readonly state = input.required<LoadState<RequestSummary>>();
  readonly activeStatuses = input<RequestStatus[]>([]);
  readonly statusClick = output<RequestStatus>();

  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;
  protected readonly data = computed(() => this.state().data);

  protected statusCount(status: RequestStatus): number {
    return this.data()?.byStatus.find((s) => s.status === status)?.count ?? 0;
  }

  protected priorityCount(priority: string): number {
    return this.data()?.byPriority.find((p) => p.priority === priority)?.count ?? 0;
  }
}
