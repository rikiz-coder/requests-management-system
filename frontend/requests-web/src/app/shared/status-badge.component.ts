import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { RequestStatus } from '../core/api/models';
import { STATUS_LABELS } from '../core/i18n/labels';

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge status-' + status()">{{ label() }}</span>`,
  styles: `
    .badge { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 13px; white-space: nowrap; }
    .status-New { background: #e3f2fd; color: #0d47a1; }
    .status-InProgress { background: #fff3e0; color: #8a4b00; }
    .status-Waiting { background: #f3e5f5; color: #5e1f6e; }
    .status-Completed { background: #e8f5e9; color: #1b5e20; }
  `,
})
export class StatusBadgeComponent {
  readonly status = input.required<RequestStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
}
