import { ParamMap, Params } from '@angular/router';

import {
  PRIORITIES,
  RequestFilter,
  RequestPriority,
  RequestQuery,
  RequestStatus,
  STATUSES,
  SortDirection,
  SortField,
} from '../../core/api/models';

export const DEFAULT_QUERY: RequestQuery = { sortBy: 'createdAt', sortDirection: 'desc', page: 1, pageSize: 20 };
export const PAGE_SIZES = [10, 20, 50, 100];

const SORT_FIELDS: readonly SortField[] = ['createdAt', 'updatedAt', 'priority', 'status', 'title', 'organizationName'];
const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** The URL is the source of truth for the list: query string → typed query (unknown values are ignored). */
export function parseQuery(params: ParamMap): RequestQuery {
  const status = params.getAll('status').filter((s): s is RequestStatus => STATUSES.includes(s as RequestStatus));
  const priority = params
    .getAll('priority')
    .filter((p): p is RequestPriority => PRIORITIES.includes(p as RequestPriority));
  const sortBy = params.get('sortBy') as SortField;
  const sortDirection = params.get('sortDirection') as SortDirection;
  const page = Number(params.get('page'));
  const pageSize = Number(params.get('pageSize'));
  const text = (key: string) => params.get(key)?.trim() || undefined;
  const date = (key: string) => (DATE.test(params.get(key) ?? '') ? params.get(key)! : undefined);

  return {
    search: text('search'),
    status: status.length ? status : undefined,
    priority: priority.length ? priority : undefined,
    organizationName: text('organizationName'),
    assignedTo: text('assignedTo'),
    createdFrom: date('createdFrom'),
    createdTo: date('createdTo'),
    sortBy: SORT_FIELDS.includes(sortBy) ? sortBy : DEFAULT_QUERY.sortBy,
    sortDirection: sortDirection === 'asc' || sortDirection === 'desc' ? sortDirection : DEFAULT_QUERY.sortDirection,
    page: Number.isInteger(page) && page >= 1 ? page : DEFAULT_QUERY.page,
    pageSize: PAGE_SIZES.includes(pageSize) ? pageSize : DEFAULT_QUERY.pageSize,
  };
}

export function parseSelectedId(params: ParamMap): number | null {
  const id = Number(params.get('id'));
  return Number.isInteger(id) && id > 0 ? id : null;
}

/** The filter part of the query (what the summary uses). */
export function toFilter(query: RequestQuery): RequestFilter {
  const { search, status, priority, organizationName, assignedTo, createdFrom, createdTo } = query;
  return { search, status, priority, organizationName, assignedTo, createdFrom, createdTo };
}

/** A partial query → query params for router.navigate; defaults and empty values are removed (null). */
export function toQueryParams(patch: Partial<RequestQuery> & { id?: number | null }): Params {
  const params: Params = {};
  for (const [key, value] of Object.entries(patch)) {
    const isDefault = (DEFAULT_QUERY as unknown as Record<string, unknown>)[key] === value;
    const isEmpty = value === undefined || value === null || value === '' || (Array.isArray(value) && value.length === 0);
    params[key] = isDefault || isEmpty ? null : value;
  }
  return params;
}

export function sameValue(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
