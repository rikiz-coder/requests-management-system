import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { BehaviorSubject, Observable, Subscriber, of } from 'rxjs';

import { PagedResult, RequestListItem, RequestQuery, RequestSummary } from '../../core/api/models';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { RequestsListStore, SEARCH_DEBOUNCE_MS } from './requests-list.store';

const page = (ids: number[]): PagedResult<RequestListItem> => ({
  items: ids.map((id) => ({
    id, title: `t${id}`, organizationName: 'o', status: 'New', priority: 'Low', assignedTo: null,
    createdAt: '', updatedAt: '', version: `v${id}`,
  })),
  page: 1, pageSize: 20, totalCount: ids.length, totalPages: 1,
});

const summary: RequestSummary = { total: 0, byStatus: [], byPriority: [] };

/** Search calls stay open until the test completes them, so cancellation can be observed. */
interface PendingSearch { query: RequestQuery; subscriber: Subscriber<PagedResult<RequestListItem>>; cancelled: boolean; done: boolean }

function respond(pending: PendingSearch, result: PagedResult<RequestListItem> | Error): void {
  pending.done = true;
  if (result instanceof Error) {
    pending.subscriber.error(result);
  } else {
    pending.subscriber.next(result);
    pending.subscriber.complete();
  }
}

function setup(initialParams: Params = {}) {
  const params$ = new BehaviorSubject(convertToParamMap(initialParams));
  let current: Params = { ...initialParams };
  const router = {
    navigate: vi.fn((_commands: unknown[], extras: { queryParams: Params }) => {
      current = { ...current, ...extras.queryParams };
      Object.keys(current).forEach((k) => current[k] === null && delete current[k]);
      params$.next(convertToParamMap(current));
      return Promise.resolve(true);
    }),
  };
  const searches: PendingSearch[] = [];
  const api = {
    search: vi.fn((query: RequestQuery) => new Observable<PagedResult<RequestListItem>>((subscriber) => {
      const pending: PendingSearch = { query, subscriber, cancelled: false, done: false };
      searches.push(pending);
      return () => { pending.cancelled = !pending.done; }; // unsubscribed before it answered = cancelled
    })),
    summary: vi.fn(() => of(summary)),
  };

  TestBed.configureTestingModule({
    providers: [
      RequestsListStore,
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { queryParamMap: params$.asObservable() } },
      { provide: RequestsApiService, useValue: api },
    ],
  });
  const store = TestBed.inject(RequestsListStore);
  return { store, router, api, searches, url: () => current };
}

describe('RequestsListStore', () => {
  afterEach(() => vi.useRealTimers());

  it('loads the list from the query in the URL', () => {
    const { store, searches } = setup({ status: ['Waiting', 'Unknown'], page: '3', sortBy: 'title', sortDirection: 'asc' });

    expect(store.list().loading).toBe(true);
    expect(searches).toHaveLength(1);
    expect(searches[0].query).toMatchObject({ status: ['Waiting'], page: 3, pageSize: 20, sortBy: 'title', sortDirection: 'asc' });
    expect(store.query().status).toEqual(['Waiting']);
  });

  it('debounces the search box: one navigation, after typing stops, back to page 1', () => {
    vi.useFakeTimers();
    const { store, router, url } = setup({ page: '4' });

    store.setSearch('ה');
    store.setSearch('הי');
    store.setSearch('היתר');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 1);
    expect(router.navigate).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(router.navigate).toHaveBeenCalledTimes(1);
    expect(url()).toEqual({ search: 'היתר' }); // page 1 is the default, so it is removed from the URL
  });

  it('a newer query cancels the request that is still running (switchMap)', () => {
    const { store, searches } = setup();

    store.setFilter({ status: ['New'] });

    expect(searches).toHaveLength(2);
    expect(searches[0].cancelled).toBe(true);
    expect(searches[1].cancelled).toBe(false);
    expect(searches[1].query.status).toEqual(['New']);

    respond(searches[1], page([7]));
    expect(store.list()).toMatchObject({ loading: false, error: null });
    expect(store.list().data?.items.map((i) => i.id)).toEqual([7]);
  });

  it('keeps the previous page while loading, and reports an error without losing it', () => {
    const { store, searches } = setup();
    respond(searches[0], page([1, 2]));

    store.setPage(2, 20);
    expect(store.list().loading).toBe(true);
    expect(store.list().data?.items).toHaveLength(2);

    respond(searches[1], new Error('boom'));
    expect(store.list().loading).toBe(false);
    expect(store.list().error).toBeInstanceOf(Error);
    expect(store.list().data?.items).toHaveLength(2);
  });

  it('reloads the summary only when the filters change, not on paging or sorting', () => {
    const { store, api } = setup();

    store.setPage(2, 20);
    store.setSort('title', 'asc');
    expect(api.summary).toHaveBeenCalledTimes(1);

    store.toggleStatusFilter('Waiting');
    expect(api.summary).toHaveBeenCalledTimes(2);
    expect(api.summary).toHaveBeenLastCalledWith(expect.objectContaining({ status: ['Waiting'] }));
  });

  it('a priority tile toggles the priority filter on and off', () => {
    const { store, url } = setup();

    store.togglePriorityFilter('High');
    expect(url()).toEqual({ priority: ['High'] });
    store.togglePriorityFilter('Low');
    expect(url()).toEqual({ priority: ['High', 'Low'] });
    store.togglePriorityFilter('High');
    expect(url()).toEqual({ priority: ['Low'] });
  });

  it('opening a request puts its id in the URL; refresh() reloads list and summary', () => {
    const { store, api, url } = setup();

    store.openRequest(42);
    expect(url()).toEqual({ id: 42 });
    expect(store.selectedId()).toBe(42);

    const searches = api.search.mock.calls.length;
    store.refresh();
    expect(api.search.mock.calls.length).toBe(searches + 1);
    expect(api.summary).toHaveBeenCalledTimes(2);
  });

  it('keeps the selection with the version the user saw', () => {
    const { store } = setup();
    const items = page([1, 2, 3]).items;

    store.setPageSelection(items, true);
    store.toggleSelection(items[1]);
    expect([...store.selection()]).toEqual([[1, 'v1'], [3, 'v3']]);

    store.removeFromSelection([1]);
    expect(store.selectionCount()).toBe(1);
  });
});
