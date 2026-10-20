import { StatusHistoryEntry } from '../../../core/api/models';
import { formatDuration, requestFacts } from './request-facts';

const entry = (id: number, changedAt: string): StatusHistoryEntry =>
  ({ id, previousStatus: 'New', newStatus: 'InProgress', changedAt, changedBy: 'דנה' });

describe('requestFacts', () => {
  const now = new Date('2026-10-20T12:00:00Z');

  it('counts from creation and from the latest status change, whatever the history order', () => {
    const facts = requestFacts({ createdAt: '2026-10-10T12:00:00Z' },
      [entry(2, '2026-10-18T12:00:00Z'), entry(3, '2026-10-20T09:00:00Z'), entry(1, '2026-10-11T12:00:00Z')], now);

    expect(facts).toEqual({ openForMs: 10 * 86_400_000, inStatusForMs: 3 * 3_600_000, changes: 3 });
    expect(formatDuration(facts.openForMs)).toBe('10 ימים');
    expect(formatDuration(facts.inStatusForMs)).toBe('3 שעות');
  });

  it('without history the status has not changed since creation', () => {
    const facts = requestFacts({ createdAt: '2026-10-20T11:59:30Z' }, [], now);

    expect(facts).toEqual({ openForMs: 30_000, inStatusForMs: 30_000, changes: 0 });
    expect(formatDuration(facts.openForMs)).toBe('פחות מדקה');
    expect(formatDuration(86_400_000)).toBe('יום');
    expect(formatDuration(5 * 60_000)).toBe('5 דקות');
  });
});
