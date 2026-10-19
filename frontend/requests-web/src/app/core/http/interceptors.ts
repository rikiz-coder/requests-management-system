import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';

import { CurrentUserService } from '../user/current-user.service';
import { ConnectionService, isConnectionError } from './connection.service';

/**
 * Adds X-User-Name to every API call. HTTP header values are ASCII, so the (Hebrew) name is
 * URL-encoded; the API decodes it.
 */
export const userHeaderInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(CurrentUserService).name();
  if (!user || !req.url.startsWith('/api')) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'X-User-Name': encodeURIComponent(user) } }));
};

/** Detects "server unreachable" and recovery, for the connection banner. */
export const connectionInterceptor: HttpInterceptorFn = (req, next) => {
  const connection = inject(ConnectionService);
  return next(req).pipe(
    tap((event) => {
      if (event instanceof HttpResponse) {
        connection.markOnline();
      }
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (isConnectionError(error.status)) {
          connection.markOffline();
        } else {
          connection.markOnline(); // the server answered, even if with an error
        }
      }
      return throwError(() => error);
    }),
  );
};
