export const clockTimePattern = '(?:[01][0-9]|2[0-3]):[0-5][0-9]';
export const preciseClockTimePattern = clockTimePattern + '(?::[0-5][0-9](?:\\.[0-9]{1,7})?)?';

const clockTime = new RegExp('^' + clockTimePattern + '$');

export function isClockTime(value: string): boolean {
  return clockTime.test(value);
}

const partialHours = /^(?:[0-9]|[01][0-9]|2[0-3])$/;
const partialMinutes = /^(?:[0-5][0-9]?)?$/;
const partialPreciseMinutes = /^(?:[0-5][0-9]?)?(?::(?:[0-5][0-9]?)?(?:\.[0-9]{0,7})?)?$/;

type TimeInputFormat = 'clock' | 'precise' | 'duration';

export function readTimeInput(event: Event, previous: string, format: TimeInputFormat): string {
  const field = event.target as HTMLInputElement;
  const inputEvent = event as InputEvent;
  if (!inputEvent.inputType?.startsWith('insert')) return field.value;

  const caret = field.selectionStart ?? field.value.length;
  // Text after the caret was outside the replaced selection.
  const end = previous.length - (field.value.length - caret);
  let start = caret - (inputEvent.data?.length ?? 0);
  if (inputEvent.data == null || start < 0 || start > end || previous.slice(0, start) !== field.value.slice(0, start)) {
    start = 0;
    while (start < Math.min(end, caret) && previous[start] === field.value[start]) start++;
  }

  const edit = insertTime(previous, start, end, field.value.slice(start, caret), format);
  if (!edit) {
    field.value = previous;
    field.setSelectionRange(start, end);
    return previous;
  }
  if (field.value !== edit.value) field.value = edit.value;
  field.setSelectionRange(edit.caret, edit.caret);
  return edit.value;
}

function insertTime(value: string, start: number, end: number, inserted: string, format: TimeInputFormat) {
  if (!/^[0-9 :]+$/.test(inserted)) return null;

  const before = value.slice(0, start);
  let after = value.slice(end);
  if (inserted === ' ' || inserted === ':') {
    if (inserted === ' ' && !before && !after) return { value: '', caret: 0 };
    if (!/[0-9]/.test(before) || before.endsWith(':')) return null;
    if (before.includes(':') && (inserted === ' ' || format !== 'precise')) return null;
    const segment = before.split(':').at(-1);
    if (before.includes(':') && segment?.length !== 2) return null;
    if (after.startsWith(':')) after = after.slice(1);
    inserted = ':';
  } else {
    inserted = inserted.trim().replace(/ *: */g, ':').replace(/ +/g, ':');
    if (!inserted) return null;
  }

  const draft = before + inserted + after;
  const caret = before.length + inserted.length;
  const duration = format === 'duration';
  const firstDigit = duration ? /^[0-9]$/ : /^[0-2]$/;
  if (firstDigit.test(draft)) return { value: draft, caret };

  const explicitHours = (duration ? /^([0-9]+):/ : /^([0-9]{1,2}):/).exec(draft)?.[1];
  const compactHourLength = duration ? Math.max(2, draft.length - 2) : /^[3-9]/.test(draft) ? 1 : 2;
  const hourLength = explicitHours?.length ?? compactHourLength;
  const hours = draft.slice(0, hourLength);
  const minutes = draft.slice(hourLength + (explicitHours ? 1 : 0));
  const hourPattern = duration ? /^[0-9]+$/ : partialHours;
  const minutePattern = format === 'precise' ? partialPreciseMinutes : partialMinutes;
  if (!hourPattern.test(hours) || !minutePattern.test(minutes)) return null;

  const padding = hours.length === 1 ? 1 : 0;
  const separator = caret >= hourLength && (!explicitHours || (!duration && caret === hourLength)) ? 1 : 0;
  return {
    value: hours.padStart(2, '0') + ':' + minutes,
    caret: caret + padding + separator
  };
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
