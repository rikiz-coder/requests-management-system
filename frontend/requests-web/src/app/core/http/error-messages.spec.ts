import { HttpErrorResponse } from '@angular/common/http';

import { errorMessage } from './error-messages';

const http = (status: number, error: object | null = null) => new HttpErrorResponse({ status, error });

describe('errorMessage', () => {
  it('names the invalid fields in Hebrew', () => {
    expect(errorMessage(http(400, { errors: { pageSize: ['x'], 'status[0]': ['y'] } }))).toBe('נתונים לא תקינים: גודל עמוד, status.');
  });

  it('explains a forbidden transition with the allowed statuses', () => {
    expect(errorMessage(http(422, { currentStatus: 'Completed', allowedStatuses: ['InProgress'] })))
      .toBe('המעבר לא מותר מהסטטוס "הושלמה". מותר: בטיפול.');
  });

  it('treats "no response" and proxy errors as a connection problem', () => {
    expect(errorMessage(http(0))).toBe('אין חיבור לשרת.');
    expect(errorMessage(http(504))).toBe('אין חיבור לשרת.');
  });

  it('gives a support code for server errors, and unwraps resource errors', () => {
    expect(errorMessage(http(500, { traceId: 'abc' }))).toContain('abc');
    expect(errorMessage({ cause: http(404) })).toBe('הפנייה לא נמצאה.');
  });
});
