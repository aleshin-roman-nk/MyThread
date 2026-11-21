import { Component, ElementRef, forwardRef, input, viewChild } from '@angular/core';
import { AbstractControl, ControlValueAccessor, NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidationErrors, Validator } from '@angular/forms';

function parseDate(value: string): string | null {
  const match = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(value);
  if (!match || Number(match[3]) < 1) return null;
  const iso = `${match[3]}-${match[2]}-${match[1]}`;
  const date = new Date(`${iso}T12:00:00`);
  return Number.isFinite(date.getTime()) && date.getFullYear() === Number(match[3])
    && date.getMonth() + 1 === Number(match[2]) && date.getDate() === Number(match[1]) ? iso : null;
}

@Component({
  selector: 'app-date-input',
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DateInputComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => DateInputComponent), multi: true }
  ],
  styles: `
    :host { display: block; min-width: 0; }
    .date-field { position: relative; }
    .date-text { padding-right: 44px; }
    .calendar-button { position: absolute; right: 2px; top: 2px; bottom: 2px; width: 40px; padding: 9px; color: #496642; background: transparent; }
    .calendar-button:hover { background: #e9eddf; }
    svg { width: 20px; height: 20px; }
    .native-calendar { position: absolute; left: 0; bottom: 0; width: 1px; height: 1px; padding: 0; opacity: 0; pointer-events: none; }
  `,
  template: `
    <div class="date-field">
      <input class="date-text" [id]="inputId()" type="text" autocomplete="off" placeholder="дд.мм.гггг" maxlength="10"
        [value]="text" [disabled]="disabled" [attr.aria-invalid]="text && !validDate() ? 'true' : null" (input)="onInput($event)" (blur)="onTouched()">
      <button class="calendar-button" type="button" aria-label="Выбрать дату" title="Выбрать дату" [disabled]="disabled" (click)="openCalendar()">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18"/></svg>
      </button>
      <input #calendar class="native-calendar" type="date" tabindex="-1" aria-hidden="true" [value]="validDate() ?? ''" [disabled]="disabled" (change)="onPick($event)">
    </div>
  `
})
export class DateInputComponent implements ControlValueAccessor, Validator {
  readonly inputId = input.required<string>();
  private readonly calendar = viewChild.required<ElementRef<HTMLInputElement>>('calendar');
  text = '';
  disabled = false;
  private onChange: (value: string) => void = () => {};
  onTouched: () => void = () => {};

  validDate() { return parseDate(this.text); }
  writeValue(value: string | null) {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
    this.text = match ? `${match[3]}.${match[2]}.${match[1]}` : '';
  }
  registerOnChange(fn: (value: string) => void) { this.onChange = fn; }
  registerOnTouched(fn: () => void) { this.onTouched = fn; }
  setDisabledState(disabled: boolean) { this.disabled = disabled; }
  validate(_: AbstractControl): ValidationErrors | null { return this.text && !this.validDate() ? { date: true } : null; }
  onInput(event: Event) {
    this.text = (event.target as HTMLInputElement).value;
    this.onChange(this.validDate() ?? '');
  }
  onPick(event: Event) {
    this.writeValue((event.target as HTMLInputElement).value);
    this.onChange(this.validDate() ?? ''); this.onTouched();
  }
  openCalendar() {
    if (this.disabled) return;
    this.onTouched();
    const calendar = this.calendar().nativeElement;
    if (calendar.showPicker) calendar.showPicker();
    else calendar.click();
  }
}
