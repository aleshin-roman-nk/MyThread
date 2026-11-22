import { booleanAttribute, Component, computed, ElementRef, forwardRef, input, signal, viewChild } from '@angular/core';
import { AbstractControl, ControlValueAccessor, NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidationErrors, Validator } from '@angular/forms';
import { clockTimePattern, currentClockTime, preciseClockTimePattern, readTimeInput } from './time-format';

const clockTime = new RegExp('^' + clockTimePattern + '$');
const preciseClockTime = new RegExp('^' + preciseClockTimePattern + '$');
const outerRadius = 42;
const innerRadius = 27;
const minuteNumberRadius = 37;
const minuteTickRadius = 44;

function dialMarks(values: number[], units: number, radius: number) {
  return values.map(value => {
    const angle = value * 2 * Math.PI / units;
    return { value, x: 50 + radius * Math.sin(angle), y: 50 - radius * Math.cos(angle) };
  });
}

const hourPositions = Array.from({ length: 12 }, (_, index) => index + 1);
const hourMarks = [
  ...dialMarks(hourPositions, 12, outerRadius),
  ...dialMarks(hourPositions.map(hour => (hour + 12) % 24), 12, innerRadius)
];
const minuteTicks = Array.from({ length: 60 }, (_, index) => index);
const minuteMarks = dialMarks(minuteTicks.filter(minute => minute % 5 === 0), 60, minuteNumberRadius);

@Component({
  selector: 'app-time-input',
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TimeInputComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => TimeInputComponent), multi: true }
  ],
  templateUrl: './time-input.component.html',
  styleUrl: './time-input.component.scss'
})
export class TimeInputComponent implements ControlValueAccessor, Validator {
  readonly inputId = input.required<string>();
  readonly required = input(false, { transform: booleanAttribute });
  readonly precise = input(false, { transform: booleanAttribute });

  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('picker');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly minuteMode = viewChild.required<ElementRef<HTMLButtonElement>>('minuteMode');

  readonly text = signal('');
  readonly disabled = signal(false);
  readonly mode = signal<'hours' | 'minutes'>('hours');
  readonly hour = signal(0);
  readonly minute = signal(0);
  readonly hourMarks = hourMarks;
  readonly minuteMarks = minuteMarks;
  readonly minuteTicks = minuteTicks;
  readonly handAngle = computed(() => this.mode() === 'hours' ? this.hour() * 30 : this.minute() * 6);
  readonly handLength = computed(() => {
    if (this.mode() === 'minutes') return this.minute() % 5 === 0 ? minuteNumberRadius : minuteTickRadius;
    return this.hour() === 0 || this.hour() > 12 ? innerRadius : outerRadius;
  });

  private readonly timePattern = computed(() => this.precise() ? preciseClockTime : clockTime);
  readonly invalid = computed(() => this.text() !== '' && !this.timePattern().test(this.text()));
  private onChange: (value: string) => void = () => {};
  onTouched: () => void = () => {};

  writeValue(value: string | null) { this.text.set(value ?? ''); }
  registerOnChange(fn: (value: string) => void) { this.onChange = fn; }
  registerOnTouched(fn: () => void) { this.onTouched = fn; }
  setDisabledState(disabled: boolean) {
    this.disabled.set(disabled);
    if (disabled) this.dialog()?.nativeElement.close();
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const value: string = control.value ?? '';
    return value && !this.timePattern().test(value) ? { clockTime: true } : null;
  }

  onInput(event: Event) {
    this.updateValue(readTimeInput(event, this.text(), this.precise() ? 'precise' : 'clock'));
  }

  openPicker() {
    const time = this.text() && !this.invalid() ? this.text() : currentClockTime();
    this.hour.set(Number(time.slice(0, 2)));
    this.minute.set(Number(time.slice(3, 5)));
    this.mode.set('hours');
    this.dialog()?.nativeElement.showModal();
  }

  closePicker(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.dialog()?.nativeElement.close();
  }

  onPickerClosed() {
    this.onTouched();
    this.trigger().nativeElement.focus();
  }

  selectHour(hour: number) {
    this.hour.set(hour);
    this.mode.set('minutes');
    this.minuteMode().nativeElement.focus();
  }

  selectMinute(event: MouseEvent) {
    const dial = event.currentTarget as HTMLElement;
    const { left, top, width, height } = dial.getBoundingClientRect();
    const x = event.clientX - left - width / 2;
    const y = event.clientY - top - height / 2;
    if (Math.hypot(x, y) < 8) return;
    const angle = Math.atan2(x, -y);
    this.minute.set((Math.round(angle * 30 / Math.PI) + 60) % 60);
  }

  selectMinuteMark(event: MouseEvent, minute: number) {
    event.stopPropagation();
    this.minute.set(minute);
  }

  adjustMinute(event: KeyboardEvent) {
    const minute = this.minute();
    let next: number;
    switch (event.key) {
      case 'ArrowUp':
      case 'ArrowRight':
        next = Math.min(minute + 1, 59);
        break;
      case 'ArrowDown':
      case 'ArrowLeft':
        next = Math.max(minute - 1, 0);
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = 59;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.minute.set(next);
  }

  applyTime() {
    const seconds = this.precise() && !this.invalid() ? this.text().slice(5) : '';
    this.updateValue(`${this.pad(this.hour())}:${this.pad(this.minute())}${seconds}`);
    this.closePicker();
  }

  pad(value: number) { return String(value).padStart(2, '0'); }

  private updateValue(value: string) {
    this.text.set(value);
    this.onChange(value);
  }
}
