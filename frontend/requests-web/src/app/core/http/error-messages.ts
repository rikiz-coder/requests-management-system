import { HttpErrorResponse } from '@angular/common/http';

import { ProblemDetails } from '../api/models';
import { FIELD_LABELS, STATUS_LABELS } from '../i18n/labels';
import { isConnectionError } from './connection.service';

/** Signal resources wrap non-Error values (like HttpErrorResponse); unwrap them. */
function unwrap(error: unknown): unknown {
  return error instanceof HttpErrorResponse ? error : ((error as { cause?: unknown } | null)?.cause ?? error);
}

/** True for a 404 from the API (also when wrapped by a signal resource). */
export function isNotFound(error: unknown): boolean {
  error = unwrap(error);
  return error instanceof HttpErrorResponse && error.status === 404;
}

export function problemOf(error: unknown): ProblemDetails | null {
  error = unwrap(error);
  return error instanceof HttpErrorResponse && error.error && typeof error.error === 'object'
    ? (error.error as ProblemDetails)
    : null;
}

/** A specific Hebrew message built from what the server returned. */
export function errorMessage(error: unknown): string {
  error = unwrap(error);
  if (!(error instanceof HttpErrorResponse)) {
    return 'אירעה שגיאה לא צפויה.';
  }
  if (isConnectionError(error.status)) {
    return 'אין חיבור לשרת.';
  }
  const problem = problemOf(error);
  switch (error.status) {
    case 400: {
      const fields = Object.keys(problem?.errors ?? {}).map((f) => {
        const name = f.replace(/\[\d+\].*$/, '');
        return FIELD_LABELS[name] ?? name;
      });
      return fields.length ? `נתונים לא תקינים: ${[...new Set(fields)].join(', ')}.` : 'הבקשה לא תקינה.';
    }
    case 404:
      return 'הפנייה לא נמצאה.';
    case 409:
      return 'הפנייה עודכנה בינתיים על ידי משתמש אחר.';
    case 422: {
      const allowed = (problem?.allowedStatuses ?? []).map((s) => STATUS_LABELS[s]).join(', ');
      const current = problem?.currentStatus ? STATUS_LABELS[problem.currentStatus] : '';
      return `המעבר לא מותר מהסטטוס "${current}".` + (allowed ? ` מותר: ${allowed}.` : '');
    }
    case 428:
      return 'חסרה גרסת הפנייה. רעננו את הפנייה ונסו שוב.';
    default:
      return `תקלה בשרת. קוד לפנייה לתמיכה: ${problem?.traceId ?? error.status}.`;
  }
}
