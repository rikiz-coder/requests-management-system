/** Types that mirror the API contract (see README – API). Enums travel as names. */

export type RequestStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';
export type RequestPriority = 'Low' | 'Medium' | 'High';
export type SortField = 'createdAt' | 'updatedAt' | 'priority' | 'status' | 'title' | 'organizationName';
export type SortDirection = 'asc' | 'desc';

export const STATUSES: readonly RequestStatus[] = ['New', 'InProgress', 'Waiting', 'Completed'];
export const PRIORITIES: readonly RequestPriority[] = ['Low', 'Medium', 'High'];

export interface RequestListItem {
  id: number;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  assignedTo: string | null;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface RequestDetails extends RequestListItem {
  allowedNextStatuses: RequestStatus[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface StatusHistoryEntry {
  id: number;
  previousStatus: RequestStatus;
  newStatus: RequestStatus;
  changedAt: string;
  changedBy: string;
}

export interface RequestSummary {
  total: number;
  byStatus: { status: RequestStatus; count: number }[];
  byPriority: { priority: RequestPriority; count: number }[];
}

export interface Lookups {
  handlers: string[];
  organizations: string[];
}

/** Filters shared by the list and the summary. */
export interface RequestFilter {
  search?: string;
  status?: RequestStatus[];
  priority?: RequestPriority[];
  organizationName?: string;
  assignedTo?: string;
  createdFrom?: string; // yyyy-MM-dd
  createdTo?: string; // yyyy-MM-dd
}

/** The full list query: filters + sorting and paging. Every field is reflected in the URL. */
export interface RequestQuery extends RequestFilter {
  sortBy: SortField;
  sortDirection: SortDirection;
  page: number;
  pageSize: number;
}

export type BulkOutcome = 'Updated' | 'NotFound' | 'Conflict' | 'InvalidTransition';

export interface BulkItemResult {
  id: number;
  outcome: BulkOutcome;
  error: string | null;
  version: string | null;
}

export interface BulkUpdateResult {
  requested: number;
  succeeded: number;
  failed: number;
  results: BulkItemResult[];
}

/** RFC 9457 Problem Details, as returned by the API on every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  // 409
  currentState?: RequestDetails;
  lastChange?: StatusHistoryEntry;
  // 422
  currentStatus?: RequestStatus;
  allowedStatuses?: RequestStatus[];
}
