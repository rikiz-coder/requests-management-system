import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar } from '@angular/material/snack-bar';

import { LookupsService } from '../../core/api/lookups.service';
import { RequestStatus } from '../../core/api/models';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { errorMessage } from '../../core/http/error-messages';
import { CurrentUserService } from '../../core/user/current-user.service';
import { BulkActionsBarComponent } from './bulk-actions-bar.component';
import { BulkResultDialogComponent, BulkResultDialogData } from './bulk-result-dialog.component';
import { RequestDetailsComponent } from './request-details.component';
import { RequestFiltersComponent } from './request-filters.component';
import { RequestsListStore } from './requests-list.store';
import { RequestsTableComponent } from './requests-table.component';
import { SummaryPanelComponent } from './summary-panel.component';

/** Container: connects the store to the presentational components. */
@Component({
  selector: 'app-requests-page',
  imports: [MatSidenavModule, SummaryPanelComponent, RequestFiltersComponent, RequestsTableComponent,
    BulkActionsBarComponent, RequestDetailsComponent],
  providers: [RequestsListStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-drawer-container class="container" [hasBackdrop]="true" (backdropClick)="store.closeRequest()">
      <mat-drawer mode="over" position="end" class="drawer" [opened]="store.selectedId() !== null"
                  (closedStart)="store.selectedId() !== null && store.closeRequest()">
        @if (store.selectedId(); as id) {
          <app-request-details [requestId]="id" (close)="store.closeRequest()" (changed)="store.refresh()" />
        }
      </mat-drawer>

      <mat-drawer-content class="content">
        <app-summary-panel [state]="store.summary()" [activeStatuses]="store.query().status ?? []"
                           (statusClick)="store.toggleStatusFilter($event)" />

        <app-request-filters [query]="store.query()" [lookups]="lookups.lookups()"
                             (searchChange)="store.setSearch($event)" (filterChange)="store.setFilter($event)"
                             (clear)="store.clearFilters()" />

        @if (store.selectionCount() > 0) {
          <app-bulk-actions-bar [count]="store.selectionCount()" [busy]="bulkBusy()" [canSubmit]="!!user.name()"
                                (submit)="bulkUpdate($event)" (clear)="store.clearSelection()" />
        }

        <app-requests-table [state]="store.list()" [query]="store.query()" [selection]="store.selection()"
                            [openId]="store.selectedId()"
                            (sortChange)="store.setSort($event.sortBy, $event.sortDirection)"
                            (pageChange)="store.setPage($event.page, $event.pageSize)"
                            (open)="store.openRequest($event)" (toggle)="store.toggleSelection($event)"
                            (pageSelection)="store.setPageSelection(store.list().data?.items ?? [], $event)"
                            (retry)="store.refresh()" />
      </mat-drawer-content>
    </mat-drawer-container>
  `,
  styles: `
    :host { display: block; height: 100%; }
    /* clip (not hidden): the closed drawer sits outside the viewport, and 'hidden' still lets focus or
       scrollIntoView scroll the container sideways – 'clip' forbids any scrolling. */
    .container { height: 100%; background: transparent; overflow: clip; }
    .content { display: flex; flex-direction: column; gap: 16px; padding: 16px 24px; overflow-x: hidden; }
    .drawer { width: min(460px, 92vw); }
  `,
})
export class RequestsPageComponent {
  protected readonly store = inject(RequestsListStore);
  protected readonly lookups = inject(LookupsService);
  protected readonly user = inject(CurrentUserService);
  private readonly api = inject(RequestsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly bulkBusy = signal(false);

  protected bulkUpdate(newStatus: RequestStatus): void {
    const items = [...this.store.selection()].map(([id, version]) => ({ id, version }));
    this.bulkBusy.set(true);
    this.api.bulkUpdateStatus(newStatus, items).subscribe({
      next: (result) => {
        this.bulkBusy.set(false);
        this.store.removeFromSelection(result.results.filter((r) => r.outcome === 'Updated').map((r) => r.id));
        this.store.refresh();
        const text = result.failed === 0
          ? `עודכנו ${result.succeeded} פניות`
          : `עודכנו ${result.succeeded} פניות; לא עודכנו ${result.failed}`;
        this.snackBar.open(text, undefined, { duration: 4000 });
        if (result.failed > 0) {
          this.dialog
            .open<BulkResultDialogComponent, BulkResultDialogData, number>(BulkResultDialogComponent, {
              data: { result, newStatus },
              width: '520px',
            })
            .afterClosed()
            .subscribe((id) => id && this.store.openRequest(id));
        }
      },
      error: (error: unknown) => {
        this.bulkBusy.set(false);
        this.snackBar.open(errorMessage(error), 'סגירה', { duration: 6000 });
      },
    });
  }
}
