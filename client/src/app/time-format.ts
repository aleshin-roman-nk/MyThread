export const clockTimePattern = '(?:[01][0-9]|2[0-3]):[0-5][0-9]';
export const preciseClockTimePattern = clockTimePattern + '(?::[0-5][0-9](?:\\.[0-9]{1,7})?)?';

const clockTime = new RegExp('^' + clockTimePattern + '$');

export function isClockTime(value: string): boolean {
  return clockTime.test(value);
}

export function currentClockTime(): string {
  const now = new Date();
  return [now.getHours(), now.getMinutes()].map(value => String(value).padStart(2, '0')).join(':');
}

export function formatNoteTime(time: string): string {
  return time.replace(/^(\d{2}:\d{2}):00(?:\.0+)?$/, '$1');
}

export function serializeNoteTime(time: string): string {
  return time.length === 5 ? time + ':00' : time;
}
