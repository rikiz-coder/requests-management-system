import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import {
  Observable,
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  scan,
  shareReplay,
  startWith,
  switchMap,
} from 'rxjs';

import {
  PagedResult,
  RequestListItem,
  RequestPriority,
  RequestQuery,
  RequestStatus,
  RequestSummary,
} from '../../core/api/models';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { DEFAULT_QUERY, parseQuery, parseSelectedId, sameValue, toFilter, toQueryParams } from './request-query';

export interface LoadState<T> {
  loading: boolean;
  data: T | null;
  error: unknown | null;
}

const INITIAL: LoadState<never> = { loading: true, data: null, error: null };
export const SEARCH_DEBOUNCE_MS = 350;

/**
 * State of the requests screen.
 * - The URL is the source of truth: every action updates the query string, and the list is loaded from it
 *   (refresh, a shared link and the Back button all show the same view).
 * - Every load goes through switchMap: a newer query cancels the request that is still running
 *   (the browser shows it as "canceled", and the server cancels the SQL query via its CancellationToken).
 * - The search box is debounced, so typing sends one request after the user stops.
 */
@Injectable()
export class RequestsListStore {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(RequestsApiService);

  private readonly refresh$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  private readonly query$: Observable<RequestQuery> = this.route.queryParamMap.pipe(
    map(parseQuery),
    distinctUntilChanged(sameValue),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly query = toSignal(this.query$, { initialValue: DEFAULT_QUERY });

  readonly selectedId = toSignal(this.route.queryParamMap.pipe(map(parseSelectedId)), { initialValue: null });

  readonly list = toSignal(
    combineLatest([this.query$, this.refresh$.pipe(startWith(undefined))]).pipe(
      switchMap(([query]) => load(this.api.search(query))),
      scan(reduce<PagedResult<RequestListItem>>, INITIAL),
    ),
    { initialValue: INITIAL as LoadState<PagedResult<RequestListItem>> },
  );

  readonly summary = toSignal(
    combineLatest([
      this.query$.pipe(map(toFilter), distinctUntilChanged(sameValue)),
      this.refresh$.pipe(startWith(undefined)),
    ]).pipe(
      switchMap(([filter]) => load(this.api.summary(filter))),
      scan(reduce<RequestSummary>, INITIAL),
    ),
    { initialValue: INITIAL as LoadState<RequestSummary> },
  );

  /** Requests selected for a bulk change: id → the version the user saw. */
  private readonly _selection = signal<ReadonlyMap<number, string>>(new Map());
  readonly selection = this._selection.asReadonly();
  readonly selectionCount = computed(() => this._selection().size);

  constructor() {
    this.search$
      .pipe(
        map((term) => term.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((search) => {
        if (search !== (this.query().search ?? '')) {
          this.navigate({ search, page: 1 });
        }
      });
  }

  // ---- list actions (all of them go through the URL) ----

  setSearch(term: string): void {
    this.search$.next(term);
  }

  setFilter(patch: Partial<RequestQuery>): void {
    this.navigate({ ...patch, page: 1 });
  }

  toggleStatusFilter(status: RequestStatus): void {
    const current = this.query().status ?? [];
    const next = current.includes(status) ? current.filter((s) => s !== status) : [...current, status];
    this.setFilter({ status: next });
  }

  togglePriorityFilter(priority: RequestPriority): void {
    const current = this.query().priority ?? [];
    const next = current.includes(priority) ? current.filter((p) => p !== priority) : [...current, priority];
    this.setFilter({ priority: next });
  }

  clearFilters(): void {
    this.navigate({
      search: undefined,
      status: undefined,
      priority: undefined,
      organizationName: undefined,
      assignedTo: undefined,
      createdFrom: undefined,
      createdTo: undefined,
      page: 1,
    });
  }

  setSort(sortBy: RequestQuery['sortBy'], sortDirection: RequestQuery['sortDirection']): void {
    this.navigate({ sortBy, sortDirection, page: 1 });
  }

  setPage(page: number, pageSize: number): void {
    this.navigate({ page: pageSize === this.query().pageSize ? page : 1, pageSize });
  }

  openRequest(id: number): void {
    this.navigate({ id });
  }

  closeRequest(): void {
    this.navigate({ id: null });
  }

  /** Reload the list and the summary with the current query (after an update, or "try again"). */
  refresh(): void {
    this.refresh$.next();
  }

  // ---- bulk selection ----

  toggleSelection(item: RequestListItem): void {
    const next = new Map(this._selection());
    if (next.has(item.id)) {
      next.delete(item.id);
    } else {
      next.set(item.id, item.version);
    }
    this._selection.set(next);
  }

  setPageSelection(items: RequestListItem[], selected: boolean): void {
    const next = new Map(this._selection());
    for (const item of items) {
      if (selected) {
        next.set(item.id, item.version);
      } else {
        next.delete(item.id);
      }
    }
    this._selection.set(next);
  }

  removeFromSelection(ids: number[]): void {
    const next = new Map(this._selection());
    ids.forEach((id) => next.delete(id));
    this._selection.set(next);
  }

  clearSelection(): void {
    this._selection.set(new Map());
  }

  private navigate(patch: Partial<RequestQuery> & { id?: number | null }): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(patch),
      queryParamsHandling: 'merge',
    });
  }
}

type LoadEvent<T> = { kind: 'loading' } | { kind: 'data'; data: T } | { kind: 'error'; error: unknown };

function load<T>(source: Observable<T>): Observable<LoadEvent<T>> {
  return source.pipe(
    map((data): LoadEvent<T> => ({ kind: 'data', data })),
    catchError((error: unknown) => of<LoadEvent<T>>({ kind: 'error', error })),
    startWith<LoadEvent<T>>({ kind: 'loading' }),
  );
}

/** Keeps the previous data while a new request loads, so the table does not flash empty. */
function reduce<T>(state: LoadState<T>, event: LoadEvent<T>): LoadState<T> {
  switch (event.kind) {
    case 'loading':
      return { ...state, loading: true, error: null };
    case 'data':
      return { loading: false, data: event.data, error: null };
    case 'error':
      return { ...state, loading: false, error: event.error };
  }
}
