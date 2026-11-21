export type StatusFilter = 0 | 1 | 2 | 'all';

export function readStatusFilter(key: string): StatusFilter {
  try {
    const value = localStorage.getItem(key);
    if (value === 'all') return value;
    if (value === '0' || value === '1' || value === '2') return Number(value) as 0 | 1 | 2;
  } catch {
    // Filtering must still work when browser storage is unavailable.
  }
  return 0;
}

export function writeStatusFilter(key: string, value: StatusFilter): void {
  try {
    localStorage.setItem(key, String(value));
  } catch {
    // Keep the current selection in memory when browser storage is unavailable.
  }
}
