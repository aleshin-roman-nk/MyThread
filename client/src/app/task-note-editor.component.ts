import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, ProjectTask, TaskNote, today } from './api.service';
import { NoteContentComponent } from './note-content.component';
import { DateInputComponent } from './date-input.component';
import { currentClockTime, formatNoteTime, preciseClockTimePattern, serializeNoteTime } from './time-format';

@Component({
  selector: 'app-task-note-editor',
  imports: [DatePipe, FormsModule, NoteContentComponent, DateInputComponent],
  styles: `
    .note-reading { border: 1px solid #a6b39e; border-radius: 8px; background: #eef0e7; padding: 18px; }
    .note-title-date { white-space: nowrap; }
  `,
  template: `
    <dialog #dialog class="panel editor note-dialog" aria-labelledby="task-note-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <div>
          <h2 id="task-note-title">{{ viewing() ? 'Заметка задачи' : note() ? 'Изменить заметку' : 'Новая заметка' }}@if (viewing()) { <span class="note-title-date"> \\ <time [attr.datetime]="displayedNote()!.date">{{ displayedNote()!.date | date:'dd.MM.yyyy' }}</time></span> }</h2>
          <p class="muted">{{ task().name }}</p>
        </div>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть заметку задачи" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      @if (viewing()) {
        <div class="note-meta">
          <time [attr.datetime]="displayedNote()!.time">{{ formatNoteTime(displayedNote()!.time) }}</time>
          <span>Оценка {{ displayedNote()!.rating }}/5</span>
        </div>
        <div class="note-dialog-body note-reading" tabindex="0" role="region" aria-label="Текст заметки">
          <app-note-content [text]="displayedNote()!.text" />
        </div>
        <div class="actions dialog-actions">
          <button type="button" (click)="viewing.set(false)">Изменить</button>
          <button class="secondary" type="button" (click)="cancelled.emit()">Закрыть</button>
        </div>
      } @else {
        <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
          <label for="task-note-date">Дата</label>
          <app-date-input inputId="task-note-date" name="date" [(ngModel)]="date" required [disabled]="busy()" />
          <div class="note-dialog-fields">
            <div>
              <label for="task-note-time">Время (ЧЧ:ММ)</label>
              <input id="task-note-time" name="time" [(ngModel)]="time" [pattern]="timePattern" required [disabled]="busy()" />
            </div>
            <div>
              <label for="task-note-rating">Оценка</label>
              <select id="task-note-rating" name="rating" [(ngModel)]="rating" [disabled]="busy()" required>
                @for (value of ratings; track value) { <option [ngValue]="value">{{ value }}</option> }
              </select>
            </div>
          </div>
          <label for="task-note-text">Заметка</label>
          <textarea autocomplete="off" id="task-note-text" name="text" [(ngModel)]="text" required maxlength="4000" rows="10" [disabled]="busy()"></textarea>
          <p class="field-hint">{{ text.length }} / 4000</p>
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
          <div class="actions dialog-actions">
            <button type="submit" [disabled]="form.invalid || !text.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить заметку' }}</button>
            <button class="secondary" type="button" (click)="cancelEdit()" [disabled]="busy()">Отмена</button>
          </div>
        </form>
      }
    </dialog>
  `
})
export class TaskNoteEditorComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly task = input.required<ProjectTask>();
  readonly note = input<TaskNote | null>(null);
  readonly readOnly = input(false);
  readonly saved = output<void>();
  readonly updated = output<TaskNote>();
  readonly cancelled = output<void>();
  readonly displayedNote = signal<TaskNote | null>(null);
  readonly viewing = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  date = today();
  text = '';
  time = currentClockTime();
  rating = 3;
  readonly timePattern = preciseClockTimePattern;
  readonly ratings = [1, 2, 3, 4, 5];
  readonly formatNoteTime = formatNoteTime;

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
      const note = this.note();
      this.displayedNote.set(note); this.viewing.set(!!note && this.readOnly());
      this.date = note?.date ?? today(); this.text = note?.text ?? ''; this.error.set('');
      this.time = note ? formatNoteTime(note.time) : currentClockTime();
      this.rating = note?.rating ?? 3;
    });
  }
  onCancel(event: Event) { event.preventDefault(); if (!this.busy()) this.cancelled.emit(); }
  cancelEdit() {
    const note = this.displayedNote();
    if (!note) { this.cancelled.emit(); return; }
    this.date = note.date; this.text = note.text; this.error.set(''); this.viewing.set(true);
    this.time = formatNoteTime(note.time);
    this.rating = note.rating;
  }
  save() {
    if (this.busy() || !this.date || !this.text.trim() || this.text.length > 4000) return;
    this.busy.set(true); this.error.set('');
    this.api.saveTaskNote(this.task().projectId, this.task().id, { date: this.date, time: serializeNoteTime(this.time), text: this.text, rating: this.rating }, this.note()?.id)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef)).subscribe({
        next: note => {
          if (this.note()) {
            this.displayedNote.set(note); this.date = note.date; this.text = note.text;
            this.time = formatNoteTime(note.time);
            this.rating = note.rating;
            this.viewing.set(true); this.updated.emit(note);
          } else this.saved.emit();
        },
        error: error => this.error.set(errorMessage(error))
      });
  }
}
