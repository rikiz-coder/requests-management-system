import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

import { RequestDetails, RequestStatus } from '../../../core/api/models';
import { STATUS_LABELS } from '../../../core/i18n/labels';
import { CurrentUserService } from '../../../core/user/current-user.service';

/** Buttons for the transitions the server allows from the current status (the server still enforces them). */
@Component({
  selector: 'app-request-status-actions',
  imports: [MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h4>העברה לסטטוס</h4>
    @if (!user.name()) { <p class="hint">בחרו משתמש נוכחי בראש המסך כדי לעדכן.</p> }
    <div class="buttons">
      @for (s of request().allowedNextStatuses; track s) {
        <button mat-flat-button [disabled]="saving() || !user.name()" (click)="change.emit(s)">
          {{ statusLabels[s] }}
        </button>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    h4 { margin: 0 0 12px; }
    .buttons { display: flex; gap: 8px; flex-wrap: wrap; }
    .hint { color: var(--mat-sys-on-surface-variant); font-size: 13px; }
  `,
})
export class RequestStatusActionsComponent {
  protected readonly user = inject(CurrentUserService);
  protected readonly statusLabels = STATUS_LABELS;

  readonly request = input.required<RequestDetails>();
  readonly saving = input(false);
  readonly change = output<RequestStatus>();
}
