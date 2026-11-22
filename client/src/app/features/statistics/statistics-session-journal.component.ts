import { DatePipe } from '@angular/common';
import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, input, OnInit, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { errorMessage } from '../../shared/error-message';
import { today } from '../../shared/today';
import { monthBounds } from './statistics-period';
import { SessionRowComponent } from '../sessions/session-row.component';
import { DaySession, SessionsApi } from '../sessions/sessions-api';

@Component({
  selector: 'app-statistics-session-journal',
  imports: [DatePipe, SessionRowComponent],
  styles: `
    .journal { width: min(940px, calc(100% - 24px)); max-height: calc(100dvh - 24px); color: inherit; }
    .journal[open] { display: flex; flex-direction: column; overflow: hidden; }
    .journal::backdrop { background: #172b27a6; }
    .dialog-heading { margin-bottom: 16px; }
    .journal-context { margin: 0; overflow-wrap: anywhere; }
    .journal-content { min-height: 0; overflow: auto; padding: 2px; }
    .journal-day + .journal-day { margin-top: 18px; }
    .journal-day h3 { margin: 0 0 8px; font-size: 15px; }
    .journal-rows { display: grid; gap: 6px; }
    .journal-rows app-session-row { min-width: 650px; }
  `,
  template: `
    <dialog #dialog class="panel journal" aria-labelledby="statistics-journal-title"
      aria-describedby="statistics-journal-context" (close)="dismissed.emit()">
      <div class="dialog-heading">
        <div>
          <h2 id="statistics-journal-title">Журнал сессий</h2>
          <p id="statistics-journal-context" class="muted journal-context">
            {{ subject().name }} ·
            @if (period() === 'month') {
              <time [attr.datetime]="date().slice(0, 7)">{{ monthLabel() }}</time>
            } @else {
              <time [attr.datetime]="date()">{{ date() | date:'dd.MM.yyyy' }}</time>
            }
          </p>
        </div>
        <button type="button" class="secondary dialog-close" aria-label="Закрыть журнал" (click)="dialog.close()">×</button>
      </div>

      @if (loading()) {
        <p role="status">Загружаем сессии…</p>
      } @else if (loadError()) {
        <p class="error" role="alert">{{ loadError() }}
          <button type="button" class="text-button" (click)="load()">Повторить</button>
        </p>
      } @else if (groups().length) {
        <div class="journal-content">
          @for (group of groups(); track group.date) {
            <section class="journal-day" [attr.aria-labelledby]="period() === 'month' ? 'journal-day-' + group.date : null">
              @if (period() === 'month') {
                <h3 [id]="'journal-day-' + group.date">
                  <time [attr.datetime]="group.date">{{ group.date | date:'dd.MM.yyyy' }}</time> · {{ group.weekday }}
                </h3>
              }
              <div class="journal-rows">
                @for (entry of group.entries; track entry.session.id) {
                  <app-session-row [session]="entry.session" [fullCode]="entry.fullCode" [targetName]="entry.targetName" />
                }
              </div>
            </section>
          }
        </div>
      } @else {
        <p class="muted">За выбранный {{ period() === 'month' ? 'месяц' : 'день' }} сессий по этому предмету нет.</p>
      }
    </dialog>
  `
})
export class StatisticsSessionJournalComponent implements OnInit {
  private readonly api = inject(SessionsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly subject = input.required<{ id: number; name: string }>();
  readonly date = input.required<string>();
  readonly period = input<'day' | 'month'>('day');
  readonly dismissed = output<void>();
  private readonly monthFormat = new Intl.DateTimeFormat('ru', { month: 'long', year: 'numeric', timeZone: 'UTC' });
  private readonly weekdayFormat = new Intl.DateTimeFormat('ru', { weekday: 'long', timeZone: 'UTC' });
  private readonly bounds = computed(() => {
    if (this.period() === 'day') return { from: this.date(), through: this.date() };

    return monthBounds(this.date().slice(0, 7));
  });
  readonly monthLabel = computed(() => this.monthFormat.format(new Date(this.bounds().from)));
  private readonly sessions = signal<DaySession[]>([]);
  readonly groups = computed(() => {
    const entries = this.sessions();
    if (!entries.length) return [];
    const { from } = this.bounds();
    if (this.period() === 'day') return [{ date: from, weekday: '', entries }];

    const byDate = new Map<string, DaySession[]>();
    for (const entry of entries) {
      const startDate = today(new Date(entry.session.startTime)).padStart(10, '0');
      const date = startDate < from ? from : startDate;
      const group = byDate.get(date);
      if (group) group.push(entry);
      else byDate.set(date, [entry]);
    }
    return Array.from(byDate, ([date, entries]) => ({
      date, weekday: this.weekdayFormat.format(new Date(date)), entries
    }));
  });
  readonly loading = signal(true);
  readonly loadError = signal('');

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.loadError.set('');
    const { from, through } = this.bounds();
    this.api.subjectSessionJournal(this.subject().id, from, through).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: sessions => {
        this.sessions.set(sessions);
        this.loading.set(false);
      },
      error: error => {
        this.loadError.set(errorMessage(error));
        this.loading.set(false);
      }
    });
  }
}

