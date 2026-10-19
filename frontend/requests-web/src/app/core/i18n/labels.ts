import { BulkOutcome, RequestPriority, RequestStatus } from '../api/models';

export const STATUS_LABELS: Record<RequestStatus, string> = {
  New: 'חדשה',
  InProgress: 'בטיפול',
  Waiting: 'ממתינה',
  Completed: 'הושלמה',
};

export const PRIORITY_LABELS: Record<RequestPriority, string> = {
  Low: 'נמוכה',
  Medium: 'בינונית',
  High: 'גבוהה',
};

export const OUTCOME_LABELS: Record<BulkOutcome, string> = {
  Updated: 'עודכנה',
  NotFound: 'לא נמצאה',
  Conflict: 'עודכנה בינתיים על ידי משתמש אחר',
  InvalidTransition: 'המעבר לא מותר מהסטטוס הנוכחי',
};

/** Field names as they appear in API validation errors → Hebrew. */
export const FIELD_LABELS: Record<string, string> = {
  search: 'חיפוש',
  status: 'סטטוס',
  priority: 'עדיפות',
  page: 'עמוד',
  pageSize: 'גודל עמוד',
  createdFrom: 'מתאריך',
  createdTo: 'עד תאריך',
  organizationName: 'ארגון',
  assignedTo: 'מטפל',
  sortBy: 'מיון',
  newStatus: 'סטטוס',
  'X-User-Name': 'משתמש נוכחי',
  items: 'פניות שנבחרו',
};
