import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, catchError, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { today } from '../../shared/today';
import { DateInputComponent } from '../../shared/date-input.component';
import { NoteContentComponent } from '../../shared/note-content.component';
import { ActivatedRoute } from '@angular/router';
import { DesktopPinComponent } from '../desktop/desktop-pin.component';
import { formatNoteTime } from '../../shared/time-format';
import { DiaryApi, DiaryNote } from './diary-api';
import { DiaryDraft, DiaryNoteFormComponent, newDiaryDraft } from './diary-note-form.component';

const notesPerDayPage = 4;
type NoteOrder = 'newest' | 'oldest';

@Component({
  imports: [DatePipe, DateInputComponent, FormsModule, NgTemplateOutlet, NoteContentComponent, DiaryNoteFormComponent, DesktopPinComponent],
  styles: `
    .page-heading { flex-wrap: wrap; }
    .page-heading h1 { margin: 0; }
    .date-bar { flex-wrap: wrap; }
    .date-bar .period-navigation { gap: 6px; }
    .date-bar .period-field { display: flex; align-items: center; gap: 12px; }
    .period-field label[for="diary-date"] { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
    .period-navigation button { flex: 0 0 42px; min-height: 42px; padding: 8px; }
    .period-navigation svg { width: 20px; height: 20px; }
    .date-bar .modes { gap: 6px; margin-left: auto; }
    .date-bar .note-order { width: auto; max-width: 100%; background: #f8faf3; }
    .editor { margin-bottom: 26px; }
    .day-row { display: grid; grid-template-columns: 110px minmax(0, 1fr); gap: 16px; margin-bottom: 18px; }
    .day-row h2 { font-size: 17px; margin: 12px 0 0; }
    .cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); grid-auto-rows: 1fr; gap: 12px; }
    .cards .note-card { display: flex; flex-direction: column; min-width: 0; padding: 14px 16px 10px; }
    .cards .note-preview app-note-content { min-height: 5.55em; margin: 6px 0 8px; }
    .cards .note-actions { margin-top: auto; }
    .day-pages { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 12px; margin-top: 10px; }
    dialog.note-reading { height: auto; max-height: calc(100dvh - 32px); }
    .note-meta { display: flex; justify-content: space-between; gap: 12px; color: #496642; font-size: 14px; font-weight: 600; }
    .note-card app-note-content { margin: 10px 0 4px; }
    .note-actions { align-items: center; margin-top: 12px; }
    @media (max-width: 680px) {
      .day-row { grid-template-columns: 1fr; gap: 8px; }
      .date-bar .period-navigation { flex-wrap: wrap; justify-content: flex-start; }
      .date-bar .period-field { flex: 1; min-width: 0; }
      .date-bar .period-field label { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
      .date-bar .period-field :is(input, app-date-input) { width: 100%; }
      .cards { gap: 8px; }
      .cards .note-card { padding: 10px; }
      .cards .note-meta { flex-wrap: wrap; gap: 4px 8px; }
    }
  `,
  template: `
    <div class="page-heading">
      <h1>Дневник</h1>
      <button type="button" (click)="openEditor()" [disabled]="!!draft() || confirmingId() !== null">+ Новая заметка</button>
    </div>

    <section class="date-bar" aria-label="Период дневника">
      <div class="period-navigation">
        <button type="button" class="secondary" [attr.aria-label]="previousLabel()" [title]="previousLabel()"
          [disabled]="!previousPeriod()" (click)="selectDate(previousPeriod())">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m15 6-6 6 6 6"/></svg>
        </button>
        <div class="period-field">
          @if (mode() === 'day') {
            <label for="diary-date">Дата</label>
            <app-date-input inputId="diary-date" [ngModel]="date()" (ngModelChange)="selectDate($event)" />
          } @else {
            <label for="diary-month">Месяц</label>
            <input #monthInput id="diary-month" type="month" placeholder="гггг-мм" [value]="month()" (change)="selectMonth(monthInput.value)">
          }
        </div>
        <button type="button" class="secondary" [attr.aria-label]="nextLabel()" [title]="nextLabel()"
          [disabled]="!nextPeriod()" (click)="selectDate(nextPeriod())">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 6 6 6-6 6"/></svg>
        </button>
        <button type="button" class="secondary" aria-label="Сегодня" title="Сегодня" (click)="selectDate(today())">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 10h18"/><circle cx="12" cy="15.5" r="2" fill="currentColor" stroke="none"/></svg>
        </button>
      </div>
      <div class="modes" role="group" aria-label="Режим дневника">
        <button type="button" [class.secondary]="mode() !== 'day'" [attr.aria-pressed]="mode() === 'day'" (click)="setMode('day')">День</button>
        <button type="button" [class.secondary]="mode() !== 'month'" [attr.aria-pressed]="mode() === 'month'" (click)="setMode('month')">Месяц</button>
      </div>
      <select class="note-order" aria-label="Порядок заметок" [ngModel]="noteOrder()"
        [disabled]="deleting()" (ngModelChange)="changeNoteOrder($event)">
        <option value="newest">Сначала новые</option>
        <option value="oldest">Сначала старые</option>
      </select>
    </section>

    @if (draft(); as value) {
      <section class="panel editor" aria-labelledby="diary-editor-title">
        <h2 id="diary-editor-title">{{ value.id === undefined ? 'Новая заметка' : 'Редактирование заметки' }}</h2>
        <app-diary-note-form [draft]="value" (saved)="onSaved($event)" (cancelled)="draft.set(null)" />
      </section>
    }

    @if (deleteError()) { <p class="error" role="alert">{{ deleteError() }}</p> }

    @if (loading()) {
      <p role="status">Загружаем заметки…</p>
    } @else if (loadError()) {
      <div class="error" role="alert">{{ loadError() }} <button type="button" class="text-button" (click)="reload()">Повторить</button></div>
    } @else if (!notes().length) {
      <div class="empty-state"><p>{{ mode() === 'day' ? 'За этот день пока нет заметок.' : 'За этот месяц пока нет заметок.' }}</p></div>
    } @else if (mode() === 'day') {
      <section class="note-list" aria-label="Заметки за день">
        @for (note of orderedNotes(); track note.id) {
          <ng-container *ngTemplateOutlet="card; context: { $implicit: note }" />
        }
      </section>
    } @else {
      @for (group of groups(); track group.date) {
        <section class="day-row" [attr.aria-labelledby]="'diary-' + group.date">
          <h2 [id]="'diary-' + group.date"><time [attr.datetime]="group.date">{{ group.date | date:'dd.MM.yyyy' }}</time></h2>
          <div>
            <div class="cards">
              @for (note of group.notes; track note.id) {
                <ng-container *ngTemplateOutlet="card; context: { $implicit: note }" />
              }
            </div>
            @if (group.pageCount > 1) {
              <div class="day-pages" role="group" aria-label="Страницы заметок за день">
                <button type="button" class="text-button" [disabled]="group.page === 0 || deleting()"
                  (click)="changeDayPage(group.date, group.page - 1)">Назад</button>
                <span class="muted" aria-live="polite">{{ group.page + 1 }} из {{ group.pageCount }}</span>
                <button type="button" class="text-button" [disabled]="group.page + 1 === group.pageCount || deleting()"
                  (click)="changeDayPage(group.date, group.page + 1)">Дальше</button>
              </div>
            }
          </div>
        </section>
      }
    }

    <ng-template #card let-note>
      <article class="note-card">
        <div class="note-preview" role="button" tabindex="0" aria-label="Открыть полный текст заметки"
          (click)="showNote(note, reading)" (keydown.enter)="showNote(note, reading)"
          (keydown.space)="$event.preventDefault(); showNote(note, reading)">
          <div class="note-meta">
            <time [attr.datetime]="note.time">{{ note.time.slice(0, 5) }}</time>
            <span>Оценка {{ note.rating }}/5</span>
          </div>
          <app-note-content [text]="note.text" [preview]="mode() === 'month'" />
        </div>
        <div class="actions note-actions">
          <app-desktop-pin kind="note" [targetId]="note.id" [inlineError]="true" />
          @if (confirmingId() === note.id) {
            <span>Удалить заметку?</span>
            <button type="button" class="text-button danger" [disabled]="deleting()" (click)="deleteNote(note.id)">Да, удалить</button>
            <button type="button" class="text-button" [disabled]="deleting()" (click)="cancelDelete()">Отмена</button>
          } @else {
            <button type="button" class="text-button" [disabled]="!!draft() || confirmingId() !== null" (click)="openEditor(note)">Изменить</button>
            <button type="button" class="text-button danger" [disabled]="!!draft() || confirmingId() !== null" (click)="confirmDelete(note.id)">Удалить</button>
          }
        </div>
      </article>
    </ng-template>

    <dialog #reading class="panel note-dialog note-reading" aria-labelledby="diary-reading-title" (close)="viewedNote.set(null)">
      <div class="dialog-heading">
        <h2 id="diary-reading-title">Заметка дневника</h2>
        <button type="button" autofocus class="secondary dialog-close" aria-label="Закрыть" (click)="reading.close()">×</button>
      </div>
      @if (viewedNote(); as note) {
        <div class="note-meta">
          <time [attr.datetime]="note.date + 'T' + note.time">{{ note.date | date:'dd.MM.yyyy' }} · {{ note.time.slice(0, 5) }}</time>
          <span>Оценка {{ note.rating }}/5</span>
        </div>
        <div class="note-dialog-body" tabindex="0"><app-note-content [text]="note.text" /></div>
      }
    </dialog>
  `
})
export class DiaryComponent {
  private readonly api = inject(DiaryApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh = new Subject<void>();
  readonly today = today;
  readonly mode = signal<'day' | 'month'>('day');
  readonly date = signal(today());
  readonly month = computed(() => this.date().slice(0, 7));
  readonly previousLabel = computed(() => this.mode() === 'day' ? 'Предыдущий день' : 'Предыдущий месяц');
  readonly nextLabel = computed(() => this.mode() === 'day' ? 'Следующий день' : 'Следующий месяц');
  readonly previousPeriod = computed(() => this.adjacentPeriod(-1));
  readonly nextPeriod = computed(() => this.adjacentPeriod(1));
  readonly notes = signal<DiaryNote[]>([]);
  readonly noteOrder = signal<NoteOrder>('newest');
  // The API returns notes in ascending order by date, time and ID.
  readonly orderedNotes = computed(() => this.noteOrder() === 'newest' ? this.notes().slice().reverse() : this.notes());
  private readonly dayPages = signal<Record<string, number>>({});
  readonly groups = computed(() => {
    const days = new Map<string, DiaryNote[]>();
    for (const note of this.orderedNotes()) {
      const group = days.get(note.date);
      if (group) group.push(note);
      else days.set(note.date, [note]);
    }
    return Array.from(days, ([date, notes]) => {
      const pageCount = Math.ceil(notes.length / notesPerDayPage);
      const page = Math.min(this.dayPages()[date] ?? 0, pageCount - 1);
      const start = page * notesPerDayPage;
      return { date, notes: notes.slice(start, start + notesPerDayPage), page, pageCount };
    });
  });
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly draft = signal<DiaryDraft | null>(null);
  readonly confirmingId = signal<number | null>(null);
  readonly deleting = signal(false);
  readonly deleteError = signal('');
  readonly viewedNote = signal<DiaryNote | null>(null);

  constructor() {
    const params = inject(ActivatedRoute).snapshot.queryParamMap;
    let noteToEdit = Number(params.get('editNote'));
    const date = params.get('date');
    if (date && /^(?!0000)\d{4}-\d{2}-\d{2}$/.test(date) && Number.isFinite(Date.parse(date))) this.date.set(date);
    this.refresh.pipe(startWith(undefined), switchMap(() => {
      this.loading.set(true);
      this.loadError.set('');
      const from = this.mode() === 'day' ? this.date() : this.month() + '-01';
      const through = new Date(from + 'T00:00:00Z');
      if (this.mode() === 'month') through.setUTCMonth(through.getUTCMonth() + 1, 0);
      return this.api.diaryNotes(from, through.toISOString().slice(0, 10)).pipe(catchError(error => {
        this.loadError.set(errorMessage(error));
        return of([]);
      }));
    }), takeUntilDestroyed()).subscribe(notes => {
      this.notes.set(notes);
      this.loading.set(false);
      const note = notes.find(item => item.id === noteToEdit);
      if (note) {
        noteToEdit = 0;
        this.openEditor(note);
      }
    });
  }

  setMode(mode: 'day' | 'month') {
    if (mode === this.mode()) return;
    this.cancelDelete();
    this.mode.set(mode);
    this.reload();
  }

  changeNoteOrder(order: NoteOrder) {
    if (order === this.noteOrder()) return;
    this.cancelDelete();
    this.dayPages.set({});
    this.noteOrder.set(order);
  }

  selectDate(value: string) {
    if (!value || value === this.date()) return;
    this.cancelDelete();
    this.date.set(value);
    this.reload();
  }

  selectMonth(value: string) {
    if (/^(?!0000)\d{4}-(0[1-9]|1[0-2])$/.test(value) && value !== this.month()) {
      this.selectDate(value === today().slice(0, 7) ? today() : value + '-01');
    }
  }

  private adjacentPeriod(offset: number): string {
    const monthly = this.mode() === 'month';
    const current = new Date((monthly ? this.month() + '-01' : this.date()) + 'T00:00:00Z');
    if (monthly) current.setUTCMonth(current.getUTCMonth() + offset);
    else current.setUTCDate(current.getUTCDate() + offset);

    const year = current.getUTCFullYear();
    if (year < 1 || year > 9999) return '';
    const date = current.toISOString().slice(0, 10);
    return date === '0001-01-01' ? '' : date;
  }

  changeDayPage(date: string, page: number) {
    this.cancelDelete();
    this.dayPages.update(pages => ({ ...pages, [date]: page }));
  }

  showNote(note: DiaryNote, dialog: HTMLDialogElement) {
    this.viewedNote.set(note);
    dialog.showModal();
  }

  openEditor(note?: DiaryNote) {
    this.deleteError.set('');
    this.draft.set(note
      ? { ...note, time: formatNoteTime(note.time) }
      : newDiaryDraft(this.date()));
  }

  onSaved(note: DiaryNote) {
    this.draft.set(null);
    this.date.set(note.date);
    this.reload();
  }

  confirmDelete(id: number) {
    this.deleteError.set('');
    this.confirmingId.set(id);
  }

  cancelDelete() {
    if (this.deleting()) return;
    this.confirmingId.set(null);
    this.deleteError.set('');
  }

  deleteNote(id: number) {
    if (this.deleting() || this.confirmingId() !== id) return;
    this.deleting.set(true);
    this.deleteError.set('');
    this.api.deleteDiaryNote(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.deleting.set(false);
        this.cancelDelete();
        this.reload();
      },
      error: error => {
        this.deleting.set(false);
        this.confirmingId.set(null);
        this.deleteError.set(errorMessage(error));
      }
    });
  }

  reload() { this.refresh.next(); }
}
