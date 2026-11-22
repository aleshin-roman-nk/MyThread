import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject as Stream, catchError, forkJoin, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { today } from '../../shared/today';
import { Subject, SubjectsApi } from '../subjects/subjects-api';
import { DiaryApi, DiaryNote } from '../diary/diary-api';
import { NoteEditorComponent } from '../notes/note-editor.component';
import { NoteContentComponent } from '../../shared/note-content.component';
import { DesktopPinComponent } from '../desktop/desktop-pin.component';
import { DateInputComponent } from '../../shared/date-input.component';
import { DaySessionsComponent } from '../sessions/day-sessions.component';
import { DaySession, SessionsApi } from '../sessions/sessions-api';

@Component({
  imports: [FormsModule, RouterLink, NoteEditorComponent, NoteContentComponent, DateInputComponent, DaySessionsComponent, DesktopPinComponent],
  styles: `
    .day-diary-notes { margin-top: 34px; }
    .day-diary-notes .section-heading { flex-wrap: wrap; }
    .day-diary-notes app-note-content { margin: 10px 0 4px; }
    .date-bar { flex-wrap: wrap; }
    .day-navigation { display: flex; align-items: center; gap: 8px; }
    .day-navigation app-date-input { width: 175px; }
    .day-arrow { width: 42px; height: 42px; padding: 8px; font-size: 20px; flex-shrink: 0; }
    .date-bar .useful-work { display: flex; align-items: center; gap: 12px; margin-left: auto; padding-left: 20px; border-left: 1px solid #a8b79f; }
    .useful-work svg { width: 28px; height: 28px; color: #456b58; flex-shrink: 0; }
    .useful-work-label { display: block; font-size: 12px; color: #52644e; }
    .useful-work-value { display: block; font-size: 26px; font-weight: 700; font-variant-numeric: tabular-nums; color: #214f46; white-space: nowrap; }
    @media (max-width: 680px) { .date-bar .useful-work { justify-content: flex-start; margin-left: 0; padding: 12px 0 0; border-left: 0; border-top: 1px solid #a8b79f; } }
  `,
  template: `
    <div class="page-heading">
      <div><p class="eyebrow">МАЛЕНЬКИЙ ШАГ КАЖДЫЙ ДЕНЬ</p><h1>Мой день</h1><p class="subtitle">Сессии за выбранный день.</p></div>
      <button type="button" (click)="openEditor()" [disabled]="loading() || !!error()">+ Новая заметка</button>
    </div>
    <section class="date-bar" aria-label="Выбор дня">
      <div>
        <label for="day">День в дневнике</label>
        <div class="day-navigation">
          <button class="secondary day-arrow" type="button" (click)="shiftDay(-1)" aria-label="Предыдущий день" title="Предыдущий день">←</button>
          <app-date-input inputId="day" [ngModel]="date()" (ngModelChange)="changeDate($event)" required />
          <button class="secondary day-arrow" type="button" (click)="shiftDay(1)" aria-label="Следующий день" title="Следующий день">→</button>
        </div>
      </div>
      <button class="secondary" (click)="changeDate(todayValue)">Сегодня</button>
      <div class="useful-work" title="Сумма закрытых сессий в пределах выбранного дня" aria-live="polite" [attr.aria-busy]="loading()">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></svg>
        <div><span class="useful-work-label">Полезная работа</span><strong class="useful-work-value">{{ loading() ? '…' : error() ? '—' : usefulWorkLabel() }}</strong></div>
      </div>
    </section>
    @if (error()) { <div class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></div> }
    <app-day-sessions [sessions]="sessions()" [loading]="loading()" [unavailable]="!!error()" />
    <section class="day-diary-notes" aria-labelledby="day-diary-notes-heading" [attr.aria-busy]="diaryLoading()">
      <div class="section-heading">
        <h2 id="day-diary-notes-heading">{{ date() === todayValue ? 'Заметки за сегодня' : 'Заметки за выбранный день' }}</h2>
        <a class="text-button" routerLink="/diary">Открыть дневник</a>
      </div>
      @if (diaryLoading()) {
        <p role="status">Загружаем заметки…</p>
      } @else if (diaryError()) {
        <div class="error" role="alert">{{ diaryError() }} <button type="button" class="text-button" (click)="loadDiaryNotes()">Повторить</button></div>
      } @else {
        <div class="note-list">
          @for (note of diaryNotes(); track note.id) {
            <article class="note-card">
              <div class="note-meta">
                <time [attr.datetime]="note.time">{{ note.time.slice(0, 5) }}</time>
                <span>Оценка {{ note.rating }}/5</span>
                <app-desktop-pin kind="note" [targetId]="note.id" [inlineError]="true" />
              </div>
              <app-note-content [text]="note.text" />
            </article>
          } @empty {
            <div class="empty-state"><p>В дневнике за этот день пока нет заметок.</p></div>
          }
        </div>
      }
    </section>
    @if (editorOpen()) {
      <app-note-editor [subjects]="subjects()" [selectedDate]="todayValue"
        (saved)="onSaved()" (cancelled)="closeEditor()" />
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class DayComponent {
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly diaryApi = inject(DiaryApi);
  private readonly sessionsApi = inject(SessionsApi);
  private readonly refresh = new Stream<void>();
  private readonly diaryRefresh = new Stream<void>();
  readonly todayValue = today();
  readonly date = signal(today());
  readonly subjects = signal<Subject[]>([]);
  readonly sessions = signal<DaySession[]>([]);
  readonly diaryNotes = signal<DiaryNote[]>([]);
  readonly diaryLoading = signal(true);
  readonly diaryError = signal('');
  readonly usefulWorkSeconds = signal(0);
  readonly usefulWorkLabel = computed(() => {
    const minutes = Math.floor(this.usefulWorkSeconds() / 60);
    return `${Math.floor(minutes / 60)} ч ${String(minutes % 60).padStart(2, '0')} мин`;
  });
  readonly editorOpen = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly message = signal('');

  constructor() {
    this.refresh.pipe(startWith(undefined), switchMap(() => {
      this.loading.set(true);
      this.error.set('');
      return forkJoin({ subjects: this.subjectsApi.subjects(), sessions: this.sessionsApi.daySessions(this.date()), usefulWork: this.sessionsApi.usefulWork(this.date()) })
        .pipe(catchError(error => { this.error.set(errorMessage(error)); return of(null); }));
    }), takeUntilDestroyed()).subscribe(result => {
      if (result) {
        this.subjects.set(result.subjects);
        this.sessions.set(result.sessions);
        this.usefulWorkSeconds.set(result.usefulWork.totalSeconds);
      }
      this.loading.set(false);
    });
    this.diaryRefresh.pipe(startWith(undefined), switchMap(() => {
      this.diaryLoading.set(true);
      this.diaryError.set('');
      const date = this.date();
      return this.diaryApi.diaryNotes(date, date).pipe(catchError(error => {
        this.diaryError.set(errorMessage(error));
        return of([]);
      }));
    }), takeUntilDestroyed()).subscribe(notes => {
      this.diaryNotes.set(notes);
      this.diaryLoading.set(false);
    });
  }

  loadDiaryNotes() { this.diaryRefresh.next(); }
  shiftDay(direction: number) {
    const date = new Date(`${this.date()}T12:00:00`);
    date.setDate(date.getDate() + direction);
    this.changeDate(today(date));
  }
  changeDate(value: string) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || !Number.isFinite(new Date(`${value}T00:00:00`).getTime()) || value === this.date()) return;
    this.date.set(value);
    this.reload();
    this.loadDiaryNotes();
  }
  openEditor() { this.message.set(''); this.editorOpen.set(true); }
  closeEditor() { this.editorOpen.set(false); }
  reload() { this.refresh.next(); }
  onSaved() { this.closeEditor(); this.message.set('Заметка сохранена в предмете.'); }
}
