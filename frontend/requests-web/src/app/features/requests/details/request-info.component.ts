import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { RequestDetails } from '../../../core/api/models';
import { PRIORITY_LABELS } from '../../../core/i18n/labels';
import { StatusBadgeComponent } from '../../../shared/status-badge.component';

/** The request's fields (presentational). */
@Component({
  selector: 'app-request-info',
  imports: [DatePipe, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h3>{{ request().title }}</h3>
    <dl>
      <dt>ארגון</dt><dd>{{ request().organizationName }}</dd>
      <dt>סטטוס</dt><dd><app-status-badge [status]="request().status" /></dd>
      <dt>עדיפות</dt><dd>{{ priorityLabels[request().priority] }}</dd>
      <dt>מטפל</dt><dd>{{ request().assignedTo ?? 'לא שויך' }}</dd>
      <dt>נוצרה</dt><dd>{{ request().createdAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
      <dt>עודכנה</dt><dd>{{ request().updatedAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
    </dl>
  `,
  styles: `
    :host { display: block; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: 6px 16px; margin: 0; }
    dt { color: var(--mat-sys-on-surface-variant); }
    dd { margin: 0; }
  `,
})
export class RequestInfoComponent {
  readonly request = input.required<RequestDetails>();
  protected readonly priorityLabels = PRIORITY_LABELS;
}
