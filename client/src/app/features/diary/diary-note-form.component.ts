import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { DateInputComponent } from '../../shared/date-input.component';
import { TimeInputComponent } from '../../shared/time-input.component';
import { DiaryApi, DiaryNote, SaveDiaryNote } from './diary-api';

export type DiaryDraft = SaveDiaryNote & { id?: number };

export function newDiaryDraft(date: string): DiaryDraft {
  const now = new Date();
  const time = [now.getHours(), now.getMinutes()].map(value => String(value).padStart(2, '0')).join(':');
  return { date, time, text: '', rating: 3 };
}

@Component({
  selector: 'app-diary-note-form',
  imports: [DateInputComponent, TimeInputComponent, FormsModule],
  styles: `
    .fields { display: flex; flex-wrap: wrap; gap: 16px; margin-bottom: 16px; }
    .fields > div { flex: 1 1 130px; }
    .actions { margin-top: 16px; }
  `,
  template: `
    @let value = draft();
    <form #form="ngForm" (ngSubmit)="form.valid && save(value)">
      <div class="fields">
        <div>
          <label for="note-date">Дата</label>
          <app-date-input inputId="note-date" name="date" [(ngModel)]="value.date" [disabled]="saving()" required />
        </div>
        <div>
          <label for="note-time">Время (ЧЧ:ММ)</label>
          <app-time-input inputId="note-time" name="time"
            [(ngModel)]="value.time" [disabled]="saving()" required precise />
        </div>
        <div>
          <label for="note-rating">Оценка</label>
          <select id="note-rating" name="rating" [(ngModel)]="value.rating" [disabled]="saving()">
            @for (rating of ratings; track rating) { <option [ngValue]="rating">{{ rating }}</option> }
          </select>
        </div>
      </div>
      <label for="note-text">Текст заметки</label>
      <textarea #noteText id="note-text" name="text" [(ngModel)]="value.text" [disabled]="saving()" required maxlength="4000"></textarea>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <div class="actions">
        <button type="submit" [disabled]="form.invalid || !value.text.trim() || saving()">Сохранить</button>
        <button type="button" class="secondary" [disabled]="saving()" (click)="cancelled.emit()">Отмена</button>
      </div>
    </form>
  `
})
export class DiaryNoteFormComponent {
  private readonly api = inject(DiaryApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly noteText = viewChild.required<ElementRef<HTMLTextAreaElement>>('noteText');
  readonly draft = input.required<DiaryDraft>();
  readonly saved = output<DiaryNote>();
  readonly cancelled = output<void>();
  readonly saving = signal(false);
  readonly error = signal('');
  readonly ratings = [1, 2, 3, 4, 5];

  constructor() { afterNextRender(() => this.noteText().nativeElement.focus()); }

  save({ id, date, time, text, rating }: DiaryDraft) {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    const request = { date, time: time.length === 5 ? time + ':00' : time, text, rating };
    this.api.saveDiaryNote(request, id).pipe(
      finalize(() => this.saving.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: note => this.saved.emit(note),
      error: error => this.error.set(errorMessage(error))
    });
  }
}
