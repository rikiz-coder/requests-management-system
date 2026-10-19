import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Lookups, PRIORITIES, RequestQuery, STATUSES } from '../../core/api/models';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../core/i18n/labels';

/** Presentational: shows the current query and emits changes. Debounce and loading live in the store. */
@Component({
  selector: 'app-request-filters',
  imports: [MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatAutocompleteModule,
    MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters">
      <mat-form-field class="search" appearance="outline" subscriptSizing="dynamic">
        <mat-label>חיפוש לפי מספר, כותרת או שם ארגון</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput type="search" maxlength="100" [value]="query().search ?? ''"
               (input)="searchChange.emit($any($event.target).value)" />
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>סטטוס</mat-label>
        <mat-select multiple [value]="query().status ?? []" (selectionChange)="filterChange.emit({ status: $event.value })">
          @for (s of statuses; track s) { <mat-option [value]="s">{{ statusLabels[s] }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>עדיפות</mat-label>
        <mat-select multiple [value]="query().priority ?? []" (selectionChange)="filterChange.emit({ priority: $event.value })">
          @for (p of priorities; track p) { <mat-option [value]="p">{{ priorityLabels[p] }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>מטפל</mat-label>
        <mat-select [value]="query().assignedTo ?? ''" (selectionChange)="filterChange.emit({ assignedTo: $event.value || undefined })">
          <mat-option value="">הכול</mat-option>
          @for (h of lookups().handlers; track h) { <mat-option [value]="h">{{ h }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>ארגון (מתחיל ב־)</mat-label>
        <input matInput [value]="query().organizationName ?? ''" [matAutocomplete]="orgs"
               (input)="organizationText.set($any($event.target).value)"
               (keydown.enter)="applyOrganization($any($event.target).value)"
               (blur)="applyOrganization($any($event.target).value)" />
        <mat-autocomplete #orgs (optionSelected)="applyOrganization($event.option.value)">
          @for (o of organizationOptions(); track o) { <mat-option [value]="o">{{ o }}</mat-option> }
        </mat-autocomplete>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
        <mat-label>נוצרה מתאריך</mat-label>
        <input matInput [matDatepicker]="from" [value]="toDate(query().createdFrom)"
               (dateChange)="filterChange.emit({ createdFrom: fromDate($event.value) })" />
        <mat-datepicker-toggle matIconSuffix [for]="from" />
        <mat-datepicker #from />
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
        <mat-label>עד תאריך</mat-label>
        <input matInput [matDatepicker]="to" [value]="toDate(query().createdTo)"
               (dateChange)="filterChange.emit({ createdTo: fromDate($event.value) })" />
        <mat-datepicker-toggle matIconSuffix [for]="to" />
        <mat-datepicker #to />
      </mat-form-field>

      @if (hasFilters()) {
        <button mat-button type="button" (click)="clear.emit()"><mat-icon>filter_alt_off</mat-icon> ניקוי סינון</button>
      }
    </div>
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; gap: 8px 12px; align-items: center; }
    .search { flex: 1 1 320px; }
    mat-form-field { width: 170px; }
    .search { width: auto; }
    .date { width: 165px; }
  `,
})
export class RequestFiltersComponent {
  readonly query = input.required<RequestQuery>();
  readonly lookups = input.required<Lookups>();
  readonly searchChange = output<string>();
  readonly filterChange = output<Partial<RequestQuery>>();
  readonly clear = output<void>();

  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;
  protected readonly organizationText = signal('');

  protected readonly organizationOptions = computed(() => {
    const text = this.organizationText().trim();
    const all = this.lookups().organizations;
    return (text ? all.filter((o) => o.startsWith(text)) : all).slice(0, 50);
  });

  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!(q.search || q.status?.length || q.priority?.length || q.organizationName || q.assignedTo ||
      q.createdFrom || q.createdTo);
  });

  protected applyOrganization(value: string): void {
    const organizationName = value.trim() || undefined;
    if (organizationName !== this.query().organizationName) {
      this.filterChange.emit({ organizationName });
    }
  }

  protected toDate(value: string | undefined): Date | null {
    return value ? new Date(`${value}T00:00:00`) : null;
  }

  /** Local calendar date → yyyy-MM-dd (no time zone shift). */
  protected fromDate(value: unknown): string | undefined {
    if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
      return undefined;
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }
}
