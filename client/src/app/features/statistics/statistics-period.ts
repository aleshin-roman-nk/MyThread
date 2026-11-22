import { dateKey } from '../../shared/calendar';

export function monthBounds(month: string) {
  const from = month + '-01';
  const last = new Date(from);
  last.setUTCMonth(last.getUTCMonth() + 1, 0);
  return { from, through: dateKey(last) };
}
