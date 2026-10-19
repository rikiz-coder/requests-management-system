import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import {
  BulkUpdateResult,
  Lookups,
  PagedResult,
  RequestDetails,
  RequestFilter,
  RequestListItem,
  RequestQuery,
  RequestStatus,
  RequestSummary,
  StatusHistoryEntry,
} from './models';

/** HTTP only – no state. Every filtering, sorting and paging decision is made by the server. */
@Injectable({ providedIn: 'root' })
export class RequestsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  search(query: RequestQuery): Observable<PagedResult<RequestListItem>> {
    return this.http.get<PagedResult<RequestListItem>>(`${this.base}/requests`, { params: toParams(query) });
  }

  summary(filter: RequestFilter): Observable<RequestSummary> {
    return this.http.get<RequestSummary>(`${this.base}/requests/summary`, { params: toParams(filter) });
  }

  get(id: number): Observable<RequestDetails> {
    return this.http.get<RequestDetails>(`${this.base}/requests/${id}`);
  }

  history(id: number): Observable<StatusHistoryEntry[]> {
    return this.http.get<StatusHistoryEntry[]>(`${this.base}/requests/${id}/history`);
  }

  lookups(): Observable<Lookups> {
    return this.http.get<Lookups>(`${this.base}/lookups`);
  }

  /** Optimistic concurrency: the version the user saw goes in If-Match (as an ETag). */
  updateStatus(id: number, newStatus: RequestStatus, version: string): Observable<RequestDetails> {
    return this.http
      .patch<RequestDetails>(
        `${this.base}/requests/${id}/status`,
        { newStatus },
        { headers: { 'If-Match': `"${version}"` }, observe: 'response' },
      )
      .pipe(map((response: HttpResponse<RequestDetails>) => response.body!));
  }

  bulkUpdateStatus(newStatus: RequestStatus, items: { id: number; version: string }[]): Observable<BulkUpdateResult> {
    return this.http.post<BulkUpdateResult>(`${this.base}/requests/bulk/status`, { newStatus, items });
  }
}

/** Query object → HTTP params; arrays become repeated keys (?status=New&status=Waiting). */
export function toParams(values: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === undefined || value === null || value === '') {
      continue;
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        params = params.append(key, String(item));
      }
    } else {
      params = params.set(key, String(value));
    }
  }
  return params;
}
