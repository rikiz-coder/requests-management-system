import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { RequestDetails, RequestStatus } from '../../core/api/models';
import { errorMessage } from '../../core/http/error-messages';
import { RequestDetailsState } from './details/request-details.state';
import { formatDuration, requestFacts } from './details/request-facts';
import { RequestHistoryComponent } from './details/request-history.component';
import { RequestInfoComponent } from './details/request-info.component';
import { RequestStatusActionsComponent } from './details/request-status-actions.component';
import { listParams, parseQuery } from './request-query';

/**
 * /requests/:id – one request on a full page. The list query travels in the query string
 * (/requests/42?status=Waiting&page=2), so "back to the list" – and the browser's Back – restore the same view.
 */
@Component({
  selector: 'app-request-full-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, RequestInfoComponent,
    RequestStatusActionsComponent, RequestHistoryComponent],
  providers: [RequestDetailsState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="head">
        <a mat-button routerLink="/" [queryParams]="backParams()" class="back">
          <mat-icon>arrow_forward</mat-icon> חזרה לרשימה
        </a>
        <h2>פנייה {{ id() }}</h2>
      </header>

      @if (state.resource.isLoading() || state.saving()) { <mat-progress-bar mode="indeterminate" /> }

      @if (id() === null || state.notFound()) {
        <section class="card message">
          <p>הפנייה לא נמצאה.</p>
          <a mat-stroked-button routerLink="/" [queryParams]="backParams()">חזרה לרשימה</a>
        </section>
      } @else if (state.resource.error(); as error) {
        <section class="card message">
          <p class="error" role="alert">{{ message(error) }}</p>
          <button mat-stroked-button (click)="state.resource.reload()">נסו שוב</button>
        </section>
      } @else if (state.resource.value(); as v) {
        @if (facts(); as f) {
          <section class="facts" aria-label="נתוני הפנייה">
            <div class="fact"><span class="value">{{ duration(f.openForMs) }}</span><span class="label">מאז שנוצרה</span></div>
            <div class="fact"><span class="value">{{ duration(f.inStatusForMs) }}</span><span class="label">בסטטוס הנוכחי</span></div>
            <div class="fact"><span class="value">{{ f.changes }}</span><span class="label">שינויי סטטוס</span></div>
          </section>
        }
        <div class="columns">
          <div class="column">
            <section class="card">
              <app-request-info [request]="v.request" />
              <div class="links">
                <a mat-stroked-button routerLink="/" [queryParams]="{ organizationName: v.request.organizationName }"
                   class="org-link">פניות נוספות של הארגון</a>
                @if (v.request.assignedTo; as handler) {
                  <a mat-stroked-button routerLink="/" [queryParams]="{ assignedTo: handler }"
                     class="handler-link">פניות נוספות של המטפל</a>
                }
              </div>
            </section>
            <section class="card">
              <app-request-status-actions [request]="v.request" [saving]="state.saving()"
                                          (change)="changeStatus(v.request, $event)" />
            </section>
          </div>
          <section class="card column">
            <app-request-history [history]="v.history" />
          </section>
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; height: 100%; overflow: auto; }
    .page { max-width: 1200px; margin: 0 auto; padding: 16px 24px 32px; display: flex; flex-direction: column; gap: 16px; }
    .head { display: flex; align-items: center; gap: 16px; }
    .head h2 { margin: 0; }
    .facts { display: flex; gap: 8px; flex-wrap: wrap; }
    .fact { display: flex; flex-direction: column; align-items: center; min-width: 120px; padding: 8px 16px;
      border-radius: 12px; background: var(--mat-sys-surface-container); }
    .fact .value { font-size: 20px; font-weight: 600; }
    .fact .label { font-size: 13px; color: var(--mat-sys-on-surface-variant); }
    .columns { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 16px; align-items: start; }
    .column { display: flex; flex-direction: column; gap: 16px; }
    .card { padding: 16px 20px; border-radius: 12px; background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant); }
    .links { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 16px; }
    .message { display: flex; flex-direction: column; align-items: flex-start; gap: 8px; }
    .error { color: #8c1d18; }
    @media (max-width: 800px) {
      .columns { grid-template-columns: minmax(0, 1fr); }
      .page { padding: 12px 16px 24px; }
    }
  `,
})
export class RequestFullPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly state = inject(RequestDetailsState);

  protected readonly id = toSignal(
    this.route.paramMap.pipe(
      map((params) => {
        const id = Number(params.get('id'));
        return Number.isInteger(id) && id > 0 ? id : null;
      }),
    ),
    { initialValue: null },
  );

  /** The list view this page was opened from (filters, sorting, page). */
  protected readonly backParams = toSignal(this.route.queryParamMap.pipe(map((params) => listParams(parseQuery(params)))), {
    initialValue: {},
  });

  protected readonly facts = computed(() => {
    const value = this.state.resource.value();
    return value ? requestFacts(value.request, value.history, new Date()) : null;
  });

  constructor() {
    this.state.bind(() => this.id());
  }

  protected duration(ms: number): string {
    return formatDuration(ms);
  }

  protected message(error: unknown): string {
    return errorMessage(error);
  }

  protected changeStatus(request: RequestDetails, newStatus: RequestStatus): void {
    this.state.changeStatus(request, newStatus);
  }
}
