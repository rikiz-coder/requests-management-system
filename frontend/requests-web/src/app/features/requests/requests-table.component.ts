import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort, SortDirection as MatSortDirection } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';

import { PagedResult, RequestListItem, RequestPriority, RequestQuery, SortField } from '../../core/api/models';
import { errorMessage } from '../../core/http/error-messages';
import { PRIORITY_LABELS } from '../../core/i18n/labels';
import { StatusBadgeComponent } from '../../shared/status-badge.component';
import { PAGE_SIZES } from './request-query';
import { LoadState } from './requests-list.store';

/** Presentational table: renders one page from the server and emits sort / page / selection actions. */
@Component({
  selector: 'app-requests-table',
  imports: [MatTableModule, MatSortModule, MatPaginatorModule, MatProgressBarModule, MatCheckboxModule,
    MatButtonModule, DatePipe, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card" [class.loading]="state().loading">
      @if (state().loading) { <mat-progress-bar mode="indeterminate" class="bar" /> }

      @if (state().error && !state().loading) {
        <div class="message error" role="alert">
          <span>טעינת הפניות נכשלה: {{ errorText() }}</span>
          <button mat-stroked-button (click)="retry.emit()">נסו שוב</button>
        </div>
      }

      <table mat-table [dataSource]="items()" matSort [matSortActive]="query().sortBy"
             [matSortDirection]="matDirection()" (matSortChange)="onSort($event)" matSortDisableClear>
        <ng-container matColumnDef="select">
          <th mat-header-cell *matHeaderCellDef>
            <mat-checkbox [checked]="allSelected()" [indeterminate]="someSelected()"
                          (change)="pageSelection.emit($event.checked)" aria-label="בחירת כל הפניות בעמוד" />
          </th>
          <td mat-cell *matCellDef="let r">
            <mat-checkbox [checked]="selection().has(r.id)" (click)="$event.stopPropagation()"
                          (change)="toggle.emit(r)" [attr.aria-label]="'בחירת פנייה ' + r.id" />
          </td>
        </ng-container>

        <ng-container matColumnDef="id">
          <th mat-header-cell *matHeaderCellDef>מס'</th>
          <td mat-cell *matCellDef="let r">{{ r.id }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>כותרת</th>
          <td mat-cell *matCellDef="let r" class="title">{{ r.title }}</td>
        </ng-container>
        <ng-container matColumnDef="organizationName">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>ארגון</th>
          <td mat-cell *matCellDef="let r">{{ r.organizationName }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>סטטוס</th>
          <td mat-cell *matCellDef="let r"><app-status-badge [status]="r.status" /></td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>עדיפות</th>
          <td mat-cell *matCellDef="let r" [class.high]="r.priority === 'High'">{{ priorityLabel(r.priority) }}</td>
        </ng-container>
        <ng-container matColumnDef="assignedTo">
          <th mat-header-cell *matHeaderCellDef>מטפל</th>
          <td mat-cell *matCellDef="let r">{{ r.assignedTo ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>נוצרה</th>
          <td mat-cell *matCellDef="let r">{{ r.createdAt | date: 'dd/MM/yyyy HH:mm' }}</td>
        </ng-container>
        <ng-container matColumnDef="updatedAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>עודכנה</th>
          <td mat-cell *matCellDef="let r">{{ r.updatedAt | date: 'dd/MM/yyyy HH:mm' }}</td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
        <tr mat-row *matRowDef="let r; columns: columns" class="row" (click)="open.emit(r.id)"
            [class.selected]="r.id === openId()" tabindex="0" (keydown.enter)="open.emit(r.id)"></tr>
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell message" [attr.colspan]="columns.length">
            @if (!state().loading && !state().error) { לא נמצאו פניות התואמות לסינון. }
          </td>
        </tr>
      </table>

      <mat-paginator [length]="state().data?.totalCount ?? 0" [pageIndex]="query().page - 1"
                     [pageSize]="query().pageSize" [pageSizeOptions]="pageSizes" showFirstLastButtons
                     (page)="onPage($event)" />
    </div>
  `,
  styles: `
    .table-card { position: relative; background: var(--mat-sys-surface); border-radius: 12px; overflow: auto; }
    .bar { position: absolute; top: 0; inset-inline: 0; z-index: 2; }
    .loading table { opacity: 0.55; transition: opacity 120ms; }
    table { width: 100%; }
    .row { cursor: pointer; }
    .row:hover { background: var(--mat-sys-surface-container-low); }
    .row.selected { background: var(--mat-sys-secondary-container); }
    .title { max-width: 360px; }
    .high { color: #b3261e; font-weight: 600; }
    .message { padding: 24px; text-align: center; color: var(--mat-sys-on-surface-variant); }
    .message.error { display: flex; gap: 12px; align-items: center; justify-content: center; color: #8c1d18; }
  `,
})
export class RequestsTableComponent {
  readonly state = input.required<LoadState<PagedResult<RequestListItem>>>();
  readonly query = input.required<RequestQuery>();
  readonly selection = input.required<ReadonlyMap<number, string>>();
  readonly openId = input<number | null>(null);

  readonly sortChange = output<{ sortBy: SortField; sortDirection: 'asc' | 'desc' }>();
  readonly pageChange = output<{ page: number; pageSize: number }>();
  readonly open = output<number>();
  readonly toggle = output<RequestListItem>();
  readonly pageSelection = output<boolean>();
  readonly retry = output<void>();

  protected readonly columns = ['select', 'id', 'title', 'organizationName', 'status', 'priority', 'assignedTo',
    'createdAt', 'updatedAt'];
  protected readonly pageSizes = PAGE_SIZES;

  protected readonly items = computed(() => this.state().data?.items ?? []);
  protected readonly matDirection = computed<MatSortDirection>(() => this.query().sortDirection);
  protected readonly errorText = computed(() => errorMessage(this.state().error));
  protected readonly allSelected = computed(() => {
    const items = this.items();
    return items.length > 0 && items.every((i) => this.selection().has(i.id));
  });
  protected readonly someSelected = computed(
    () => !this.allSelected() && this.items().some((i) => this.selection().has(i.id)),
  );

  protected priorityLabel(priority: RequestPriority): string {
    return PRIORITY_LABELS[priority];
  }

  protected onSort(sort: Sort): void {
    this.sortChange.emit({ sortBy: sort.active as SortField, sortDirection: sort.direction === 'asc' ? 'asc' : 'desc' });
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
