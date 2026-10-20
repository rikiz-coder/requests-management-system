import { RequestListItem, StatusHistoryEntry } from '../../../core/api/models';

export interface RequestFacts {
  /** Time since the request was created. */
  openForMs: number;
  /** Time since the last status change (or since creation, if the status never changed). */
  inStatusForMs: number;
  /** Number of status changes. */
  changes: number;
}

/** Facts computed from the request and its (already loaded) history – nothing extra is fetched. */
export function requestFacts(request: Pick<RequestListItem, 'createdAt'>, history: StatusHistoryEntry[], now: Date): RequestFacts {
  const created = Date.parse(request.createdAt);
  const lastChange = history.reduce((latest, h) => Math.max(latest, Date.parse(h.changedAt)), created);
  return {
    openForMs: Math.max(0, now.getTime() - created),
    inStatusForMs: Math.max(0, now.getTime() - lastChange),
    changes: history.length,
  };
}

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** A duration in Hebrew, in its largest whole unit: "3 ימים", "שעה", "12 דקות", "פחות מדקה". */
export function formatDuration(ms: number): string {
  const unit = (n: number, one: string, many: string) => (n === 1 ? one : `${n} ${many}`);
  if (ms >= DAY) {
    return unit(Math.floor(ms / DAY), 'יום', 'ימים');
  }
  if (ms >= HOUR) {
    return unit(Math.floor(ms / HOUR), 'שעה', 'שעות');
  }
  if (ms >= MINUTE) {
    return unit(Math.floor(ms / MINUTE), 'דקה', 'דקות');
  }
  return 'פחות מדקה';
}
