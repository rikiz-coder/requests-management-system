import { Injectable, signal } from '@angular/core';

/** Tracks whether the API is reachable, so the UI shows one banner instead of an error everywhere. */
@Injectable({ providedIn: 'root' })
export class ConnectionService {
  private readonly _offline = signal(false);

  readonly offline = this._offline.asReadonly();

  markOffline(): void {
    this._offline.set(true);
  }

  markOnline(): void {
    this._offline.set(false);
  }
}

/** Status 0 = no response at all; 502/503/504 = a proxy (nginx / dev server) could not reach the API. */
export function isConnectionError(status: number): boolean {
  return status === 0 || status === 502 || status === 503 || status === 504;
}
