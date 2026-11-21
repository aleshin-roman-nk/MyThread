const dayMilliseconds = 86_400_000;
const minimumDate = Date.parse('0001-01-01');
const maximumDate = Date.parse('9999-12-31');

export const months = ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'];
export const weekdays = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];

export function dateKey(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function weekStart(date: Date): number {
  return date.getTime() - ((date.getUTCDay() + 6) % 7) * dayMilliseconds;
}

export function calendarRange(month: string) {
  const first = new Date(month + '-01');
  const last = new Date(first);
  last.setUTCMonth(last.getUTCMonth() + 1, 0);
  const start = weekStart(first);
  const end = weekStart(last) + 6 * dayMilliseconds;
  return {
    from: dateKey(new Date(Math.max(minimumDate, start))),
    through: dateKey(new Date(Math.min(maximumDate, end))),
    days: Array.from({ length: (end - start) / dayMilliseconds + 1 }, (_, index) => {
      const day = new Date(start + index * dayMilliseconds);
      return day >= first && day <= last ? { date: dateKey(day), number: day.getUTCDate() } : null;
    })
  };
}

export function weekRange(date: string) {
  const start = weekStart(new Date(date));
  return {
    from: dateKey(new Date(Math.max(minimumDate, start))),
    through: dateKey(new Date(Math.min(maximumDate, start + 6 * dayMilliseconds)))
  };
}

export function formatDuration(minutes: number): string {
  return [Math.floor(minutes / 60), minutes % 60].map(value => String(value).padStart(2, '0')).join(':');
}
