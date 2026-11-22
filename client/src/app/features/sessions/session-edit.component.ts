import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { DateInputComponent } from '../../shared/date-input.component';
import { TimeInputComponent } from '../../shared/time-input.component';
import { isClockTime } from '../../shared/time-format';
import { Session, SessionsApi } from './sessions-api';

function localParts(value: string) {
  const date = new Date(value);
  const pad = (number: number) => String(number).padStart(2, '0');
  return { date: `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`, time: `${pad(date.getHours())}:${pad(date.getMinutes())}` };
}

function parseLocal(date: string, time: string, original: string | null): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || !isClockTime(time)) return null;
  // Text-only edits preserve the original seconds and UTC offset.
  if (original) {
    const parts = localParts(original);
    if (parts.date === date && parts.time === time) return new Date(original);
  }
  const parsed = new Date(`${date}T${time}:00`);
  if (!Number.isFinite(parsed.getTime())) return null;
  const parts = localParts(parsed.toISOString());
  return parts.date === date && parts.time === time ? parsed : null;
}

@Component({
  selector: 'app-session-edit',
  imports: [FormsModule, DateInputComponent, TimeInputComponent],
  styles: `
    .actions { margin-top: 20px; }
    .date-time-fields { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); gap: 16px; }
    .date-time-fields > div { min-width: 0; }
    .clear-end { margin-top: 8px; }
  `,
  template: `
    <dialog #dialog class="panel subject-settings-dialog" aria-labelledby="session-edit-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="session-edit-title">Изменить сессию</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть правку сессии" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <div class="date-time-fields">
          <div><label for="session-start-date">Дата начала</label><app-date-input inputId="session-start-date" name="startDate" [(ngModel)]="startDate" required [disabled]="busy()" /></div>
          <div><label for="session-start-time">Время начала</label><app-time-input inputId="session-start-time" name="startTime" [(ngModel)]="startTime" required [disabled]="busy()" /></div>
        </div>
        <div class="date-time-fields">
          <div><label for="session-end-date">Дата завершения</label><app-date-input inputId="session-end-date" name="endDate" [(ngModel)]="endDate" [required]="!!endDate || !!endTime" [disabled]="busy()" /></div>
          <div><label for="session-end-time">Время завершения</label><app-time-input inputId="session-end-time" name="endTime" [(ngModel)]="endTime" [required]="!!endDate || !!endTime" [disabled]="busy()" /></div>
        </div>
        <p class="field-hint">Местное время в формате ЧЧ:ММ, 24 часа. Пустое завершение означает, что сессия открыта.</p>
        @if (endDate || endTime) { <button class="text-button clear-end" type="button" (click)="endDate = ''; endTime = ''" [disabled]="busy()">Очистить завершение</button> }
        <label for="session-text">Текст сессии</label>
        <textarea autocomplete="off" id="session-text" name="text" [(ngModel)]="text" maxlength="1000" rows="5" [disabled]="busy()"></textarea>
        <p class="field-hint">{{ text.length }} / 1000</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить изменения' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class SessionEditComponent {
  private readonly api = inject(SessionsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly session = input.required<Session>();
  readonly saved = output<Session>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  startDate = '';
  startTime = '';
  endDate = '';
  endTime = '';
  text = '';

  constructor() {
    let previousOverflow: string | undefined;
    afterNextRender(() => {
      this.dialog().nativeElement.showModal();
      previousOverflow = this.document.body.style.overflow;
      this.document.body.style.overflow = 'hidden';
    });
    this.destroyRef.onDestroy(() => {
      this.dialog().nativeElement.close();
      if (previousOverflow !== undefined) this.document.body.style.overflow = previousOverflow;
    });
    effect(() => {
      const session = this.session();
      const start = localParts(session.startTime);
      const end = session.endTime ? localParts(session.endTime) : { date: '', time: '' };
      this.startDate = start.date; this.startTime = start.time;
      this.endDate = end.date; this.endTime = end.time; this.text = session.text;
    });
  }

  onCancel(event: Event) { event.preventDefault(); if (!this.busy()) this.cancelled.emit(); }
  save() {
    if (this.busy()) return;
    const start = parseLocal(this.startDate, this.startTime, this.session().startTime);
    const hasEnd = !!this.endDate || !!this.endTime;
    const end = hasEnd ? parseLocal(this.endDate, this.endTime, this.session().endTime) : null;
    if (!start || (hasEnd && !end)) {
      this.error.set('Укажите корректные дату и время.'); return;
    }
    if (end && end < start) { this.error.set('Завершение не может быть раньше начала.'); return; }
    if (this.text.length > 1000) return;
    this.busy.set(true); this.error.set('');
    this.api.updateSession(this.session().id, { startTime: start.toISOString(), endTime: end?.toISOString() ?? null, text: this.text })
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: session => this.saved.emit(session), error: error => this.error.set(errorMessage(error)) });
  }
}
