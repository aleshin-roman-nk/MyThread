import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, Note, Subject, today } from './api.service';
import { NoteContentComponent } from './note-content.component';
import { DateInputComponent } from './date-input.component';
import { currentClockTime, formatNoteTime, preciseClockTimePattern, serializeNoteTime } from './time-format';

@Component({
  selector: 'app-note-editor',
  imports: [FormsModule, RouterLink, DatePipe, NoteContentComponent, DateInputComponent],
  template: `
    <dialog #dialog class="panel editor note-dialog" aria-labelledby="note-dialog-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <div>
          <p class="eyebrow">{{ viewing() ? 'ПРОСМОТР' : note() ? 'РЕДАКТИРОВАНИЕ' : 'НОВАЯ ЗАПИСЬ' }}</p>
          <h2 id="note-dialog-title">{{ viewing() ? 'Заметка' : note() ? 'Изменить заметку' : 'Новая заметка' }}</h2>
        </div>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть окно" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      @if (viewing()) {
        <div class="note-meta">
          <span class="tag">{{ displayedNote()!.subjectName }}</span>
          <time [attr.datetime]="displayedNote()!.date + 'T' + displayedNote()!.time">{{ displayedNote()!.date | date:'dd.MM.yyyy' }} · {{ formatNoteTime(displayedNote()!.time) }}</time>
          <span>Оценка {{ displayedNote()!.rating }}/5</span>
        </div>
        <div class="note-dialog-body" tabindex="0" role="region" aria-label="Текст заметки">
          <app-note-content [text]="displayedNote()!.text" />
        </div>
        <div class="actions dialog-actions">
          <button type="button" (click)="viewing.set(false)">Изменить</button>
          <button class="secondary" type="button" (click)="cancelled.emit()">Закрыть</button>
        </div>
      } @else if (subjects().length === 0) {
        <p class="muted">Сначала добавьте предмет — так записи будет проще находить.</p>
        <a class="button" routerLink="/subjects">Создать предмет →</a>
      } @else {
        <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
          <div class="note-dialog-fields">
            <div>
              <label for="note-subject">Предмет</label>
              <select id="note-subject" name="subjectId" [(ngModel)]="subjectId" required [disabled]="busy()">
                <option [ngValue]="0" disabled>Выберите предмет</option>
                @for (subject of subjects(); track subject.id) {
                  <option [ngValue]="subject.id">{{ subject.name }}</option>
                }
              </select>
            </div>
            <div>
              <label for="note-date">Дата</label>
              <app-date-input inputId="note-date" name="date" [(ngModel)]="date" required [disabled]="busy()" />
            </div>
          </div>
          <div class="note-dialog-fields">
            <div>
              <label for="note-time">Время (ЧЧ:ММ)</label>
              <input id="note-time" name="time" [(ngModel)]="time" [pattern]="timePattern" required [disabled]="busy()" />
            </div>
            <div>
              <label for="note-rating">Оценка</label>
              <select id="note-rating" name="rating" [(ngModel)]="rating" [disabled]="busy()" required>
                @for (value of ratings; track value) { <option [ngValue]="value">{{ value }}</option> }
              </select>
            </div>
          </div>
          <label for="note-text">Заметка</label>
          <textarea autocomplete="off" id="note-text" name="text" [(ngModel)]="text" required maxlength="4000" rows="10" autofocus
            placeholder="Идея, набросок мысли или то, к чему хочется вернуться позже…" [disabled]="busy()"></textarea>
          <div class="field-hint">{{ text.length }} / 4000</div>
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
          <div class="actions dialog-actions">
            <button type="submit" [disabled]="form.invalid || !subjectId || !text.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить заметку' }}</button>
            <button class="secondary" type="button" (click)="cancelEdit()" [disabled]="busy()">Отмена</button>
          </div>
        </form>
      }
    </dialog>
  `
})
export class NoteEditorComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly subjects = input.required<Subject[]>();
  readonly selectedDate = input(today());
  readonly selectedSubjectId = input(0);
  readonly note = input<Note | null>(null);
  readonly readOnly = input(false);
  readonly saved = output<void>();
  readonly updated = output<Note>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly viewing = signal(false);
  readonly displayedNote = signal<Note | null>(null);
  subjectId = 0;
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
      this.displayedNote.set(note);
      this.viewing.set(!!note && this.readOnly());
      this.subjectId = note?.subjectId ?? this.selectedSubjectId();
      this.date = note?.date ?? this.selectedDate();
      this.text = note?.text ?? '';
      this.time = note ? formatNoteTime(note.time) : currentClockTime();
      this.rating = note?.rating ?? 3;
      this.error.set('');
    });
  }

  onCancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }

  cancelEdit() {
    const note = this.displayedNote();
    if (!note) { this.cancelled.emit(); return; }
    this.subjectId = note.subjectId;
    this.date = note.date;
    this.text = note.text;
    this.time = formatNoteTime(note.time);
    this.rating = note.rating;
    this.error.set('');
    this.viewing.set(true);
  }

  save() {
    if (this.busy() || !this.subjectId || !this.date || !this.text.trim()) return;
    this.busy.set(true);
    this.error.set('');
    this.api.saveNote({ subjectId: this.subjectId, date: this.date, time: serializeNoteTime(this.time), text: this.text, rating: this.rating }, this.note()?.id)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: note => {
          if (this.note()) {
            this.displayedNote.set(note);
            this.subjectId = note.subjectId;
            this.date = note.date;
            this.text = note.text;
            this.time = formatNoteTime(note.time);
            this.rating = note.rating;
            this.viewing.set(true);
            this.updated.emit(note);
          } else {
            this.text = ''; this.saved.emit();
          }
        },
        error: error => this.error.set(errorMessage(error))
      });
  }
}

