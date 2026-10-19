import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, shareReplay } from 'rxjs';

import { Lookups } from './models';
import { RequestsApiService } from './requests-api.service';

/** Handlers and organizations – loaded once and shared by the filters and the current-user picker. */
@Injectable({ providedIn: 'root' })
export class LookupsService {
  private readonly lookups$ = inject(RequestsApiService)
    .lookups()
    .pipe(
      catchError(() => of<Lookups>({ handlers: [], organizations: [] })),
      shareReplay(1),
    );

  readonly lookups = toSignal(this.lookups$, { initialValue: { handlers: [], organizations: [] } as Lookups });
}
