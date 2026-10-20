import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { outputFromObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { RequestDetails, RequestStatus } from '../../core/api/models';
import { errorMessage } from '../../core/http/error-messages';
import { RequestDetailsState } from './details/request-details.state';
import { RequestHistoryComponent } from './details/request-history.component';
import { RequestInfoComponent } from './details/request-info.component';
import { RequestStatusActionsComponent } from './details/request-status-actions.component';

/**
 * One request in the side drawer: details, status change (only the transitions the server allows) and history.
 * The id comes from the URL, so a refresh or a shared link reopens the same request.
 * The same pieces (and the same RequestDetailsState) make up the full-page view.
 */
@Component({
  selector: 'app-request-details',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, RequestInfoComponent,
    RequestStatusActionsComponent, RequestHistoryComponent],
  providers: [RequestDetailsState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <h2>פנייה {{ requestId() }}</h2>
      <span class="spacer"></span>
      <button mat-icon-button (click)="expand.emit()" aria-label="פתיחה במסך מלא" matTooltip="פתיחה במסך מלא"
              class="expand"><mat-icon>open_in_full</mat-icon></button>
      <button mat-icon-button (click)="close.emit()" aria-label="סגירה"><mat-icon>close</mat-icon></button>
    </header>

    @if (state.resource.isLoading() || state.saving()) { <mat-progress-bar mode="indeterminate" /> }

    @if (state.resource.error(); as error) {
      <p class="error" role="alert">{{ message(error) }}</p>
      <button mat-stroked-button (click)="state.resource.reload()">נסו שוב</button>
    } @else if (state.resource.value(); as v) {
      <app-request-info class="block" [request]="v.request" />
      <app-request-status-actions class="block" [request]="v.request" [saving]="state.saving()"
                                  (change)="changeStatus(v.request, $event)" />
      <app-request-history class="block" [history]="v.history" />
    }
  `,
  styles: `
    :host { display: block; padding: 0 20px 20px; }
    .head { display: flex; align-items: center; gap: 4px; }
    .spacer { flex: 1; }
    .block { margin-top: 16px; }
    .error { color: #8c1d18; }
  `,
})
export class RequestDetailsComponent {
  protected readonly state = inject(RequestDetailsState);

  readonly requestId = input.required<number>();
  readonly close = output<void>();
  /** Open this request in the full-page view. */
  readonly expand = output<void>();
  /** Emitted after a change was saved (or found outdated), so the list and the summary reload. */
  readonly changed = outputFromObservable(this.state.changed$);

  constructor() {
    this.state.bind(() => this.requestId());
  }

  protected message(error: unknown): string {
    return errorMessage(error);
  }

  protected changeStatus(request: RequestDetails, newStatus: RequestStatus): void {
    this.state.changeStatus(request, newStatus);
  }
}
